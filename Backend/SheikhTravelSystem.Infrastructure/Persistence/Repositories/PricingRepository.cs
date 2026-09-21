using Dapper;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories;

public sealed class PricingRepository(IDbConnectionFactory dbFactory) : IPricingRepository
{
    public async Task<RoutePricingInfo?> GetRoutePricingAsync(
        int routeId,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        var route = await connection.QuerySingleOrDefaultAsync<RoutePricingRow>(
            new CommandDefinition(
                "SELECT Distance, BasePrice FROM Routes WHERE Id = @Id AND IsDeleted = 0 AND IsActive = 1",
                new { Id = routeId },
                cancellationToken: cancellationToken));

        return route is null
            ? null
            : new RoutePricingInfo(route.Distance, route.BasePrice);
    }

    public async Task<decimal?> GetVehicleFuelAverageAsync(
        int vehicleId,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        return await connection.ExecuteScalarAsync<decimal?>(
            new CommandDefinition(
                "SELECT FuelAverage FROM Vehicles WHERE Id = @Id",
                new { Id = vehicleId },
                cancellationToken: cancellationToken));
    }

    private sealed class RoutePricingRow
    {
        public decimal Distance { get; init; }
        public decimal BasePrice { get; init; }
    }
}
