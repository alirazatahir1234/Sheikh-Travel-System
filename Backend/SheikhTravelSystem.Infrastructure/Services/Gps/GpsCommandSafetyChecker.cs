using Dapper;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;

namespace SheikhTravelSystem.Infrastructure.Services.Gps;

public sealed class GpsCommandSafetyChecker(IDbConnectionFactory dbFactory) : IGpsCommandSafetyChecker
{
    public async Task<string?> CheckEngineCutoffPreconditionAsync(
        int? vehicleId, CancellationToken cancellationToken)
    {
        if (!vehicleId.HasValue)
            return "Cannot verify vehicle state; refusing engine cut-off for safety.";

        using var connection = dbFactory.CreateConnection();
        var state = await connection.QueryFirstOrDefaultAsync<(decimal? Speed, DateTime? LastUpdate)>(
            new CommandDefinition(
                "SELECT Speed, LastUpdate FROM VehicleCurrentLocation WHERE VehicleId = @VehicleId",
                new { VehicleId = vehicleId.Value },
                cancellationToken: cancellationToken));

        return GpsCommandSafetyRules.EvaluateEngineCutoff(state.LastUpdate, state.Speed);
    }
}
