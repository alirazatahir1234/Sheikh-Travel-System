using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;

namespace SheikhTravelSystem.Infrastructure.Services.Drivers;

public sealed class DriverVerificationOrchestrator(
    IDriverRepository driverRepository,
    NadraCnicVerificationClient nadraClient,
    UaeKycBackgroundScreeningClient uaeClient,
    IOptions<DriverVerificationOptions> options,
    ILogger<DriverVerificationOrchestrator> logger)
    : IDriverVerificationOrchestrator
{
    public IReadOnlyList<string> DescribeMissingNadraConfiguration()
        => nadraClient.DescribeMissingConfiguration();

    public IReadOnlyList<string> DescribeMissingUaeKycConfiguration()
        => uaeClient.DescribeMissingConfiguration();

    public async Task<IReadOnlyList<DriverProviderCheckResult>> RunForDriverAsync(
        int tenantId,
        int driverId,
        string? documentTypeFilter,
        bool manualTrigger,
        CancellationToken cancellationToken = default)
    {
        var driver = await driverRepository.GetByIdAsync(tenantId, driverId, cancellationToken);
        var results = new List<DriverProviderCheckResult>();

        var runCnic = ShouldRun("CNIC", documentTypeFilter);
        var runBackground = ShouldRun("BackgroundCheck", documentTypeFilter);

        if (runCnic)
        {
            if (string.IsNullOrWhiteSpace(driver.CNIC))
            {
                results.Add(await PersistAsync(tenantId, driverId, "Nadra", "CNIC", "Failed",
                    "CNIC number is missing on driver profile — add CNIC before NADRA verification.",
                    null, null, null, cancellationToken));
            }
            else
            {
                var attempt = await nadraClient.VerifyCnicAsync(driver.CNIC, driver.FullName, cancellationToken);
                results.Add(await PersistAsync(tenantId, driverId, MapNadraProvider(), "CNIC",
                    attempt.Status, attempt.Summary, attempt.ExternalReference, attempt.HttpStatusCode,
                    attempt.MissingConfigKeys, cancellationToken));
            }
        }

        if (runBackground && IsUaeDriver(driver.Nationality))
        {
            var attempt = await uaeClient.ScreenDriverAsync(
                driver.FullName,
                driver.Nationality,
                driver.CNIC,
                driver.DateOfBirth,
                cancellationToken);

            var uaeProvider = options.Value.UaeKyc.Provider ?? "UaeKyc";
            results.Add(await PersistAsync(tenantId, driverId, uaeProvider, "BackgroundCheck",
                attempt.Status, attempt.Summary, attempt.ExternalReference, attempt.HttpStatusCode,
                attempt.MissingConfigKeys, cancellationToken));
        }
        else if (runBackground && manualTrigger)
        {
            results.Add(await PersistAsync(tenantId, driverId, "UaeKyc", "BackgroundCheck", "Skipped",
                "Background screening applies to UAE-based drivers (nationality).",
                null, null, null, cancellationToken));
        }

        if (results.Count == 0)
            logger.LogDebug("No provider verification checks selected for driver {DriverId}", driverId);

        return results;
    }

    private string MapNadraProvider()
    {
        var channel = (options.Value.Nadra.Channel ?? "Nishan").Trim();
        return channel.Equals("ESahulat", StringComparison.OrdinalIgnoreCase)
            ? "NadraESahulat"
            : "NadraVerisys";
    }

    private static bool ShouldRun(string documentType, string? filter)
        => string.IsNullOrWhiteSpace(filter)
           || string.Equals(filter, documentType, StringComparison.OrdinalIgnoreCase);

    private static bool IsUaeDriver(string? nationality)
    {
        if (string.IsNullOrWhiteSpace(nationality)) return false;
        var n = nationality.Trim();
        return n.Contains("uae", StringComparison.OrdinalIgnoreCase)
               || n.Contains("emirat", StringComparison.OrdinalIgnoreCase)
               || n.Contains("dubai", StringComparison.OrdinalIgnoreCase)
               || n.Equals("ae", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<DriverProviderCheckResult> PersistAsync(
        int tenantId,
        int driverId,
        string provider,
        string documentType,
        string status,
        string summary,
        string? externalReference,
        int? httpStatusCode,
        IReadOnlyList<string>? missingKeys,
        CancellationToken cancellationToken)
    {
        await driverRepository.InsertProviderVerificationCheckAsync(
            tenantId, driverId, provider, documentType, status, summary,
            externalReference, httpStatusCode, missingKeys, cancellationToken);

        return new DriverProviderCheckResult(
            provider, documentType, status, summary, missingKeys, externalReference, httpStatusCode);
    }
}
