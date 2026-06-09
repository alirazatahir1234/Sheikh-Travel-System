using FluentAssertions;
using SheikhTravelSystem.Application.Features.Tenants;

namespace SheikhTravelSystem.Tests.Tenants;

public class ProvisionTenantCommandValidatorTests
{
    private readonly ProvisionTenantCommandValidator _validator = new();

    private static ProvisionTenantCommand ValidCommand() => new()
    {
        Name = "Test Travel Co",
        Slug = "test-travel",
        AdminFullName = "Jane Admin",
        AdminEmail = "admin@test-travel.example",
        AdminPassword = "password123"
    };

    [Fact]
    public void Validate_ValidCommand_ShouldPass()
    {
        var result = _validator.Validate(ValidCommand());
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("Invalid Slug!")]
    [InlineData("UPPER")]
    public void Validate_InvalidSlug_ShouldFail(string slug)
    {
        var cmd = ValidCommand() with { Slug = slug };
        var result = _validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_InvalidModuleCode_ShouldFail()
    {
        var cmd = ValidCommand() with { ModuleCodes = ["NOT_A_REAL_MODULE"] };
        var result = _validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ProvisionTenantCommand.ModuleCodes));
    }

    [Fact]
    public void Validate_ShortPassword_ShouldFail()
    {
        var cmd = ValidCommand() with { AdminPassword = "short" };
        var result = _validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
    }
}
