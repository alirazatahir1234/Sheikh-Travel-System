using FluentAssertions;
using SheikhTravelSystem.Application.Features.Drivers.Commands;
using SheikhTravelSystem.Application.Features.Drivers.DTOs;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Tests.Drivers;

public class CreateDriverCommandValidatorTests
{
    private readonly CreateDriverCommandValidator _validator = new();

    private static CreateDriverCommand ValidCommand() => new(
        new CreateDriverDto(
            FirstName: "Ahmed",
            LastName: "Khan",
            Phone: "03001234567",
            LicenseNumber: "LHR-DL-001",
            LicenseExpiryDate: DateTime.UtcNow.AddYears(2),
            Email: "ahmed.khan@example.com",
            DateOfBirth: DateTime.UtcNow.AddYears(-25),
            CNIC: "35202-1234567-1",
            Address: "Lahore, Pakistan",
            EmergencyContactName: "Sara Khan",
            EmergencyContact: "+92 300 1234567"));

    [Fact]
    public void Validate_ValidCommand_ShouldPass()
    {
        var result = _validator.Validate(ValidCommand());
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyFirstName_ShouldFail(string name)
    {
        var cmd = new CreateDriverCommand(ValidCommand().Driver with { FirstName = name });
        var result = _validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("FirstName"));
    }

    [Fact]
    public void Validate_ExpiredLicense_ShouldFail()
    {
        var cmd = new CreateDriverCommand(ValidCommand().Driver with
        {
            LicenseExpiryDate = DateTime.UtcNow.AddDays(-1)
        });
        var result = _validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("LicenseExpiryDate"));
    }

    [Fact]
    public void Validate_FutureDateOfBirth_ShouldFail()
    {
        var cmd = new CreateDriverCommand(ValidCommand().Driver with
        {
            DateOfBirth = DateTime.UtcNow.AddDays(1)
        });
        var result = _validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName.Contains("DateOfBirth") &&
            e.ErrorMessage.Contains("future", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_UnderMinimumAge_ShouldFail()
    {
        var cmd = new CreateDriverCommand(ValidCommand().Driver with
        {
            DateOfBirth = DateTime.UtcNow.AddYears(-17)
        });
        var result = _validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName.Contains("DateOfBirth") &&
            e.ErrorMessage.Contains("18", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    public void Validate_InvalidEmail_ShouldFail(string email)
    {
        var cmd = new CreateDriverCommand(ValidCommand().Driver with { Email = email });
        var result = _validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("Email"));
    }

    [Fact]
    public void Validate_MissingDateOfBirth_ShouldFail()
    {
        var cmd = new CreateDriverCommand(ValidCommand().Driver with { DateOfBirth = null });
        var result = _validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("DateOfBirth"));
    }

    [Fact]
    public void DriverStatus_OnLeave_ShouldExist()
    {
        Enum.IsDefined(typeof(DriverStatus), DriverStatus.OnLeave).Should().BeTrue();
    }
}
