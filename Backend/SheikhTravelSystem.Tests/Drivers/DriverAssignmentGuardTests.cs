using FluentAssertions;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Features.Drivers;
using SheikhTravelSystem.Application.Features.Drivers.Commands;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Tests.Drivers;

public class DriverAssignmentGuardTests
{
    private static readonly DateTime ValidLicense = DateTime.UtcNow.AddYears(1);

    [Fact]
    public void EnsureAssignable_ValidDriver_DoesNotThrow()
    {
        var act = () => DriverAssignmentGuard.EnsureAssignable(
            isActive: true,
            status: DriverStatus.Available,
            verificationStatus: "Verified",
            licenseExpiry: ValidLicense);

        act.Should().NotThrow();
    }

    [Fact]
    public void EnsureAssignable_InactiveDriver_ThrowsConflict()
    {
        var act = () => DriverAssignmentGuard.EnsureAssignable(
            isActive: false,
            status: DriverStatus.Available,
            verificationStatus: "Verified",
            licenseExpiry: ValidLicense);

        act.Should().Throw<ConflictException>()
            .WithMessage("*inactive*");
    }

    [Theory]
    [InlineData(DriverStatus.Suspended)]
    [InlineData(DriverStatus.OnLeave)]
    public void EnsureAssignable_BlockedStatus_ThrowsConflict(DriverStatus status)
    {
        var act = () => DriverAssignmentGuard.EnsureAssignable(
            isActive: true,
            status: status,
            verificationStatus: "Verified",
            licenseExpiry: ValidLicense);

        act.Should().Throw<ConflictException>();
    }

    [Fact]
    public void EnsureAssignable_UnverifiedDriver_ThrowsConflict()
    {
        var act = () => DriverAssignmentGuard.EnsureAssignable(
            isActive: true,
            status: DriverStatus.Available,
            verificationStatus: "Pending",
            licenseExpiry: ValidLicense);

        act.Should().Throw<ConflictException>()
            .WithMessage("*verified*");
    }

    [Fact]
    public void EnsureAssignable_ExpiredLicense_ThrowsConflict()
    {
        var act = () => DriverAssignmentGuard.EnsureAssignable(
            isActive: true,
            status: DriverStatus.Available,
            verificationStatus: "Verified",
            licenseExpiry: DateTime.UtcNow.AddDays(-1));

        act.Should().Throw<ConflictException>()
            .WithMessage("*expired*");
    }

    [Fact]
    public void EnsureManualStatusAllowed_OnTrip_ThrowsConflict()
    {
        var act = () => DriverAssignmentGuard.EnsureManualStatusAllowed(DriverStatus.OnTrip);

        act.Should().Throw<ConflictException>()
            .WithMessage("*automatically*");
    }

    [Theory]
    [InlineData(DriverStatus.Available)]
    [InlineData(DriverStatus.OffDuty)]
    [InlineData(DriverStatus.OnLeave)]
    public void EnsureManualStatusAllowed_AllowedStatuses_DoesNotThrow(DriverStatus status)
    {
        var act = () => DriverAssignmentGuard.EnsureManualStatusAllowed(status);

        act.Should().NotThrow();
    }
}

public class ChangeDriverStatusCommandValidatorTests
{
    private readonly ChangeDriverStatusCommandValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_ShouldPass()
    {
        var result = _validator.Validate(new ChangeDriverStatusCommand(1, DriverStatus.Available));
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Validate_InvalidId_ShouldFail(int id)
    {
        var result = _validator.Validate(new ChangeDriverStatusCommand(id, DriverStatus.Available));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Id");
    }

    [Fact]
    public void Validate_InvalidEnum_ShouldFail()
    {
        var result = _validator.Validate(new ChangeDriverStatusCommand(1, (DriverStatus)999));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Status");
    }
}

public class UpdateDocumentStatusCommandValidatorTests
{
    private readonly UpdateDocumentStatusCommandValidator _validator = new();

    [Fact]
    public void Validate_ApprovedWithoutReason_ShouldPass()
    {
        var result = _validator.Validate(new UpdateDocumentStatusCommand(1, 10, "Approved"));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_RejectedWithoutReason_ShouldFail()
    {
        var result = _validator.Validate(new UpdateDocumentStatusCommand(1, 10, "Rejected"));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "RejectionReason");
    }

    [Fact]
    public void Validate_RejectedWithReason_ShouldPass()
    {
        var result = _validator.Validate(new UpdateDocumentStatusCommand(1, 10, "Rejected", "Blurry image"));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_InvalidStatus_ShouldFail()
    {
        var result = _validator.Validate(new UpdateDocumentStatusCommand(1, 10, "Pending"));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Status");
    }
}
