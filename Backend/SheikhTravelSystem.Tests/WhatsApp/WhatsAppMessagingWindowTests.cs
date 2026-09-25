using FluentAssertions;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.WhatsApp;
using SheikhTravelSystem.Application.Features.WhatsApp.DTOs;

namespace SheikhTravelSystem.Tests.WhatsApp;

public class WhatsAppMessagingWindowTests
{
    [Fact]
    public void NullInbound_IsClosed()
        => WhatsAppMessagingWindow.IsOpen(null, DateTime.UtcNow).Should().BeFalse();

    [Fact]
    public void JustUnder24Hours_IsOpen()
    {
        var now = DateTime.UtcNow;
        WhatsAppMessagingWindow.IsOpen(now.AddHours(-23).AddMinutes(-59), now).Should().BeTrue();
    }

    [Fact]
    public void JustOver24Hours_IsClosed()
    {
        var now = DateTime.UtcNow;
        WhatsAppMessagingWindow.IsOpen(now.AddHours(-24).AddMinutes(-1), now).Should().BeFalse();
    }

    [Fact]
    public void Exactly24Hours_IsOpen()
    {
        var now = DateTime.UtcNow;
        WhatsAppMessagingWindow.IsOpen(now.AddHours(-24), now).Should().BeTrue();
    }

    [Fact]
    public void WindowSeconds_Is86400()
        => WhatsAppMessagingWindow.WindowSeconds.Should().Be(86400);

    [Fact]
    public void WithWindowFlag_SetsDto()
    {
        var now = DateTime.UtcNow;
        var dto = new WhatsAppConversationDto
        {
            Id = 1,
            AccountId = 1,
            AccountCode = "UAE",
            ContactId = 1,
            ContactPhone = "+97150",
            ContactName = "Ali",
            CustomerId = null,
            Status = "Open",
            LastMessageAt = now,
            UnreadCount = 0,
            LastMessagePreview = "hi",
            LastIncomingMessageAt = now.AddHours(-1)
        };
        WhatsAppMessagingWindow.WithWindowFlag(dto, now).IsWithinMessagingWindow.Should().BeTrue();
    }
}

public class WhatsAppPermissionsAndTemplateStatusTests
{
    [Fact]
    public void Permissions_IncludeManageAccountsAndTemplates()
    {
        WhatsAppPermissions.All.Should().Contain(WhatsAppPermissions.ManageAccounts);
        WhatsAppPermissions.All.Should().Contain(WhatsAppPermissions.ManageTemplates);
        WhatsAppPermissions.All.Should().Contain(WhatsAppPermissions.AiAssist);
        WhatsAppPermissions.All.Should().NotContain("WhatsApp.Send");
    }

    [Fact]
    public void Draft_IsRecognized_ButApproved_IsDistinct()
    {
        WhatsAppTemplateStatuses.All.Should().Contain(WhatsAppTemplateStatuses.Draft);
        WhatsAppTemplateStatuses.All.Should().Contain(WhatsAppTemplateStatuses.Approved);
        WhatsAppTemplateStatuses.Draft.Should().NotBe(WhatsAppTemplateStatuses.Approved);
    }
}
