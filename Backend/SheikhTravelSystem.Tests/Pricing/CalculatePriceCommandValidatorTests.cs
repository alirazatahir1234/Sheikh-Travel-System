using FluentAssertions;
using SheikhTravelSystem.Application.Features.Pricing.Commands;
using SheikhTravelSystem.Application.Features.Pricing.DTOs;

namespace SheikhTravelSystem.Tests.Pricing;

public class CalculatePriceCommandValidatorTests
{
    private readonly CalculatePriceCommandValidator _validator = new();

    [Fact]
    public void Validate_ValidInput_ShouldPass()
    {
        var request = new CalculatePriceRequest(1, 1, 300m, 5000m, 2000m, 3000m);
        var command = new CalculatePriceCommand(request);
        var result = _validator.Validate(command);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ZeroRouteId_ShouldFail()
    {
        var request = new CalculatePriceRequest(0, 1, 300m, 5000m, 2000m, 3000m);
        var command = new CalculatePriceCommand(request);
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_ZeroVehicleId_ShouldFail()
    {
        var request = new CalculatePriceRequest(1, 0, 300m, 5000m, 2000m, 3000m);
        var command = new CalculatePriceCommand(request);
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_NegativeFuelPrice_ShouldFail()
    {
        var request = new CalculatePriceRequest(1, 1, -100m, 5000m, 2000m, 3000m);
        var command = new CalculatePriceCommand(request);
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_NegativeDriverCost_ShouldFail()
    {
        var request = new CalculatePriceRequest(1, 1, 300m, -1m, 2000m, 3000m);
        var command = new CalculatePriceCommand(request);
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_ZeroDriverCost_ShouldPass()
    {
        var request = new CalculatePriceRequest(1, 1, 300m, 0m, 2000m, 3000m);
        var command = new CalculatePriceCommand(request);
        var result = _validator.Validate(command);
        result.IsValid.Should().BeTrue();
    }
}
