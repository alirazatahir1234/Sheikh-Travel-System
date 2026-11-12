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

    [Theory]
    [InlineData("Yearly")]
    [InlineData("monthly")]
    [InlineData("Annual")]
    public void Validate_KnownBillingCycle_ShouldPass(string cycle)
    {
        var result = _validator.Validate(ValidCommand() with { BillingCycle = cycle });
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_UnknownBillingCycle_ShouldFail()
    {
        var result = _validator.Validate(ValidCommand() with { BillingCycle = "Weekly" });
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ProvisionTenantCommand.BillingCycle));
    }

    [Fact]
    public void Validate_EndDateNotAfterStartDate_ShouldFail()
    {
        var start = new DateTime(2026, 11, 12);
        var cmd = ValidCommand() with { SubscriptionStartDate = start, SubscriptionEndDate = start };
        var result = _validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ProvisionTenantCommand.SubscriptionEndDate));
    }

    [Fact]
    public void Validate_EndDateAfterStartDate_ShouldPass()
    {
        var start = new DateTime(2026, 11, 12);
        var cmd = ValidCommand() with { SubscriptionStartDate = start, SubscriptionEndDate = start.AddYears(1) };
        _validator.Validate(cmd).IsValid.Should().BeTrue();
    }
}
