using FluentAssertions;
using SheikhTravelSystem.Application.Features.Tracking.Commands;
using SheikhTravelSystem.Application.Features.Tracking.DTOs;

namespace SheikhTravelSystem.Tests.Tracking;

public class UpdateLocationCommandValidatorTests
{
    private readonly UpdateLocationCommandValidator _validator = new();

    private static UpdateLocationCommand ValidCommand() => new(
        new UpdateLocationDto(
            VehicleId: 1,
            DriverId: 2,
            BookingId: 3,
            Latitude: 33.6844,
            Longitude: 73.0479,
            Speed: 60m));

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
    public void Validate_InvalidVehicleId_ShouldFail(int vehicleId)
    {
        var cmd = new UpdateLocationCommand(ValidCommand().Location with { VehicleId = vehicleId });
        var result = _validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("VehicleId"));
    }

    [Theory]
    [InlineData(-91)]
    [InlineData(-180)]
    [InlineData(91)]
    [InlineData(200)]
    public void Validate_LatitudeOutOfRange_ShouldFail(double latitude)
    {
        var cmd = new UpdateLocationCommand(ValidCommand().Location with { Latitude = latitude });
        var result = _validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("Latitude"));
    }

    [Theory]
    [InlineData(-90)]
    [InlineData(0)]
    [InlineData(90)]
    [InlineData(45.5)]
    [InlineData(-45.5)]
    public void Validate_LatitudeInRange_ShouldPass(double latitude)
    {
        var cmd = new UpdateLocationCommand(ValidCommand().Location with { Latitude = latitude });
        var result = _validator.Validate(cmd);
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(-181)]
    [InlineData(-360)]
    [InlineData(181)]
    [InlineData(360)]
    public void Validate_LongitudeOutOfRange_ShouldFail(double longitude)
    {
        var cmd = new UpdateLocationCommand(ValidCommand().Location with { Longitude = longitude });
        var result = _validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("Longitude"));
    }

    [Theory]
    [InlineData(-180)]
    [InlineData(0)]
    [InlineData(180)]
    [InlineData(73.04)]
    [InlineData(-73.04)]
    public void Validate_LongitudeInRange_ShouldPass(double longitude)
    {
        var cmd = new UpdateLocationCommand(ValidCommand().Location with { Longitude = longitude });
        var result = _validator.Validate(cmd);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_BoundaryLatitude_NegativeNinety_ShouldPass()
    {
        var cmd = new UpdateLocationCommand(ValidCommand().Location with { Latitude = -90 });
        _validator.Validate(cmd).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_BoundaryLatitude_PositiveNinety_ShouldPass()
    {
        var cmd = new UpdateLocationCommand(ValidCommand().Location with { Latitude = 90 });
        _validator.Validate(cmd).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_BoundaryLongitude_NegativeOneEighty_ShouldPass()
    {
        var cmd = new UpdateLocationCommand(ValidCommand().Location with { Longitude = -180 });
        _validator.Validate(cmd).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_BoundaryLongitude_PositiveOneEighty_ShouldPass()
    {
        var cmd = new UpdateLocationCommand(ValidCommand().Location with { Longitude = 180 });
        _validator.Validate(cmd).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_AllFieldsInvalid_ShouldReportMultipleErrors()
    {
        var cmd = new UpdateLocationCommand(
            new UpdateLocationDto(0, null, null, 200, 400, 0m));
        var result = _validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
        result.Errors.Count.Should().BeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public void UpdateLocationDto_ShouldHoldAllFields()
    {
        var dto = new UpdateLocationDto(
            VehicleId: 5, DriverId: 10, BookingId: 20,
            Latitude: 33.6844, Longitude: 73.0479, Speed: 80m);

        dto.VehicleId.Should().Be(5);
        dto.DriverId.Should().Be(10);
        dto.BookingId.Should().Be(20);
        dto.Latitude.Should().BeApproximately(33.6844, 0.0001);
        dto.Longitude.Should().BeApproximately(73.0479, 0.0001);
        dto.Speed.Should().Be(80m);
    }

    [Fact]
    public void UpdateLocationDto_OptionalFields_CanBeNull()
    {
        var dto = new UpdateLocationDto(1, null, null, 0, 0, 0m);
        dto.DriverId.Should().BeNull();
        dto.BookingId.Should().BeNull();
    }

    [Fact]
    public void TrackingDto_ShouldHoldAllFields()
    {
        var timestamp = DateTime.UtcNow;
        var dto = new TrackingDto(
            Id: 1, VehicleId: 2, DriverId: 3, BookingId: 4,
            Latitude: 33.68, Longitude: 73.04, Speed: 60m, Timestamp: timestamp);

        dto.Id.Should().Be(1);
        dto.VehicleId.Should().Be(2);
        dto.DriverId.Should().Be(3);
        dto.BookingId.Should().Be(4);
        dto.Latitude.Should().BeApproximately(33.68, 0.01);
        dto.Longitude.Should().BeApproximately(73.04, 0.01);
        dto.Speed.Should().Be(60m);
        dto.Timestamp.Should().Be(timestamp);
    }

    [Fact]
    public void UpdateLocationCommand_ShouldWrapDto()
    {
        var dto = ValidCommand().Location;
        var cmd = new UpdateLocationCommand(dto);
        cmd.Location.Should().Be(dto);
    }

    [Fact]
    public void TrackingDto_RecordEquality_SameValues_ShouldBeEqual()
    {
        var ts = DateTime.UtcNow;
        var a = new TrackingDto(1, 1, null, null, 33.0, 73.0, 50m, ts);
        var b = new TrackingDto(1, 1, null, null, 33.0, 73.0, 50m, ts);
        a.Should().Be(b);
    }
}
