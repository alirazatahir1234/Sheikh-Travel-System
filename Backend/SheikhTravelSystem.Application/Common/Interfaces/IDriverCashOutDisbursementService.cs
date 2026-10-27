namespace SheikhTravelSystem.Application.Common.Interfaces;

public record DriverCashOutDisbursementRequest(
    int CashOutRequestId,
    int TenantId,
    int DriverId,
    decimal NetPayout,
    string Method,
    string DestinationSnapshot);

public record DriverCashOutDisbursementResult(bool Success, string? ProviderReference, string? Message);

public interface IDriverCashOutDisbursementService
{
    Task<DriverCashOutDisbursementResult> DisburseAsync(
        DriverCashOutDisbursementRequest request,
        CancellationToken cancellationToken = default);
}
