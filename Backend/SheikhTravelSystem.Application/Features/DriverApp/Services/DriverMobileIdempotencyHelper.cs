using System.Text.Json;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;

namespace SheikhTravelSystem.Application.Features.DriverApp.Services;

public static class DriverMobileIdempotencyHelper
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static async Task<(bool IsReplay, ApiResponse<T>? Replay)> TryReplayAsync<T>(
        IDriverMobileSyncRepository repository,
        int tenantId,
        string? clientOpId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(clientOpId)) return (false, null);

        var row = await repository.GetIdempotencyAsync(tenantId, clientOpId, ct);
        if (row is null) return (false, null);

        var replay = JsonSerializer.Deserialize<ApiResponse<T>>(row.ResponseJson, JsonOpts);
        return (true, replay);
    }

    public static async Task PersistAsync<T>(
        IDriverMobileSyncRepository repository,
        int tenantId,
        int driverId,
        string? clientOpId,
        string opType,
        ApiResponse<T> response,
        int httpStatus,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(clientOpId)) return;

        var json = JsonSerializer.Serialize(response, JsonOpts);
        await repository.SaveIdempotencyAsync(
            tenantId, driverId, clientOpId, opType, json, httpStatus, ct);
    }
}
