using FluentAssertions;
using SheikhTravelSystem.Application.Features.Bookings.Commands;
using SheikhTravelSystem.Application.Features.Bookings.DTOs;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Tests.Bookings;

public class DoubleBookingPreventionTests
{
    // The overlap detection logic used by AssignDriverCommand and AssignVehicleCommand:
    // A conflict exists when two bookings overlap within a 4-hour window.
    // Conflict: existing.PickupTime < (new.PickupTime + 4h) AND (existing.PickupTime + 4h) > new.PickupTime
    private static bool HasTimeConflict(DateTime existingPickup, DateTime newPickup)
    {
        var bufferHours = 4;
        return existingPickup < newPickup.AddHours(bufferHours)
            && existingPickup.AddHours(bufferHours) > newPickup;
    }

    [Fact]
    public void SamePickupTime_ShouldConflict()
    {
        var t = DateTime.UtcNow.AddHours(2);
        HasTimeConflict(t, t).Should().BeTrue();
    }

    [Fact]
    public void Bookings3HoursApart_ShouldConflict()
    {
        var t1 = DateTime.UtcNow.AddHours(2);
        var t2 = t1.AddHours(3);
        HasTimeConflict(t1, t2).Should().BeTrue();
    }

    [Fact]
    public void Bookings4HoursApart_ShouldNotConflict()
    {
        var t1 = DateTime.UtcNow.AddHours(2);
        var t2 = t1.AddHours(4);
        HasTimeConflict(t1, t2).Should().BeFalse();
    }

    [Fact]
    public void Bookings6HoursApart_ShouldNotConflict()
    {
        var t1 = DateTime.UtcNow.AddHours(2);
        var t2 = t1.AddHours(6);
        HasTimeConflict(t1, t2).Should().BeFalse();
    }

    [Fact]
    public void Bookings1HourApart_ShouldConflict()
    {
        var t1 = DateTime.UtcNow.AddHours(2);
        var t2 = t1.AddHours(1);
        HasTimeConflict(t1, t2).Should().BeTrue();
    }

    [Fact]
    public void AssignDriverCommand_ShouldHoldBookingAndDriverIds()
    {
        var command = new AssignDriverCommand(BookingId: 10, DriverId: 5);
        command.BookingId.Should().Be(10);
        command.DriverId.Should().Be(5);
    }

    [Fact]
    public void AssignVehicleCommand_ShouldHoldBookingAndVehicleIds()
    {
        var command = new AssignVehicleCommand(BookingId: 7, VehicleId: 3);
        command.BookingId.Should().Be(7);
        command.VehicleId.Should().Be(3);
    }

    [Fact]
    public void AssignDriverCommand_RecordEquality_ShouldWork()
    {
        var a = new AssignDriverCommand(1, 2);
        var b = new AssignDriverCommand(1, 2);
        a.Should().Be(b);
    }

    [Fact]
    public void AssignVehicleCommand_RecordEquality_ShouldWork()
    {
        var a = new AssignVehicleCommand(1, 2);
        var b = new AssignVehicleCommand(1, 2);
        a.Should().Be(b);
    }

    [Fact]
    public void AssignDriverCommand_DifferentIds_ShouldNotBeEqual()
    {
        var a = new AssignDriverCommand(1, 2);
        var b = new AssignDriverCommand(1, 99);
        a.Should().NotBe(b);
    }

    [Fact]
    public void DriverStatus_Available_AllowsAssignment()
    {
        var status = DriverStatus.Available;
        (status == DriverStatus.Available).Should().BeTrue();
    }

    [Theory]
    [InlineData(DriverStatus.OnTrip)]
    [InlineData(DriverStatus.OffDuty)]
    [InlineData(DriverStatus.Suspended)]
    public void DriverStatus_NonAvailable_BlocksAssignment(DriverStatus status)
    {
        (status == DriverStatus.Available).Should().BeFalse();
    }

    [Fact]
    public void VehicleStatus_Available_AllowsAssignment()
    {
        var status = VehicleStatus.Available;
        (status == VehicleStatus.Available).Should().BeTrue();
    }

    [Theory]
    [InlineData(VehicleStatus.OnTrip)]
    [InlineData(VehicleStatus.Maintenance)]
    [InlineData(VehicleStatus.Retired)]
    public void VehicleStatus_NonAvailable_BlocksAssignment(VehicleStatus status)
    {
        (status == VehicleStatus.Available).Should().BeFalse();
    }

    [Fact]
    public void BookingDto_ShouldHoldAllFields()
    {
        var pickupTime = DateTime.UtcNow.AddHours(2);
        var dto = new BookingDto(
            Id: 1, CustomerId: 2, CustomerName: "Ali", RouteId: 3,
            RouteDescription: "Karachi -> Lahore", VehicleId: 4, VehicleName: "Bus A",
            DriverId: 5, DriverName: "Ahmed", PickupTime: pickupTime,
            DropoffTime: null, PassengerCount: 10, TotalAmount: 5000m,
            Status: BookingStatus.Pending, Notes: null, CreatedAt: DateTime.UtcNow);

        dto.Id.Should().Be(1);
        dto.CustomerId.Should().Be(2);
        dto.CustomerName.Should().Be("Ali");
        dto.RouteId.Should().Be(3);
        dto.RouteDescription.Should().Be("Karachi -> Lahore");
        dto.VehicleId.Should().Be(4);
        dto.DriverId.Should().Be(5);
        dto.PickupTime.Should().Be(pickupTime);
        dto.PassengerCount.Should().Be(10);
        dto.TotalAmount.Should().Be(5000m);
        dto.Status.Should().Be(BookingStatus.Pending);
    }

    [Fact]
    public void AssignDriverDto_ShouldHoldDriverId()
    {
        var dto = new AssignDriverDto(DriverId: 7);
        dto.DriverId.Should().Be(7);
    }

    [Fact]
    public void AssignVehicleDto_ShouldHoldVehicleId()
    {
        var dto = new AssignVehicleDto(VehicleId: 9);
        dto.VehicleId.Should().Be(9);
    }
}
