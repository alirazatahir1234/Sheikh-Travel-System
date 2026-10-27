using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Common;

namespace SheikhTravelSystem.Infrastructure.Services.Drivers;

public sealed record UaeScreeningAttempt(
    bool CalledProvider,
    bool Success,
    string Status,
    string Summary,
    string? ExternalReference,
    int? HttpStatusCode,
    IReadOnlyList<string>? MissingConfigKeys);

public sealed class UaeKycBackgroundScreeningClient(
    IHttpClientFactory httpClientFactory,
    IOptions<DriverVerificationOptions> options,
    ILogger<UaeKycBackgroundScreeningClient> logger)
{
    public const string HttpClientName = "UaeKycVerification";

    public IReadOnlyList<string> DescribeMissingConfiguration()
    {
        var o = options.Value.UaeKyc;
        if (!o.Enabled)
            return ["DriverVerification:UaeKyc:Enabled (set true after partner onboarding)"];

        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(o.BaseUrl))
            missing.Add("DriverVerification:UaeKyc:BaseUrl");
        if (string.IsNullOrWhiteSpace(o.ClientId))
            missing.Add("DriverVerification:UaeKyc:ClientId (or DriverVerification__UaeKyc__ClientId)");
        if (string.IsNullOrWhiteSpace(o.ClientSecret))
            missing.Add("DriverVerification:UaeKyc:ClientSecret (or DriverVerification__UaeKyc__ClientSecret)");
        if (string.IsNullOrWhiteSpace(o.ScreeningPath))
            missing.Add("DriverVerification:UaeKyc:ScreeningPath (from Bayzat / AML partner contract)");
        return missing;
    }

    public async Task<UaeScreeningAttempt> ScreenDriverAsync(
        string fullName,
        string? nationality,
        string? emiratesIdOrPassport,
        DateTime? dateOfBirth,
        CancellationToken cancellationToken)
    {
        var missing = DescribeMissingConfiguration();
        if (missing.Count > 0)
        {
            return new UaeScreeningAttempt(
                CalledProvider: false,
                Success: false,
                Status: "NotConfigured",
                Summary: "UAE KYC / background screening is not configured.",
                ExternalReference: null,
                HttpStatusCode: null,
                MissingConfigKeys: missing);
        }

        var o = options.Value.UaeKyc;
        var client = httpClientFactory.CreateClient(HttpClientName);
        client.Timeout = TimeSpan.FromSeconds(Math.Clamp(o.TimeoutSeconds, 5, 120));

        try
        {
            var token = await AcquireTokenAsync(client, o, cancellationToken);
            if (token is null)
            {
                return new UaeScreeningAttempt(
                    true, false, "Failed",
                    "UAE KYC OAuth token request failed — check ClientId/ClientSecret and OAuthTokenPath.",
                    null, null, null);
            }

            var url = CombineUrl(o.BaseUrl, o.ScreeningPath);
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (!string.IsNullOrWhiteSpace(o.CompanyId))
                request.Headers.TryAddWithoutValidation("X-Company-Id", o.CompanyId);

            var payload = new Dictionary<string, object?>
            {
                ["fullName"] = fullName,
                ["nationality"] = nationality,
                ["emiratesId"] = emiratesIdOrPassport,
                ["dateOfBirth"] = dateOfBirth?.ToString("yyyy-MM-dd"),
                ["screeningType"] = "employment_background",
                ["provider"] = o.Provider
            };
            request.Content = JsonContent.Create(payload);

            var response = await client.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var code = (int)response.StatusCode;

            if (code is >= 200 and < 300)
            {
                var cleared = InferCleared(body);
                if (cleared == false)
                {
                    return new UaeScreeningAttempt(
                        true, false, "Failed",
                        "UAE KYC partner flagged background screening — review in ERP.",
                        TryReadReference(body), code, null);
                }

                return new UaeScreeningAttempt(
                    true, true, "Verified",
                    $"UAE KYC ({o.Provider}) background screening cleared.",
                    TryReadReference(body), code, null);
            }

            return new UaeScreeningAttempt(
                true, false, "Failed",
                $"UAE KYC HTTP {code}: {Truncate(body, 200)}",
                null, code, null);
        }
        catch (TaskCanceledException)
        {
            return new UaeScreeningAttempt(false, false, "Error", "UAE KYC provider call timed out.", null, null, null);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "UAE KYC screening HTTP error.");
            return new UaeScreeningAttempt(false, false, "Error", ex.Message, null, null, null);
        }
    }

    private async Task<string?> AcquireTokenAsync(
        HttpClient client, UaeKycVerificationOptions o, CancellationToken cancellationToken)
    {
        var url = CombineUrl(o.BaseUrl, o.OAuthTokenPath);
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = o.ClientId,
            ["client_secret"] = o.ClientSecret
        });

        var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("access_token", out var token))
                return token.GetString();
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    private static bool? InferCleared(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            foreach (var key in new[] { "cleared", "approved", "success", "status" })
            {
                if (!doc.RootElement.TryGetProperty(key, out var prop)) continue;
                if (prop.ValueKind == JsonValueKind.True) return true;
                if (prop.ValueKind == JsonValueKind.False) return false;
                if (prop.ValueKind == JsonValueKind.String)
                {
                    var s = prop.GetString() ?? "";
                    if (s.Contains("clear", StringComparison.OrdinalIgnoreCase)
                        || s.Contains("pass", StringComparison.OrdinalIgnoreCase))
                        return true;
                    if (s.Contains("fail", StringComparison.OrdinalIgnoreCase)
                        || s.Contains("reject", StringComparison.OrdinalIgnoreCase))
                        return false;
                }
            }
        }
        catch (JsonException) { }

        return null;
    }

    private static string? TryReadReference(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            foreach (var key in new[] { "screeningId", "caseId", "reference", "id" })
            {
                if (doc.RootElement.TryGetProperty(key, out var prop) && prop.ValueKind == JsonValueKind.String)
                    return prop.GetString();
            }
        }
        catch (JsonException) { }

        return null;
    }

    private static string CombineUrl(string baseUrl, string path)
    {
        var b = baseUrl.TrimEnd('/');
        var p = path.StartsWith('/') ? path : "/" + path;
        return b + p;
    }

    private static string Truncate(string s, int max)
        => s.Length <= max ? s : s[..max] + "…";
}
