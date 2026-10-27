using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Common;

namespace SheikhTravelSystem.Infrastructure.Services.Drivers;

public sealed record NadraVerificationAttempt(
    bool CalledProvider,
    bool Success,
    string Status,
    string Summary,
    string? ExternalReference,
    int? HttpStatusCode,
    IReadOnlyList<string>? MissingConfigKeys);

public sealed class NadraCnicVerificationClient(
    IHttpClientFactory httpClientFactory,
    IOptions<DriverVerificationOptions> options,
    ILogger<NadraCnicVerificationClient> logger)
{
    public const string HttpClientName = "NadraVerification";

    public IReadOnlyList<string> DescribeMissingConfiguration()
    {
        var o = options.Value.Nadra;
        if (!o.Enabled)
            return ["DriverVerification:Nadra:Enabled (set true after onboarding)"];

        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(o.BaseUrl))
            missing.Add("DriverVerification:Nadra:BaseUrl (or env DriverVerification__Nadra__BaseUrl)");

        var channel = (o.Channel ?? "Nishan").Trim();
        if (channel.Equals("ESahulat", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(o.FranchiseId))
                missing.Add("DriverVerification:Nadra:FranchiseId");
            if (string.IsNullOrWhiteSpace(o.Username))
                missing.Add("DriverVerification:Nadra:Username");
            if (string.IsNullOrWhiteSpace(o.Password))
                missing.Add("DriverVerification:Nadra:Password (or DriverVerification__Nadra__Password)");
            if (string.IsNullOrWhiteSpace(o.ESahulatVerifyPath))
                missing.Add("DriverVerification:Nadra:ESahulatVerifyPath");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(o.CompanyId))
                missing.Add("DriverVerification:Nadra:CompanyId (Nishaan portal client id)");
            if (string.IsNullOrWhiteSpace(o.ApiKey))
                missing.Add("DriverVerification:Nadra:ApiKey (or DriverVerification__Nadra__ApiKey)");
            if (string.IsNullOrWhiteSpace(o.VerisysPath))
                missing.Add("DriverVerification:Nadra:VerisysPath");
        }

        return missing;
    }

    public async Task<NadraVerificationAttempt> VerifyCnicAsync(
        string cnic,
        string? holderName,
        CancellationToken cancellationToken)
    {
        var missing = DescribeMissingConfiguration();
        if (missing.Count > 0)
        {
            return new NadraVerificationAttempt(
                CalledProvider: false,
                Success: false,
                Status: "NotConfigured",
                Summary: "NADRA CNIC verification is not configured.",
                ExternalReference: null,
                HttpStatusCode: null,
                MissingConfigKeys: missing);
        }

        var o = options.Value.Nadra;
        var channel = (o.Channel ?? "Nishan").Trim();
        var client = httpClientFactory.CreateClient(HttpClientName);
        client.Timeout = TimeSpan.FromSeconds(Math.Clamp(o.TimeoutSeconds, 5, 120));

        try
        {
            if (channel.Equals("ESahulat", StringComparison.OrdinalIgnoreCase))
                return await CallESahulatAsync(client, o, cnic, cancellationToken);

            return await CallNishanVerisysAsync(client, o, cnic, holderName, cancellationToken);
        }
        catch (TaskCanceledException)
        {
            return Fail("Error", "NADRA provider call timed out.", null);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "NADRA CNIC verification HTTP error.");
            return Fail("Error", $"NADRA provider error: {ex.Message}", null);
        }
    }

    private async Task<NadraVerificationAttempt> CallNishanVerisysAsync(
        HttpClient client,
        NadraVerificationOptions o,
        string cnic,
        string? holderName,
        CancellationToken cancellationToken)
    {
        var url = CombineUrl(o.BaseUrl, o.VerisysPath);
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.TryAddWithoutValidation("X-Company-Id", o.CompanyId);
        request.Headers.TryAddWithoutValidation("X-Api-Key", o.ApiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var payload = new Dictionary<string, object?>
        {
            ["citizenNo"] = NormalizeCnic(cnic),
            ["cnic"] = NormalizeCnic(cnic),
            ["name"] = holderName
        };
        request.Content = JsonContent.Create(payload);

        var response = await client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return ParseProviderResponse(response.StatusCode, body, "NADRA Verisys / Nishaan");
    }

    private async Task<NadraVerificationAttempt> CallESahulatAsync(
        HttpClient client,
        NadraVerificationOptions o,
        string cnic,
        CancellationToken cancellationToken)
    {
        var url = CombineUrl(o.BaseUrl, o.ESahulatVerifyPath);
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{o.Username}:{o.Password}")));

        var payload = new Dictionary<string, object?>
        {
            ["franchiseId"] = o.FranchiseId,
            ["cnic"] = NormalizeCnic(cnic)
        };
        request.Content = JsonContent.Create(payload);

        var response = await client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return ParseProviderResponse(response.StatusCode, body, "NADRA e-Sahulat");
    }

    private NadraVerificationAttempt ParseProviderResponse(
        System.Net.HttpStatusCode statusCode,
        string body,
        string providerLabel)
    {
        var code = (int)statusCode;
        if (code is >= 200 and < 300)
        {
            var verified = InferVerifiedFromJson(body);
            if (verified == true)
            {
                var reference = TryReadReference(body);
                return new NadraVerificationAttempt(
                    true, true, "Verified",
                    $"{providerLabel} confirmed CNIC.",
                    reference, code, null);
            }

            if (verified == false)
            {
                return new NadraVerificationAttempt(
                    true, false, "Failed",
                    $"{providerLabel} rejected CNIC (demographic mismatch or invalid).",
                    TryReadReference(body), code, null);
            }

            return new NadraVerificationAttempt(
                true, true, "Verified",
                $"{providerLabel} returned HTTP {code}; treated as success (review response in audit).",
                TryReadReference(body), code, null);
        }

        return new NadraVerificationAttempt(
            true, false, "Failed",
            $"{providerLabel} HTTP {code}: {Truncate(body, 200)}",
            null, code, null);
    }

    private static bool? InferVerifiedFromJson(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            foreach (var key in new[] { "verified", "isVerified", "success", "valid", "status" })
            {
                if (!root.TryGetProperty(key, out var prop)) continue;
                if (prop.ValueKind == JsonValueKind.True) return true;
                if (prop.ValueKind == JsonValueKind.False) return false;
                if (prop.ValueKind == JsonValueKind.String)
                {
                    var s = prop.GetString() ?? "";
                    if (s.Contains("verified", StringComparison.OrdinalIgnoreCase)
                        || s.Equals("ok", StringComparison.OrdinalIgnoreCase)
                        || s.Equals("success", StringComparison.OrdinalIgnoreCase))
                        return true;
                    if (s.Contains("fail", StringComparison.OrdinalIgnoreCase)
                        || s.Contains("invalid", StringComparison.OrdinalIgnoreCase))
                        return false;
                }
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    private static string? TryReadReference(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            foreach (var key in new[] { "transactionId", "reference", "requestId", "id" })
            {
                if (doc.RootElement.TryGetProperty(key, out var prop) && prop.ValueKind == JsonValueKind.String)
                    return prop.GetString();
            }
        }
        catch (JsonException) { }

        return null;
    }

    private static string NormalizeCnic(string cnic)
        => new string(cnic.Where(char.IsDigit).ToArray());

    private static string CombineUrl(string baseUrl, string path)
    {
        var b = baseUrl.TrimEnd('/');
        var p = path.StartsWith('/') ? path : "/" + path;
        return b + p;
    }

    private static NadraVerificationAttempt Fail(string status, string summary, int? code)
        => new(false, false, status, summary, null, code, null);

    private static string Truncate(string s, int max)
        => s.Length <= max ? s : s[..max] + "…";
}
