using FluentAssertions;
using SheikhTravelSystem.Application.Features.Payments.Commands;
using SheikhTravelSystem.Application.Features.Payments.DTOs;

namespace SheikhTravelSystem.Tests.Payments;

public class CreatePaymentCommandValidatorTests
{
    private readonly CreatePaymentCommandValidator _validator = new();

    [Fact]
    public void Validate_ValidInput_ShouldPass()
    {
        var dto = new CreatePaymentDto(1, 5000m, "Cash", null, null, null);
        var command = new CreatePaymentCommand(dto);
        var result = _validator.Validate(command);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ZeroBookingId_ShouldFail()
    {
        var dto = new CreatePaymentDto(0, 5000m, "Cash", null, null, null);
        var command = new CreatePaymentCommand(dto);
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_ZeroAmount_ShouldFail()
    {
        var dto = new CreatePaymentDto(1, 0m, "Cash", null, null, null);
        var command = new CreatePaymentCommand(dto);
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_NegativeAmount_ShouldFail()
    {
        var dto = new CreatePaymentDto(1, -100m, "Cash", null, null, null);
        var command = new CreatePaymentCommand(dto);
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_EmptyPaymentMethod_ShouldFail()
    {
        var dto = new CreatePaymentDto(1, 5000m, "", null, null, null);
        var command = new CreatePaymentCommand(dto);
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
    }
}
