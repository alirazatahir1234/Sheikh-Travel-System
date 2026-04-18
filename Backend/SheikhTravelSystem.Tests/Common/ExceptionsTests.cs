using FluentAssertions;
using SheikhTravelSystem.Application.Common.Exceptions;

namespace SheikhTravelSystem.Tests.Common;

public class ExceptionsTests
{
    [Fact]
    public void NotFoundException_WithNameAndIntKey_ShouldFormatMessage()
    {
        var ex = new NotFoundException("Booking", 42);
        ex.Message.Should().Be("Booking with key '42' was not found.");
    }

    [Fact]
    public void NotFoundException_WithNameAndStringKey_ShouldFormatMessage()
    {
        var ex = new NotFoundException("User", "admin@test.com");
        ex.Message.Should().Be("User with key 'admin@test.com' was not found.");
    }

    [Fact]
    public void NotFoundException_WithVehicleName_ShouldFormatMessage()
    {
        var ex = new NotFoundException("Vehicle", 7);
        ex.Message.Should().Be("Vehicle with key '7' was not found.");
    }

    [Fact]
    public void NotFoundException_WithDriverName_ShouldFormatMessage()
    {
        var ex = new NotFoundException("Driver", 15);
        ex.Message.Should().Be("Driver with key '15' was not found.");
    }

    [Fact]
    public void NotFoundException_WithRouteName_ShouldFormatMessage()
    {
        var ex = new NotFoundException("Route", 3);
        ex.Message.Should().Be("Route with key '3' was not found.");
    }

    [Fact]
    public void NotFoundException_ShouldInheritFromException()
    {
        var ex = new NotFoundException("Entity", 1);
        ex.Should().BeAssignableTo<Exception>();
    }

    [Fact]
    public void ConflictException_ShouldUseProvidedMessage()
    {
        var ex = new ConflictException("Vehicle with registration 'ABC-123' already exists.");
        ex.Message.Should().Be("Vehicle with registration 'ABC-123' already exists.");
    }

    [Fact]
    public void ConflictException_DriverLicenseMessage_ShouldBePreserved()
    {
        var ex = new ConflictException("Driver with license 'LIC-001' already exists.");
        ex.Message.Should().Contain("LIC-001");
    }

    [Fact]
    public void ConflictException_ShouldInheritFromException()
    {
        var ex = new ConflictException("conflict");
        ex.Should().BeAssignableTo<Exception>();
    }

    [Fact]
    public void ForbiddenException_DefaultMessage_ShouldBePermissionDenial()
    {
        var ex = new ForbiddenException();
        ex.Message.Should().Be("You do not have permission to perform this action.");
    }

    [Fact]
    public void ForbiddenException_CustomMessage_ShouldUseProvidedMessage()
    {
        var ex = new ForbiddenException("Admins only.");
        ex.Message.Should().Be("Admins only.");
    }

    [Fact]
    public void ForbiddenException_ShouldInheritFromException()
    {
        var ex = new ForbiddenException();
        ex.Should().BeAssignableTo<Exception>();
    }

    [Fact]
    public void NotFoundException_CanBeCaughtAsException()
    {
        Action act = () => throw new NotFoundException("Customer", 99);
        act.Should().Throw<NotFoundException>()
           .WithMessage("Customer with key '99' was not found.");
    }

    [Fact]
    public void ConflictException_CanBeCaughtAsException()
    {
        Action act = () => throw new ConflictException("Already exists.");
        act.Should().Throw<ConflictException>()
           .WithMessage("Already exists.");
    }

    [Fact]
    public void ForbiddenException_CanBeCaughtAsException()
    {
        Action act = () => throw new ForbiddenException();
        act.Should().Throw<ForbiddenException>()
           .WithMessage("You do not have permission to perform this action.");
    }

    [Fact]
    public void NotFoundException_IsNotConflictException()
    {
        var ex = new NotFoundException("X", 1);
        ex.Should().NotBeAssignableTo<ConflictException>();
    }

    [Fact]
    public void NotFoundException_MessageContainsNameAndKey()
    {
        var ex = new NotFoundException("Payment", 777);
        ex.Message.Should().Contain("Payment");
        ex.Message.Should().Contain("777");
    }
}
