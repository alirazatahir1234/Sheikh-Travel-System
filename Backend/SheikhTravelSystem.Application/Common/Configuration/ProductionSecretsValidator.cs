using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace SheikhTravelSystem.Application.Common.Configuration;

/// <summary>
/// Fail-fast startup checks for required host secrets outside Development.
/// Never logs or returns secret values — only configuration key names.
/// </summary>
public static class ProductionSecretsValidator
{
    public const string Placeholder = "__SET_IN_USER_SECRETS_OR_ENV__";
    public const int MinJwtSecretLength = 32;

    /// <summary>
    /// Returns human-readable missing/invalid key names. Empty when OK.
    /// Skips all checks when <paramref name="environment"/> is Development or named "Testing".
    /// </summary>
    public static IReadOnlyList<string> CollectFailures(IConfiguration configuration, IHostEnvironment environment)
    {
        if (environment.IsDevelopment()
            || string.Equals(environment.EnvironmentName, "Testing", StringComparison.OrdinalIgnoreCase))
        {
            return Array.Empty<string>();
        }

        var failures = new List<string>();

        RequireConfigured(configuration["ConnectionStrings:DefaultConnection"], "ConnectionStrings:DefaultConnection", failures);

        var jwt = configuration["JwtSettings:Secret"];
        if (IsMissingOrPlaceholder(jwt))
            failures.Add("JwtSettings:Secret");
        else if (jwt!.Length < MinJwtSecretLength)
            failures.Add($"JwtSettings:Secret (minimum length {MinJwtSecretLength})");

        var traccarEnabled = configuration.GetValue("Traccar:Enabled", false);
        if (traccarEnabled)
            RequireConfigured(configuration["Traccar:Password"], "Traccar:Password", failures);

        var mapsKey = configuration["GoogleMaps:ApiKey"];
        if (IsMissingOrPlaceholder(mapsKey))
            mapsKey = configuration["Geocoding:GoogleMapsApiKey"];
        RequireConfigured(mapsKey, "GoogleMaps:ApiKey or Geocoding:GoogleMapsApiKey", failures);

        return failures;
    }

    public static void EnsureValidOrThrow(IConfiguration configuration, IHostEnvironment environment)
    {
        var failures = CollectFailures(configuration, environment);
        if (failures.Count == 0)
            return;

        throw new InvalidOperationException(
            "Production host secrets are missing or still set to placeholders. " +
            "Configure environment variables or the host secret store. Missing/invalid keys: " +
            string.Join("; ", failures) +
            ". See Backend/README.secrets.md.");
    }

    private static void RequireConfigured(string? value, string keyName, List<string> failures)
    {
        if (IsMissingOrPlaceholder(value))
            failures.Add(keyName);
    }

    public static bool IsMissingOrPlaceholder(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return true;
        if (string.Equals(value.Trim(), Placeholder, StringComparison.Ordinal))
            return true;
        if (value.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase))
            return true;
        return false;
    }
}
