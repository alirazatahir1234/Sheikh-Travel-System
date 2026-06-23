using FluentValidation.TestHelper;
using SheikhTravelSystem.Application.Features.MaintenanceModule;

namespace SheikhTravelSystem.Tests.Maintenance;

public class MaintenanceRequestCreateValidationTests
{
    private readonly CreateMaintenanceRequestCommandValidator _validator = new();

    private static CreateMaintenanceRequestCommand ValidCommand() =>
        new(new CreateMaintenanceRequestDto(
            VehicleId: 1,
            DriverId: null,
            RequestType: "Corrective",
            Priority: "Medium",
            IssueCategory: "Engine",
            Description: "Engine overheating on highway drive."));

    [Fact]
    public void ValidPayload_PassesValidation()
    {
        var result = _validator.TestValidate(ValidCommand());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void VehicleIdZero_FailsValidation()
    {
        var cmd = ValidCommand() with { Body = ValidCommand().Body with { VehicleId = 0 } };
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.Body.VehicleId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("short")]
    public void ShortOrEmptyDescription_FailsValidation(string description)
    {
        var cmd = ValidCommand() with { Body = ValidCommand().Body with { Description = description } };
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.Body.Description);
    }

    [Fact]
    public void WhitespaceOnlyDescription_FailsValidation()
    {
        var cmd = ValidCommand() with
        {
            Body = ValidCommand().Body with { Description = new string(' ', MaintenanceRequestValidation.DescriptionMinLength) }
        };
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.Body.Description);
    }

    [Fact]
    public void DescriptionTooLong_FailsValidation()
    {
        var cmd = ValidCommand() with
        {
            Body = ValidCommand().Body with { Description = new string('x', MaintenanceRequestValidation.DescriptionMaxLength + 1) }
        };
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.Body.Description);
    }

    [Theory]
    [InlineData("Urgent")]
    [InlineData("")]
    public void InvalidPriority_FailsValidation(string priority)
    {
        var cmd = ValidCommand() with { Body = ValidCommand().Body with { Priority = priority } };
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.Body.Priority);
    }

    [Theory]
    [InlineData("Emergency")]
    [InlineData("")]
    public void InvalidRequestType_FailsValidation(string requestType)
    {
        var cmd = ValidCommand() with { Body = ValidCommand().Body with { RequestType = requestType } };
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.Body.RequestType);
    }

    [Theory]
    [InlineData("InvalidCategory")]
    [InlineData("")]
    public void InvalidIssueCategory_FailsValidation(string category)
    {
        var cmd = ValidCommand() with { Body = ValidCommand().Body with { IssueCategory = category } };
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.Body.IssueCategory);
    }
}
