namespace SheikhTravelSystem.Application.Common.Interfaces.Repositories;

public record DriverMobileIdempotencyRow(
    string ClientOpId,
    string OpType,
    string ResponseJson,
    int HttpStatus,
    DateTime CreatedAt);

public record DriverVoiceCommandInsert(
    int TenantId,
    int DriverId,
    string? ClientOpId,
    string LanguageCode,
    string Transcript,
    string Intent,
    int? VehicleId,
    int? BookingId,
    double? Latitude,
    double? Longitude,
    int? SosAlertId,
    DateTime CreatedAt);

public interface IDriverMobileSyncRepository
{
    Task<DriverMobileIdempotencyRow?> GetIdempotencyAsync(
        int tenantId, string clientOpId, CancellationToken ct = default);

    Task SaveIdempotencyAsync(
        int tenantId,
        int driverId,
        string clientOpId,
        string opType,
        string responseJson,
        int httpStatus,
        CancellationToken ct = default);

    Task<int> InsertVoiceCommandAsync(DriverVoiceCommandInsert row, CancellationToken ct = default);
}
