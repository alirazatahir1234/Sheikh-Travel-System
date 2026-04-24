using SheikhTravelSystem.Domain.Common;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Domain.Entities;

/// <summary>
/// A business-configurable rule that decides how a driver's allowance is computed
/// for a booking. Rules are evaluated in ascending priority order and the first
/// matching rule wins. Switching between strategies never requires a code change.
/// </summary>
public class DriverAllowanceRule : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public AllowanceCalculationType CalculationType { get; set; }

    /// <summary>
    /// Interpretation depends on <see cref="CalculationType"/>:
    ///   FixedAmount   → PKR
    ///   PerKm         → PKR / km
    ///   PerDay        → PKR / day
    ///   ProfitPercent → percent (0–100)
    /// </summary>
    public decimal Value { get; set; }

    /// <summary>Lower number = higher priority. Ties are broken by Id asc.</summary>
    public int Priority { get; set; }

    public decimal? MinDistanceKm { get; set; }
    public decimal? MaxDistanceKm { get; set; }

    /// <summary>Optional filter (matches <c>Vehicles.FuelType</c>).</summary>
    public FuelType? VehicleFuelType { get; set; }

    /// <summary>Optional free-text route filter (matches Source/Destination/Name contains).</summary>
    public string? RouteFilter { get; set; }

    public bool IsActive { get; set; } = true;

    public string? Notes { get; set; }
}
