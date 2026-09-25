using FluentAssertions;
using Moq;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.WhatsApp;
using SheikhTravelSystem.Application.Features.WhatsApp.Commands;
using SheikhTravelSystem.Application.Features.WhatsApp.DTOs;

namespace SheikhTravelSystem.Tests.WhatsApp;

public class WhatsAppRetryAndWindowClosedTests
{
    private static readonly WhatsAppAccountRow Pk = new(
        2, 1, "PK", "SheikhGo Pakistan", "+923177368305", "Support", "pn-pk", null, true, true);

    [Fact]
    public async Task Retry_WhenAttemptCountAtMax_Fails()
    {
        var repo = new Mock<IWhatsAppInboxRepository>();
        var ext = new Mock<IWhatsAppInboxExtended>();
        var tenant = new Mock<ITenantContext>();
        tenant.Setup(t => t.GetRequiredTenantId()).Returns(1);
        ext.Setup(r => r.GetOutboundMessageForRetryAsync(1, 99, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WhatsAppOutboundMessageRow(
                99, 55, 2, "wamid.old", "text", "Hello", null, "Failed", 3, "err"));

        var handler = new RetryWhatsAppMessageCommandHandler(
            repo.Object,
            ext.Object,
            Mock.Of<IWhatsAppCloudApiService>(MockBehavior.Strict),
            Mock.Of<IWhatsAppAccountConfig>(),
            tenant.Object,
            Mock.Of<IWhatsAppRealtimePublisher>());

        var result = await handler.Handle(new RetryWhatsAppMessageCommand(99), CancellationToken.None);
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Retry limit");
    }

    [Fact]
    public async Task Retry_FailedText_InsideWindow_SendsAgain()
    {
        var repo = new Mock<IWhatsAppInboxRepository>();
        var ext = new Mock<IWhatsAppInboxExtended>();
        var cloud = new Mock<IWhatsAppCloudApiService>();
        var config = new Mock<IWhatsAppAccountConfig>();
        var tenant = new Mock<ITenantContext>();
        var realtime = new Mock<IWhatsAppRealtimePublisher>();
        tenant.Setup(t => t.GetRequiredTenantId()).Returns(1);

        ext.Setup(r => r.GetOutboundMessageForRetryAsync(1, 99, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WhatsAppOutboundMessageRow(
                99, 55, 2, "wamid.old", "text", "Hello", null, "Failed", 1, "err"));
        repo.Setup(r => r.GetConversationAsync(1, 55, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WhatsAppConversationDto
            {
                Id = 55,
                AccountId = 2,
                AccountCode = "PK",
                ContactId = 55,
                ContactPhone = "+923001112233",
                Status = "Open",
                LastIncomingMessageAt = DateTime.UtcNow.AddHours(-1),
                WindowExpiresAt = DateTime.UtcNow.AddHours(23)
            });
        repo.Setup(r => r.GetAccountByIdAsync(1, 2, It.IsAny<CancellationToken>())).ReturnsAsync(Pk);
        config.Setup(c => c.ResolveCredentials(Pk))
            .Returns(new WhatsAppAccountCredentials("pn-pk", "token", null));
        cloud.Setup(c => c.SendTextAsync(Pk, It.IsAny<string>(), "Hello", It.IsAny<CancellationToken>()))
            .ReturnsAsync(WhatsAppCloudApiResult.Ok("wamid.new"));
        ext.Setup(r => r.IncrementMessageAttemptAsync(1, 99, "wamid.new", "Sent", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new RetryWhatsAppMessageCommandHandler(
            repo.Object, ext.Object, cloud.Object, config.Object, tenant.Object, realtime.Object);

        var result = await handler.Handle(new RetryWhatsAppMessageCommand(99), CancellationToken.None);
        result.Success.Should().BeTrue();
        result.Data!.MetaMessageId.Should().Be("wamid.new");
    }

    [Fact]
    public async Task Retry_OutsideWindow_ReturnsWindowClosed()
    {
        var repo = new Mock<IWhatsAppInboxRepository>();
        var ext = new Mock<IWhatsAppInboxExtended>();
        var cloud = new Mock<IWhatsAppCloudApiService>(MockBehavior.Strict);
        var config = new Mock<IWhatsAppAccountConfig>();
        var tenant = new Mock<ITenantContext>();
        tenant.Setup(t => t.GetRequiredTenantId()).Returns(1);

        ext.Setup(r => r.GetOutboundMessageForRetryAsync(1, 99, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WhatsAppOutboundMessageRow(
                99, 55, 2, null, "text", "Hello", null, "Failed", 1, "err"));
        repo.Setup(r => r.GetConversationAsync(1, 55, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WhatsAppConversationDto
            {
                Id = 55,
                AccountId = 2,
                AccountCode = "PK",
                ContactId = 55,
                ContactPhone = "+923001112233",
                Status = "Open",
                LastIncomingMessageAt = DateTime.UtcNow.AddHours(-30),
                WindowExpiresAt = DateTime.UtcNow.AddHours(-6)
            });
        repo.Setup(r => r.GetAccountByIdAsync(1, 2, It.IsAny<CancellationToken>())).ReturnsAsync(Pk);
        config.Setup(c => c.ResolveCredentials(Pk))
            .Returns(new WhatsAppAccountCredentials("pn-pk", "token", null));

        var handler = new RetryWhatsAppMessageCommandHandler(
            repo.Object, ext.Object, cloud.Object, config.Object, tenant.Object, Mock.Of<IWhatsAppRealtimePublisher>());

        var result = await handler.Handle(new RetryWhatsAppMessageCommand(99), CancellationToken.None);
        result.Success.Should().BeFalse();
        result.Code.Should().Be("WINDOW_CLOSED");
        cloud.VerifyNoOtherCalls();
    }
}
