using SheikhTravelSystem.Application.Features.Drivers;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.Trips.SmartAssignment;

/// <summary>
/// Mandatory (hard) filters for Smart Trip Assignment. Failures never reach the scorer ranking list.
/// </summary>
public static class TripAssignmentConstraints
{
    public const string ReasonEligibility = "eligibility";
    public const string ReasonOnTrip = "on_trip";
    public const string ReasonNoVehicle = "no_vehicle";
    public const string ReasonCapacity = "capacity";
    public const string ReasonVehicleType = "vehicle_type";
    public const string ReasonMaintenance = "maintenance";
    public const string ReasonShiftLimit = "shift_limit";
    public const string ReasonScheduleConflict = "schedule_conflict";
    public const string ReasonEvRange = "ev_range";

    /// <summary>
    /// Returns null when the candidate passes all hard filters; otherwise a machine reason key + human detail.
    /// </summary>
    public static TripAssignmentExclusionDto? Evaluate(
        TripAssignmentCandidateRow row,
        TripSmartAssignmentTripContext trip,
        double? tripDistanceKm)
    {
        try
        {
            DriverAssignmentGuard.EnsureAssignableForVehicleTrip(
                row.IsActive,
                (DriverStatus)row.Status,
                row.VerificationStatus,
                row.LicenseExpiryDate);
        }
        catch (Exception ex)
        {
            return Exclude(row, ReasonEligibility, ex.Message);
        }

        if (row.Status == (int)DriverStatus.OnTrip)
            return Exclude(row, ReasonOnTrip, "Driver is already OnTrip — excluded until free.");

        if (row.VehicleId is null or <= 0)
            return Exclude(row, ReasonNoVehicle, "No linked vehicle — confirm path required before suggest.");

        if (row.VehicleStatus is (int)VehicleStatus.Maintenance
            or (int)VehicleStatus.Retired
            or (int)VehicleStatus.Draft)
            return Exclude(row, ReasonMaintenance, $"Vehicle status blocks assignment ({(VehicleStatus)row.VehicleStatus!.Value}).");

        if (row.HasCriticalMaintenance)
            return Exclude(row, ReasonMaintenance, "Open critical maintenance work order on vehicle.");

        if (trip.PassengerCount > 0
            && row.SeatingCapacity is int seats and > 0
            && seats < trip.PassengerCount)
        {
            return Exclude(row, ReasonCapacity,
                $"Seating {seats} < passengers {trip.PassengerCount}.");
        }

        if (!string.IsNullOrWhiteSpace(trip.RequiredVehicleType)
            && !string.IsNullOrWhiteSpace(row.VehicleType)
            && !VehicleTypeMatches(row.VehicleType!, trip.RequiredVehicleType!))
        {
            return Exclude(row, ReasonVehicleType,
                $"Vehicle type '{row.VehicleType}' does not match required '{trip.RequiredVehicleType}'.");
        }

        if (!string.IsNullOrWhiteSpace(trip.RequiredVehicleType)
            && string.IsNullOrWhiteSpace(row.VehicleType))
        {
            return Exclude(row, ReasonVehicleType,
                $"Required vehicle type '{trip.RequiredVehicleType}' but vehicle has no type set.");
        }

        if (row.HoursWorkedToday >= SmartAssignmentPolicy.MaxHoursWorkedHardLimit)
        {
            return Exclude(row, ReasonShiftLimit,
                $"{row.HoursWorkedToday:F1} h worked today ≥ hard limit {SmartAssignmentPolicy.MaxHoursWorkedHardLimit:F0} h.");
        }

        if (row.HasScheduleConflict)
            return Exclude(row, ReasonScheduleConflict, "Driver has an overlapping trip at the planned window.");

        if (!HasSufficientEvRange(row, tripDistanceKm))
        {
            return Exclude(row, ReasonEvRange,
                $"EV estimated range insufficient for trip (buffer {(SmartAssignmentPolicy.EvRangeBufferFactor - 1) * 100:F0}%).");
        }

        return null;
    }

    public static bool HasSufficientEvRange(TripAssignmentCandidateRow row, double? tripDistanceKm)
    {
        if (row.VehicleFuelType != (int)FuelType.Electric || !tripDistanceKm.HasValue)
            return true;

        if (!row.ElectricRangeKm.HasValue || row.ElectricRangeKm <= 0)
            return true;

        var batteryFactor = row.FuelLevelPercent.HasValue
            ? row.FuelLevelPercent.Value / 100m
            : 0.6m;
        var estimatedKm = (double)(row.ElectricRangeKm.Value * batteryFactor);
        return estimatedKm >= tripDistanceKm.Value * SmartAssignmentPolicy.EvRangeBufferFactor;
    }

    private static bool VehicleTypeMatches(string vehicleType, string required)
        => vehicleType.Contains(required, StringComparison.OrdinalIgnoreCase)
           || required.Contains(vehicleType, StringComparison.OrdinalIgnoreCase);

    private static TripAssignmentExclusionDto Exclude(
        TripAssignmentCandidateRow row, string reason, string detail)
        => new(row.DriverId, row.DriverName, row.VehicleId, row.VehicleName, reason, detail);
}
