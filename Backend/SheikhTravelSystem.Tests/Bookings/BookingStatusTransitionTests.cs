using FluentAssertions;
using SheikhTravelSystem.Application.Features.Bookings.Commands;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Tests.Bookings;

public class BookingStatusTransitionTests
{
    // Mirrors the transition logic from UpdateBookingStatusCommandHandler
    private static bool IsValidTransition(BookingStatus current, BookingStatus next) =>
        (current, next) switch
        {
            (BookingStatus.Pending, BookingStatus.Confirmed)   => true,
            (BookingStatus.Pending, BookingStatus.Cancelled)   => true,
            (BookingStatus.Confirmed, BookingStatus.Started)   => true,
            (BookingStatus.Confirmed, BookingStatus.Cancelled) => true,
            (BookingStatus.Started, BookingStatus.Completed)   => true,
            _                                                   => false
        };

    [Theory]
    [InlineData(BookingStatus.Pending,   BookingStatus.Confirmed)]
    [InlineData(BookingStatus.Pending,   BookingStatus.Cancelled)]
    [InlineData(BookingStatus.Confirmed, BookingStatus.Started)]
    [InlineData(BookingStatus.Confirmed, BookingStatus.Cancelled)]
    [InlineData(BookingStatus.Started,   BookingStatus.Completed)]
    public void ValidTransitions_ShouldBeAllowed(BookingStatus from, BookingStatus to)
    {
        IsValidTransition(from, to).Should().BeTrue();
    }

    [Theory]
    [InlineData(BookingStatus.Pending,   BookingStatus.Started)]
    [InlineData(BookingStatus.Pending,   BookingStatus.Completed)]
    [InlineData(BookingStatus.Confirmed, BookingStatus.Pending)]
    [InlineData(BookingStatus.Confirmed, BookingStatus.Completed)]
    [InlineData(BookingStatus.Started,   BookingStatus.Pending)]
    [InlineData(BookingStatus.Started,   BookingStatus.Confirmed)]
    [InlineData(BookingStatus.Started,   BookingStatus.Cancelled)]
    [InlineData(BookingStatus.Completed, BookingStatus.Confirmed)]
    [InlineData(BookingStatus.Completed, BookingStatus.Started)]
    [InlineData(BookingStatus.Cancelled, BookingStatus.Pending)]
    [InlineData(BookingStatus.Cancelled, BookingStatus.Confirmed)]
    public void InvalidTransitions_ShouldBeRejected(BookingStatus from, BookingStatus to)
    {
        IsValidTransition(from, to).Should().BeFalse();
    }

    [Fact]
    public void SameStatusTransition_ShouldBeRejected()
    {
        IsValidTransition(BookingStatus.Pending, BookingStatus.Pending).Should().BeFalse();
        IsValidTransition(BookingStatus.Confirmed, BookingStatus.Confirmed).Should().BeFalse();
        IsValidTransition(BookingStatus.Started, BookingStatus.Started).Should().BeFalse();
    }

    [Fact]
    public void CompletedBooking_CannotTransitionToAnyStatus()
    {
        foreach (var status in Enum.GetValues<BookingStatus>())
        {
            IsValidTransition(BookingStatus.Completed, status).Should().BeFalse();
        }
    }

    [Fact]
    public void CancelledBooking_CannotTransitionToAnyStatus()
    {
        foreach (var status in Enum.GetValues<BookingStatus>())
        {
            IsValidTransition(BookingStatus.Cancelled, status).Should().BeFalse();
        }
    }

    [Fact]
    public void UpdateBookingStatusCommand_ShouldHoldIdAndStatus()
    {
        var command = new UpdateBookingStatusCommand(42, BookingStatus.Confirmed);
        command.Id.Should().Be(42);
        command.Status.Should().Be(BookingStatus.Confirmed);
    }

    [Fact]
    public void UpdateBookingStatusCommandValidator_ValidInput_ShouldPass()
    {
        var validator = new UpdateBookingStatusCommandValidator();
        var command = new UpdateBookingStatusCommand(1, BookingStatus.Confirmed);
        var result = validator.Validate(command);
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void UpdateBookingStatusCommandValidator_InvalidId_ShouldFail(int id)
    {
        var validator = new UpdateBookingStatusCommandValidator();
        var command = new UpdateBookingStatusCommand(id, BookingStatus.Confirmed);
        var result = validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Id");
    }

    [Fact]
    public void UpdateBookingStatusCommandValidator_InvalidEnumValue_ShouldFail()
    {
        var validator = new UpdateBookingStatusCommandValidator();
        var command = new UpdateBookingStatusCommand(1, (BookingStatus)999);
        var result = validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Status");
    }
}
