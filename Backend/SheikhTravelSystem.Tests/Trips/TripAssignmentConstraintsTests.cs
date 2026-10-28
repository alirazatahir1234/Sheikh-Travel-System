using FluentAssertions;
using SheikhTravelSystem.Application.Features.Trips.SmartAssignment;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Tests.Trips;

public class TripAssignmentConstraintsTests
{
    private static TripSmartAssignmentTripContext Trip(int passengers = 4, string? requiredType = null)
        => new(
            TripId: 1,
            TripNumber: "TR-1",
            Status: TripStatus.Scheduled,
            PlannedStart: DateTime.UtcNow.AddHours(2),
            PickupLatitude: 31.52,
            PickupLongitude: 74.35,
            DestinationLatitude: 31.55,
            DestinationLongitude: 74.40,
            RouteId: null,
            CurrentDriverId: null,
            CurrentVehicleId: null,
            PassengerCount: passengers,
            RequiredVehicleType: requiredType);

    private static TripAssignmentCandidateRow Row(
        int status = (int)DriverStatus.Available,
        int? vehicleId = 10,
        int seating = 6,
        string? vehicleType = "Sedan",
        int vehicleStatus = (int)VehicleStatus.Available,
        bool criticalMaint = false,
        bool conflict = false,
        decimal hours = 3m,
        int? fuelType = null,
        decimal? electricRange = null,
        decimal? fuelPct = null,
        DateTime? licenseExpiry = null)
        => new(
            DriverId: 1,
            DriverName: "Test Driver",
            UserId: 9,
            VehicleId: vehicleId,
            VehicleName: "V1",
            Latitude: 31.53,
            Longitude: 74.36,
            FuelLevelPercent: fuelPct,
            HoursWorkedToday: hours,
            Rating: 4.5m,
            IsActive: true,
            Status: status,
            VerificationStatus: "Verified",
            LicenseExpiryDate: licenseExpiry ?? DateTime.UtcNow.Date.AddYears(1),
            VehicleFuelType: fuelType,
            ElectricRangeKm: electricRange,
            SeatingCapacity: seating,
            VehicleType: vehicleType,
            VehicleStatus: vehicleStatus,
            HasCriticalMaintenance: criticalMaint,
            HasScheduleConflict: conflict);

    [Fact]
    public void Evaluate_passes_eligible_available_driver_with_vehicle()
    {
        TripAssignmentConstraints.Evaluate(Row(), Trip(), tripDistanceKm: 20).Should().BeNull();
    }

    [Fact]
    public void Evaluate_excludes_on_trip_drivers()
    {
        var ex = TripAssignmentConstraints.Evaluate(Row(status: (int)DriverStatus.OnTrip), Trip(), null);
        ex!.Reason.Should().Be(TripAssignmentConstraints.ReasonOnTrip);
    }

    [Fact]
    public void Evaluate_excludes_no_linked_vehicle()
    {
        var ex = TripAssignmentConstraints.Evaluate(Row(vehicleId: null), Trip(), null);
        ex!.Reason.Should().Be(TripAssignmentConstraints.ReasonNoVehicle);
    }

    [Fact]
    public void Evaluate_excludes_capacity_shortfall()
    {
        var ex = TripAssignmentConstraints.Evaluate(Row(seating: 3), Trip(passengers: 5), null);
        ex!.Reason.Should().Be(TripAssignmentConstraints.ReasonCapacity);
    }

    [Fact]
    public void Evaluate_excludes_critical_maintenance()
    {
        var ex = TripAssignmentConstraints.Evaluate(Row(criticalMaint: true), Trip(), null);
        ex!.Reason.Should().Be(TripAssignmentConstraints.ReasonMaintenance);
    }

    [Fact]
    public void Evaluate_excludes_vehicle_in_maintenance_status()
    {
        var ex = TripAssignmentConstraints.Evaluate(
            Row(vehicleStatus: (int)VehicleStatus.Maintenance), Trip(), null);
        ex!.Reason.Should().Be(TripAssignmentConstraints.ReasonMaintenance);
    }

    [Fact]
    public void Evaluate_excludes_hard_shift_limit()
    {
        var ex = TripAssignmentConstraints.Evaluate(
            Row(hours: SmartAssignmentPolicy.MaxHoursWorkedHardLimit), Trip(), null);
        ex!.Reason.Should().Be(TripAssignmentConstraints.ReasonShiftLimit);
    }

    [Fact]
    public void Evaluate_excludes_schedule_conflict()
    {
        var ex = TripAssignmentConstraints.Evaluate(Row(conflict: true), Trip(), null);
        ex!.Reason.Should().Be(TripAssignmentConstraints.ReasonScheduleConflict);
    }

    [Fact]
    public void Evaluate_excludes_vehicle_type_mismatch_when_required()
    {
        var ex = TripAssignmentConstraints.Evaluate(
            Row(vehicleType: "Sedan"), Trip(requiredType: "Bus"), null);
        ex!.Reason.Should().Be(TripAssignmentConstraints.ReasonVehicleType);
    }

    [Fact]
    public void Evaluate_excludes_insufficient_ev_range()
    {
        var ex = TripAssignmentConstraints.Evaluate(
            Row(fuelType: (int)FuelType.Electric, electricRange: 50m, fuelPct: 50m),
            Trip(),
            tripDistanceKm: 40);
        // 50 * 0.5 = 25 km estimated < 40 * 1.15
        ex!.Reason.Should().Be(TripAssignmentConstraints.ReasonEvRange);
    }

    [Fact]
    public void Policy_defaults_are_suggest_only_without_create_auto()
    {
        SmartAssignmentPolicy.CreateTimeAutoAssignEnabled.Should().BeFalse();
        SmartAssignmentPolicy.AutoApplyEnabled.Should().BeFalse();
        SmartAssignmentPolicy.ActiveMode.Should().Be(SmartAssignmentPolicy.ModeSuggestOnly);
        SmartAssignmentPolicy.EvRangeBufferFactor.Should().Be(1.15);
    }
}
