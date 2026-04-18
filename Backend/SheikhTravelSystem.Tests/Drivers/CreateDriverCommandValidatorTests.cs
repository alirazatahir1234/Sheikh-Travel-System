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
            FullName: "Ahmed Khan",
            Phone: "03001234567",
            LicenseNumber: "LHR-DL-001",
            LicenseExpiryDate: DateTime.UtcNow.AddYears(2),
            CNIC: "35202-1234567-1",
            Address: "Lahore, Pakistan"));

    [Fact]
    public void Validate_ValidCommand_ShouldPass()
    {
        var result = _validator.Validate(ValidCommand());
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyFullName_ShouldFail(string name)
    {
        var cmd = new CreateDriverCommand(ValidCommand().Driver with { FullName = name });
        var result = _validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("FullName"));
    }

    [Fact]
    public void Validate_FullNameExceeds100Chars_ShouldFail()
    {
        var longName = new string('A', 101);
        var cmd = new CreateDriverCommand(ValidCommand().Driver with { FullName = longName });
        var result = _validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("FullName"));
    }

    [Fact]
    public void Validate_FullNameExactly100Chars_ShouldPass()
    {
        var exactName = new string('A', 100);
        var cmd = new CreateDriverCommand(ValidCommand().Driver with { FullName = exactName });
        var result = _validator.Validate(cmd);
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyPhone_ShouldFail(string phone)
    {
        var cmd = new CreateDriverCommand(ValidCommand().Driver with { Phone = phone });
        var result = _validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("Phone"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyLicenseNumber_ShouldFail(string license)
    {
        var cmd = new CreateDriverCommand(ValidCommand().Driver with { LicenseNumber = license });
        var result = _validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("LicenseNumber"));
    }

    [Fact]
    public void Validate_LicenseNumberExceeds30Chars_ShouldFail()
    {
        var longLicense = new string('L', 31);
        var cmd = new CreateDriverCommand(ValidCommand().Driver with { LicenseNumber = longLicense });
        var result = _validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("LicenseNumber"));
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
    public void Validate_LicenseExpiryInFuture_ShouldPass()
    {
        var cmd = new CreateDriverCommand(ValidCommand().Driver with
        {
            LicenseExpiryDate = DateTime.UtcNow.AddMonths(6)
        });
        var result = _validator.Validate(cmd);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_MultipleViolations_ShouldReportAll()
    {
        var cmd = new CreateDriverCommand(
            new CreateDriverDto("", "", "", DateTime.UtcNow.AddDays(-1), null, null));
        var result = _validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
        result.Errors.Count.Should().BeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public void CreateDriverDto_ShouldHoldAllFields()
    {
        var expiry = DateTime.UtcNow.AddYears(2);
        var dto = new CreateDriverDto("Ali Raza", "03001234567", "DL-001", expiry, "35202-xxx", "Lahore");
        dto.FullName.Should().Be("Ali Raza");
        dto.Phone.Should().Be("03001234567");
        dto.LicenseNumber.Should().Be("DL-001");
        dto.LicenseExpiryDate.Should().Be(expiry);
        dto.CNIC.Should().Be("35202-xxx");
        dto.Address.Should().Be("Lahore");
    }

    [Fact]
    public void DriverDto_ShouldHoldAllFields()
    {
        var createdAt = DateTime.UtcNow;
        var dto = new DriverDto(
            Id: 1, FullName: "Ahmed", Phone: "0300", LicenseNumber: "DL-001",
            LicenseExpiryDate: DateTime.UtcNow.AddYears(2),
            CNIC: null, Address: null, Status: DriverStatus.Available,
            IsActive: true, CreatedAt: createdAt);

        dto.Id.Should().Be(1);
        dto.FullName.Should().Be("Ahmed");
        dto.Status.Should().Be(DriverStatus.Available);
        dto.IsActive.Should().BeTrue();
    }

    [Fact]
    public void DriverStatus_AllEnumValues_ShouldExist()
    {
        Enum.IsDefined(typeof(DriverStatus), DriverStatus.Available).Should().BeTrue();
        Enum.IsDefined(typeof(DriverStatus), DriverStatus.OnTrip).Should().BeTrue();
        Enum.IsDefined(typeof(DriverStatus), DriverStatus.OffDuty).Should().BeTrue();
        Enum.IsDefined(typeof(DriverStatus), DriverStatus.Suspended).Should().BeTrue();
    }

    [Fact]
    public void CreateDriverCommand_ShouldWrapDto()
    {
        var dto = ValidCommand().Driver;
        var cmd = new CreateDriverCommand(dto);
        cmd.Driver.Should().Be(dto);
    }

    [Fact]
    public void Validate_OptionalFieldsNull_ShouldPass()
    {
        var cmd = new CreateDriverCommand(
            ValidCommand().Driver with { CNIC = null, Address = null });
        var result = _validator.Validate(cmd);
        result.IsValid.Should().BeTrue();
    }
}
