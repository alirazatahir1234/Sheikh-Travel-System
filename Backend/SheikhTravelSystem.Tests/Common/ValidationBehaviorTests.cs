using FluentAssertions;
using FluentValidation;
using MediatR;
using Moq;
using SheikhTravelSystem.Application.Common.Behaviors;

namespace SheikhTravelSystem.Tests.Common;

public record TestCommand(string Name) : IRequest<string>;

public class TestCommandValidator : AbstractValidator<TestCommand>
{
    public TestCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Name is required.");
    }
}

public class ValidationBehaviorTests
{
    [Fact]
    public async Task Handle_NoValidators_CallsNext()
    {
        // Arrange
        var validators = Enumerable.Empty<IValidator<TestCommand>>();
        var behavior = new ValidationBehavior<TestCommand, string>(validators);
        var command = new TestCommand("");

        // Act
        var result = await behavior.Handle(command, () => Task.FromResult("ok"), CancellationToken.None);

        // Assert
        result.Should().Be("ok");
    }

    [Fact]
    public async Task Handle_ValidInput_CallsNext()
    {
        // Arrange
        var validators = new[] { new TestCommandValidator() };
        var behavior = new ValidationBehavior<TestCommand, string>(validators);
        var command = new TestCommand("valid name");

        // Act
        var result = await behavior.Handle(command, () => Task.FromResult("ok"), CancellationToken.None);

        // Assert
        result.Should().Be("ok");
    }

    [Fact]
    public async Task Handle_InvalidInput_ThrowsValidationException()
    {
        // Arrange
        var validators = new[] { new TestCommandValidator() };
        var behavior = new ValidationBehavior<TestCommand, string>(validators);
        var command = new TestCommand("");

        // Act
        var act = async () => await behavior.Handle(command, () => Task.FromResult("ok"), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }
}
