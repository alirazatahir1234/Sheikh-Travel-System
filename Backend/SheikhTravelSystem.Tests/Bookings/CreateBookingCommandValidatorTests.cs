using FluentAssertions;
using FluentValidation;
using SheikhTravelSystem.Application.Features.Bookings.Commands;
using SheikhTravelSystem.Application.Features.Bookings.DTOs;

namespace SheikhTravelSystem.Tests.Bookings;

public class CreateBookingCommandValidatorTests
{
    private readonly CreateBookingCommandValidator _validator = new();

    private static CreateBookingCommand ValidCommand() => new(
        new CreateBookingDto(
            CustomerId: 1,
            RouteId: 2,
            PickupTime: DateTime.UtcNow.AddHours(2),
            PassengerCount: 3,
            TotalAmount: 5000m,
            Notes: null));

    [Fact]
    public void Validate_ValidCommand_ShouldPass()
    {
        var result = _validator.Validate(ValidCommand());
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Validate_InvalidCustomerId_ShouldFail(int customerId)
    {
        var cmd = ValidCommand() with { Booking = ValidCommand().Booking with { CustomerId = customerId } };
        var result = _validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("CustomerId"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Validate_InvalidRouteId_ShouldFail(int routeId)
    {
        var cmd = ValidCommand() with { Booking = ValidCommand().Booking with { RouteId = routeId } };
        var result = _validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("RouteId"));
    }

    [Fact]
    public void Validate_PickupTimeInPast_ShouldFail()
    {
        var cmd = ValidCommand() with { Booking = ValidCommand().Booking with { PickupTime = DateTime.UtcNow.AddHours(-1) } };
        var result = _validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("PickupTime"));
    }

    [Fact]
    public void Validate_PickupTimeNow_ShouldFail()
    {
        var cmd = ValidCommand() with { Booking = ValidCommand().Booking with { PickupTime = DateTime.UtcNow.AddSeconds(-1) } };
        var result = _validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_InvalidPassengerCount_ShouldFail(int passengerCount)
    {
        var cmd = ValidCommand() with { Booking = ValidCommand().Booking with { PassengerCount = passengerCount } };
        var result = _validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("PassengerCount"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-500)]
    public void Validate_InvalidTotalAmount_ShouldFail(double amount)
    {
        var cmd = ValidCommand() with { Booking = ValidCommand().Booking with { TotalAmount = (decimal)amount } };
        var result = _validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("TotalAmount"));
    }

    [Fact]
    public void Validate_AllFieldsValid_ShouldPassWithNoErrors()
    {
        var result = _validator.Validate(ValidCommand());
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_MultipleViolations_ShouldReportAllErrors()
    {
        var cmd = new CreateBookingCommand(
            new CreateBookingDto(0, 0, DateTime.UtcNow.AddDays(-1), 0, 0m, null));
        var result = _validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
        result.Errors.Count.Should().BeGreaterThanOrEqualTo(4);
    }

    [Fact]
    public void CreateBookingDto_ShouldHoldAllFields()
    {
        var pickupTime = DateTime.UtcNow.AddHours(3);
        var dto = new CreateBookingDto(5, 10, pickupTime, 4, 12000m, "Window seat");
        dto.CustomerId.Should().Be(5);
        dto.RouteId.Should().Be(10);
        dto.PickupTime.Should().Be(pickupTime);
        dto.PassengerCount.Should().Be(4);
        dto.TotalAmount.Should().Be(12000m);
        dto.Notes.Should().Be("Window seat");
    }

    [Fact]
    public void CreateBookingCommand_ShouldWrapDto()
    {
        var dto = new CreateBookingDto(1, 1, DateTime.UtcNow.AddHours(1), 2, 3000m, null);
        var command = new CreateBookingCommand(dto);
        command.Booking.Should().Be(dto);
    }
}
