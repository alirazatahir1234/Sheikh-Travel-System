using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Traccar;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

/// <summary>
/// Maps device-reported OBD / CAN attributes into ingest DTO fields. No synthetic values.
/// </summary>
public static class GpsObdTelemetryMapper
{
    public static IngestPositionDto ApplyTraccarObd(IngestPositionDto dto, TraccarPositionAttributes attributes)
    {
        var patch = FromTraccarAttributes(attributes);
        if (patch.FuelConsumptionRateLph is null && patch.ObdFuelUsedLiters is null && patch.ObdOdometerKm is null)
            return dto;

        return dto with
        {
            FuelConsumptionRateLph = patch.FuelConsumptionRateLph ?? dto.FuelConsumptionRateLph,
            ObdFuelUsedLiters = patch.ObdFuelUsedLiters ?? dto.ObdFuelUsedLiters,
            ObdOdometerKm = patch.ObdOdometerKm ?? dto.ObdOdometerKm,
            FuelLevel = dto.FuelLevel ?? attributes.Fuel
        };
    }

    public static (decimal? FuelConsumptionRateLph, decimal? ObdFuelUsedLiters, decimal? ObdOdometerKm)
        FromTraccarAttributes(TraccarPositionAttributes attributes)
    {
        decimal? consumption = attributes.FuelConsumption;
        decimal? fuelUsed = attributes.FuelUsed ?? attributes.SpentFuel;
        decimal? odometerKm = attributes.ObdOdometer;
        if (odometerKm is > 100_000)
        {
            // Traccar often reports obdOdometer in meters.
            odometerKm = Math.Round(odometerKm.Value / 1000m, 2);
        }

        return (consumption, fuelUsed, odometerKm);
    }
}
