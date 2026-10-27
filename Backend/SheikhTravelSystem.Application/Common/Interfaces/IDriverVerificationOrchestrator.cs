namespace SheikhTravelSystem.Application.Common.Interfaces;

public sealed record DriverProviderCheckResult(
    string Provider,
    string DocumentType,
    string Status,
    string Summary,
    IReadOnlyList<string>? MissingConfigKeys = null,
    string? ExternalReference = null,
    int? HttpStatusCode = null);

public interface IDriverVerificationOrchestrator
{
    /// <summary>Runs provider checks for CNIC and/or background screening after upload or manual trigger.</summary>
    Task<IReadOnlyList<DriverProviderCheckResult>> RunForDriverAsync(
        int tenantId,
        int driverId,
        string? documentTypeFilter,
        bool manualTrigger,
        CancellationToken cancellationToken = default);

    IReadOnlyList<string> DescribeMissingNadraConfiguration();
    IReadOnlyList<string> DescribeMissingUaeKycConfiguration();
}
