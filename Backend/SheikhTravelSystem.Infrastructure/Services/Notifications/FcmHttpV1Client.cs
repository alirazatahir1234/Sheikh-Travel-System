using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace SheikhTravelSystem.Infrastructure.Services.Notifications;

/// <summary>
/// Minimal FCM HTTP v1 client using a Google service-account JSON file.
/// </summary>
public sealed class FcmHttpV1Client(IHttpClientFactory httpClientFactory, ILogger<FcmHttpV1Client> logger)
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);
    private string? _cachedToken;
    private DateTime _tokenExpiresAt = DateTime.MinValue;
    private readonly object _gate = new();

    public async Task<(bool Success, string Detail)> SendAsync(
        string projectId,
        string credentialsPath,
        IReadOnlyList<string> tokens,
        string title,
        string body,
        CancellationToken cancellationToken = default)
    {
        if (tokens.Count == 0)
            return (false, "No device tokens");

        var accessToken = await GetAccessTokenAsync(credentialsPath, cancellationToken);
        if (string.IsNullOrEmpty(accessToken))
            return (false, "Failed to obtain FCM access token");

        var client = httpClientFactory.CreateClient("FcmHttpV1");
        var sent = 0;
        var errors = new List<string>();

        foreach (var token in tokens.Distinct())
        {
            var payload = JsonSerializer.Serialize(new
            {
                message = new
                {
                    token,
                    notification = new { title, body },
                    android = new { priority = "high" },
                    apns = new { headers = new Dictionary<string, string> { ["apns-priority"] = "10" } }
                }
            }, JsonOpts);

            using var req = new HttpRequestMessage(
                HttpMethod.Post,
                $"https://fcm.googleapis.com/v1/projects/{projectId}/messages:send")
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            try
            {
                using var res = await client.SendAsync(req, cancellationToken);
                if (res.IsSuccessStatusCode)
                {
                    sent++;
                }
                else
                {
                    var err = await res.Content.ReadAsStringAsync(cancellationToken);
                    errors.Add($"{(int)res.StatusCode}: {Truncate(err, 120)}");
                    logger.LogWarning("FCM send failed for token …{Suffix}: {Status} {Body}",
                        token.Length > 8 ? token[^8..] : token, (int)res.StatusCode, Truncate(err, 200));
                }
            }
            catch (Exception ex)
            {
                errors.Add(ex.Message);
                logger.LogWarning(ex, "FCM HTTP error");
            }
        }

        if (sent == 0)
            return (false, errors.Count > 0 ? string.Join("; ", errors.Take(3)) : "All FCM sends failed");

        return (true, $"FCM delivered to {sent}/{tokens.Count} token(s)");
    }

    private async Task<string?> GetAccessTokenAsync(string credentialsPath, CancellationToken ct)
    {
        lock (_gate)
        {
            if (!string.IsNullOrEmpty(_cachedToken) && DateTime.UtcNow < _tokenExpiresAt.AddMinutes(-2))
                return _cachedToken;
        }

        if (!File.Exists(credentialsPath))
        {
            logger.LogWarning("FCM credentials file not found: {Path}", credentialsPath);
            return null;
        }

        await using var stream = File.OpenRead(credentialsPath);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        var root = doc.RootElement;
        var clientEmail = root.GetProperty("client_email").GetString();
        var privateKeyPem = root.GetProperty("private_key").GetString();
        var tokenUri = root.TryGetProperty("token_uri", out var tu)
            ? tu.GetString() ?? "https://oauth2.googleapis.com/token"
            : "https://oauth2.googleapis.com/token";

        if (string.IsNullOrWhiteSpace(clientEmail) || string.IsNullOrWhiteSpace(privateKeyPem))
            return null;

        var now = DateTime.UtcNow;
        var assertion = CreateServiceAccountJwt(clientEmail!, privateKeyPem!, now);

        var client = httpClientFactory.CreateClient("FcmHttpV1");
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
            ["assertion"] = assertion
        });

        using var res = await client.PostAsync(tokenUri, form, ct);
        var json = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
        {
            logger.LogWarning("Google OAuth token exchange failed: {Status} {Body}", (int)res.StatusCode, Truncate(json, 200));
            return null;
        }

        using var tokenDoc = JsonDocument.Parse(json);
        var accessToken = tokenDoc.RootElement.GetProperty("access_token").GetString();
        var expiresIn = tokenDoc.RootElement.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 3600;

        lock (_gate)
        {
            _cachedToken = accessToken;
            _tokenExpiresAt = DateTime.UtcNow.AddSeconds(expiresIn);
        }

        return accessToken;
    }

    private static string CreateServiceAccountJwt(string clientEmail, string privateKeyPem, DateTime utcNow)
    {
        var rsa = RSA.Create();
        rsa.ImportFromPem(privateKeyPem.AsSpan());
        var key = new RsaSecurityKey(rsa);
        var creds = new SigningCredentials(key, SecurityAlgorithms.RsaSha256);

        var token = new JwtSecurityToken(
            issuer: clientEmail,
            audience: "https://oauth2.googleapis.com/token",
            claims:
            [
                new Claim("scope", "https://www.googleapis.com/auth/firebase.messaging"),
                new Claim(JwtRegisteredClaimNames.Sub, clientEmail)
            ],
            notBefore: utcNow,
            expires: utcNow.AddMinutes(55),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";
}
