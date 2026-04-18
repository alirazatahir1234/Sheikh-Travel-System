using FluentAssertions;
using SheikhTravelSystem.Application.Features.Vehicles.Commands;
using SheikhTravelSystem.Application.Features.Vehicles.DTOs;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Tests.Vehicles;

public class CreateVehicleCommandValidatorTests
{
    private readonly CreateVehicleCommandValidator _validator = new();

    private static CreateVehicleCommand ValidCommand() => new(
        new CreateVehicleDto(
            Name: "Coach 01",
            RegistrationNumber: "ABC-1234",
            Model: "Hino",
            Year: 2022,
            SeatingCapacity: 45,
            FuelAverage: 12.5m,
            FuelType: FuelType.Diesel,
            CurrentMileage: 0m,
            InsuranceExpiryDate: DateTime.UtcNow.AddYears(1)));

    [Fact]
    public void Validate_ValidCommand_ShouldPass()
    {
        var result = _validator.Validate(ValidCommand());
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyName_ShouldFail(string name)
    {
        var cmd = new CreateVehicleCommand(ValidCommand().Vehicle with { Name = name });
        var result = _validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("Name"));
    }

    [Fact]
    public void Validate_NameExceeds100Chars_ShouldFail()
    {
        var longName = new string('A', 101);
        var cmd = new CreateVehicleCommand(ValidCommand().Vehicle with { Name = longName });
        var result = _validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("Name"));
    }

    [Fact]
    public void Validate_NameExactly100Chars_ShouldPass()
    {
        var exactName = new string('A', 100);
        var cmd = new CreateVehicleCommand(ValidCommand().Vehicle with { Name = exactName });
        var result = _validator.Validate(cmd);
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyRegistrationNumber_ShouldFail(string reg)
    {
        var cmd = new CreateVehicleCommand(ValidCommand().Vehicle with { RegistrationNumber = reg });
        var result = _validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("RegistrationNumber"));
    }

    [Fact]
    public void Validate_RegistrationNumberExceeds20Chars_ShouldFail()
    {
        var longReg = new string('X', 21);
        var cmd = new CreateVehicleCommand(ValidCommand().Vehicle with { RegistrationNumber = longReg });
        var result = _validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("RegistrationNumber"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-10.5)]
    public void Validate_InvalidFuelAverage_ShouldFail(double fuelAverage)
    {
        var cmd = new CreateVehicleCommand(ValidCommand().Vehicle with { FuelAverage = (decimal)fuelAverage });
        var result = _validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("FuelAverage"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-50)]
    public void Validate_InvalidSeatingCapacity_ShouldFail(int capacity)
    {
        var cmd = new CreateVehicleCommand(ValidCommand().Vehicle with { SeatingCapacity = capacity });
        var result = _validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("SeatingCapacity"));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(20)]
    [InlineData(60)]
    public void Validate_ValidSeatingCapacity_ShouldPass(int capacity)
    {
        var cmd = new CreateVehicleCommand(ValidCommand().Vehicle with { SeatingCapacity = capacity });
        var result = _validator.Validate(cmd);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_MultipleViolations_ShouldReportAll()
    {
        var cmd = new CreateVehicleCommand(
            new CreateVehicleDto("", "", null, null, 0, 0m, FuelType.Petrol, 0m, null));
        var result = _validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
        result.Errors.Count.Should().BeGreaterThanOrEqualTo(4);
    }

    [Fact]
    public void CreateVehicleDto_ShouldHoldAllFields()
    {
        var dto = ValidCommand().Vehicle;
        dto.Name.Should().Be("Coach 01");
        dto.RegistrationNumber.Should().Be("ABC-1234");
        dto.SeatingCapacity.Should().Be(45);
        dto.FuelAverage.Should().Be(12.5m);
        dto.FuelType.Should().Be(FuelType.Diesel);
    }

    [Fact]
    public void VehicleDto_ShouldHoldAllFields()
    {
        var dto = new VehicleDto(
            Id: 1, Name: "Bus A", RegistrationNumber: "LHR-001",
            Model: "Hino", Year: 2020, SeatingCapacity: 50,
            FuelAverage: 10m, FuelType: FuelType.Diesel, CurrentMileage: 5000m,
            InsuranceExpiryDate: DateTime.UtcNow.AddYears(1),
            Status: VehicleStatus.Available, CreatedAt: DateTime.UtcNow);

        dto.Id.Should().Be(1);
        dto.Name.Should().Be("Bus A");
        dto.Status.Should().Be(VehicleStatus.Available);
        dto.FuelType.Should().Be(FuelType.Diesel);
    }
}

public class UpdateVehicleCommandValidatorTests
{
    private readonly UpdateVehicleCommandValidator _validator = new();

    private static UpdateVehicleCommand ValidCommand() => new(
        Id: 1,
        Vehicle: new UpdateVehicleDto(
            Name: "Updated Coach",
            RegistrationNumber: "XYZ-9999",
            Model: "Hino",
            Year: 2023,
            SeatingCapacity: 50,
            FuelAverage: 15m,
            FuelType: FuelType.Petrol,
            CurrentMileage: 1000m,
            InsuranceExpiryDate: DateTime.UtcNow.AddYears(2),
            Status: VehicleStatus.Available));

    [Fact]
    public void Validate_ValidCommand_ShouldPass()
    {
        var result = _validator.Validate(ValidCommand());
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_InvalidId_ShouldFail(int id)
    {
        var cmd = ValidCommand() with { Id = id };
        var result = _validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Id");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyName_ShouldFail(string name)
    {
        var cmd = new UpdateVehicleCommand(1, ValidCommand().Vehicle with { Name = name });
        var result = _validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("Name"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyRegistrationNumber_ShouldFail(string reg)
    {
        var cmd = new UpdateVehicleCommand(1, ValidCommand().Vehicle with { RegistrationNumber = reg });
        var result = _validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("RegistrationNumber"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Validate_InvalidFuelAverage_ShouldFail(double fuel)
    {
        var cmd = new UpdateVehicleCommand(1, ValidCommand().Vehicle with { FuelAverage = (decimal)fuel });
        var result = _validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("FuelAverage"));
    }

    [Fact]
    public void UpdateVehicleCommand_ShouldHoldIdAndDto()
    {
        var cmd = ValidCommand();
        cmd.Id.Should().Be(1);
        cmd.Vehicle.Name.Should().Be("Updated Coach");
    }
}
