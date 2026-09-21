namespace SheikhTravelSystem.Application.Common.Interfaces.Repositories;

/// <summary>
/// Persistence helpers for pricing calculations. SQL lives in Infrastructure.
/// </summary>
public interface IPricingRepository
{
    Task<RoutePricingInfo?> GetRoutePricingAsync(int routeId, CancellationToken cancellationToken = default);

    Task<decimal?> GetVehicleFuelAverageAsync(int vehicleId, CancellationToken cancellationToken = default);
}

public sealed record RoutePricingInfo(decimal Distance, decimal BasePrice);
