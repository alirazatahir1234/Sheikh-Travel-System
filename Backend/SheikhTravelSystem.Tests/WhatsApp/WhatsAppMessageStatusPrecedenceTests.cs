using FluentAssertions;
using SheikhTravelSystem.Application.Features.WhatsApp;

namespace SheikhTravelSystem.Tests.WhatsApp;

public class WhatsAppMessageStatusPrecedenceTests
{
    [Theory]
    [InlineData(null, "sent", true)]
    [InlineData("Queued", "sent", true)]
    [InlineData("Sent", "delivered", true)]
    [InlineData("Delivered", "read", true)]
    [InlineData("Read", "delivered", false)]
    [InlineData("Delivered", "sent", false)]
    [InlineData("Sent", "queued", false)]
    [InlineData("Failed", "delivered", false)]
    [InlineData("Failed", "failed", false)]
    [InlineData("Sent", "failed", true)]
    [InlineData("Delivered", "failed", true)]
    public void ShouldApply_ForwardOnly(string? current, string incoming, bool expected)
        => WhatsAppMessageStatusPrecedence.ShouldApply(current, incoming).Should().Be(expected);

    [Fact]
    public void ComputeExpiresAt_Adds24Hours()
    {
        var last = new DateTime(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);
        WhatsAppMessagingWindow.ComputeExpiresAt(last).Should().Be(last.AddHours(24));
    }

    [Fact]
    public void IsOpenFromStoredWindow_UsesPersistedExpiry()
    {
        var now = DateTime.UtcNow;
        WhatsAppMessagingWindow
            .IsOpenFromStoredWindow(null, now.AddMinutes(5), now)
            .Should().BeTrue();
        WhatsAppMessagingWindow
            .IsOpenFromStoredWindow(now, now.AddMinutes(-1), now)
            .Should().BeFalse();
    }
}
