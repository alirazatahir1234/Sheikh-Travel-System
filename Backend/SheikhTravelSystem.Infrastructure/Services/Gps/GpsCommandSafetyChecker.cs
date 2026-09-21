using Dapper;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;

namespace SheikhTravelSystem.Infrastructure.Services.Gps;

public sealed class GpsCommandSafetyChecker(IDbConnectionFactory dbFactory) : IGpsCommandSafetyChecker
{
    private const decimal EngineCutoffMaxSpeedKmh = 20m;

    public async Task<string?> CheckEngineCutoffPreconditionAsync(
        int? vehicleId, CancellationToken cancellationToken)
    {
        if (!vehicleId.HasValue)
            return "Cannot verify vehicle state; refusing engine cut-off for safety.";

        using var connection = dbFactory.CreateConnection();
        var state = await connection.QueryFirstOrDefaultAsync<(bool? Ignition, decimal? Speed, DateTime? LastUpdate)>(
            new CommandDefinition(
                "SELECT Ignition, Speed, LastUpdate FROM VehicleCurrentLocation WHERE VehicleId = @VehicleId",
                new { VehicleId = vehicleId.Value },
                cancellationToken: cancellationToken));

        if (state.LastUpdate is null)
            return "Cannot verify vehicle state; refusing engine cut-off for safety.";

        if (state.Ignition == false)
            return "Engine is already off.";

        if (state.Speed.HasValue && state.Speed.Value >= EngineCutoffMaxSpeedKmh)
            return $"Vehicle is moving too fast (>= {EngineCutoffMaxSpeedKmh} km/h) for a safe engine cut-off.";

        return null;
    }
}
