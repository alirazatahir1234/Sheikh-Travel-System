using FluentAssertions;
using Moq;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.WhatsApp;
using SheikhTravelSystem.Application.Features.WhatsApp.Commands;
using SheikhTravelSystem.Application.Features.WhatsApp.DTOs;

namespace SheikhTravelSystem.Tests.WhatsApp;

public class WhatsAppTemplateSendTests
{
    private static readonly WhatsAppAccountRow Uae = new(
        1, 1, "UAE", "SheikhGo UAE", "+971557701219", "Sales", "pn-uae", null, true, true);

    private static WhatsAppConversationDto OpenWindowConversation()
        => new(
            Id: 55,
            AccountId: 1,
            AccountCode: "UAE",
            ContactId: 55,
            ContactPhone: "+971501234567",
            ContactName: "Test",
            CustomerId: null,
            Status: "Open",
            LastMessageAt: DateTime.UtcNow,
            UnreadCount: 0,
            LastMessagePreview: "hi",
            LastIncomingMessageAt: DateTime.UtcNow.AddHours(-1),
            IsWithinMessagingWindow: true);

    private static WhatsAppConversationDto ClosedWindowConversation()
        => OpenWindowConversation() with
        {
            LastIncomingMessageAt = DateTime.UtcNow.AddHours(-25),
            IsWithinMessagingWindow = false
        };

    [Fact]
    public async Task TextOutbound_OutsideWindow_FailsWithoutCloudCall()
    {
        var repo = new Mock<IWhatsAppInboxRepository>();
        var cloud = new Mock<IWhatsAppCloudApiService>(MockBehavior.Strict);
        var config = new Mock<IWhatsAppAccountConfig>();
        var resolver = new Mock<IWhatsAppAccountResolver>();
        var tenant = new Mock<ITenantContext>();
        tenant.Setup(t => t.GetRequiredTenantId()).Returns(1);
        repo.Setup(r => r.GetConversationAsync(1, 55, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ClosedWindowConversation());
        repo.Setup(r => r.GetAccountByIdAsync(1, 1, It.IsAny<CancellationToken>())).ReturnsAsync(Uae);
        config.Setup(c => c.ResolveCredentials(Uae))
            .Returns(new WhatsAppAccountCredentials("pn-uae", "token", null));

        var handler = new SendWhatsAppOutboundCommandHandler(
            repo.Object, cloud.Object, config.Object, resolver.Object, tenant.Object,
            Mock.Of<ICurrentUserService>(), Mock.Of<IWhatsAppRealtimePublisher>());

        var result = await handler.Handle(
            new SendWhatsAppOutboundCommand(null, 55, null, "text", "Hello"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Template required");
        cloud.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task TemplateSend_Draft_Rejected()
    {
        var repo = new Mock<IWhatsAppInboxRepository>();
        var templates = new Mock<IWhatsAppTemplateRepository>();
        var cloud = new Mock<IWhatsAppCloudApiService>(MockBehavior.Strict);
        var config = new Mock<IWhatsAppAccountConfig>();
        var resolver = new Mock<IWhatsAppAccountResolver>();
        var tenant = new Mock<ITenantContext>();
        tenant.Setup(t => t.GetRequiredTenantId()).Returns(1);
        repo.Setup(r => r.GetConversationAsync(1, 55, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ClosedWindowConversation());
        repo.Setup(r => r.GetAccountByIdAsync(1, 1, It.IsAny<CancellationToken>())).ReturnsAsync(Uae);
        config.Setup(c => c.ResolveCredentials(Uae))
            .Returns(new WhatsAppAccountCredentials("pn-uae", "token", null));
        templates.Setup(t => t.GetByNameAsync(1, 1, "support_update", "en", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WhatsAppTemplateDto(1, 1, "UAE", "support_update", "en", "UTILITY", "Draft", null, null));

        var handler = new SendWhatsAppTemplateCommandHandler(
            repo.Object, templates.Object, cloud.Object, config.Object, resolver.Object,
            tenant.Object, Mock.Of<ICurrentUserService>(), Mock.Of<IWhatsAppRealtimePublisher>());

        var result = await handler.Handle(
            new SendWhatsAppTemplateCommand(55, null, null, "support_update", "en"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("not Approved");
        cloud.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task TemplateSend_Approved_SucceedsOutsideWindow()
    {
        var repo = new Mock<IWhatsAppInboxRepository>();
        var templates = new Mock<IWhatsAppTemplateRepository>();
        var cloud = new Mock<IWhatsAppCloudApiService>();
        var config = new Mock<IWhatsAppAccountConfig>();
        var resolver = new Mock<IWhatsAppAccountResolver>();
        var tenant = new Mock<ITenantContext>();
        var realtime = new Mock<IWhatsAppRealtimePublisher>();
        tenant.Setup(t => t.GetRequiredTenantId()).Returns(1);
        repo.Setup(r => r.GetConversationAsync(1, 55, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ClosedWindowConversation());
        repo.Setup(r => r.GetAccountByIdAsync(1, 1, It.IsAny<CancellationToken>())).ReturnsAsync(Uae);
        config.Setup(c => c.ResolveCredentials(Uae))
            .Returns(new WhatsAppAccountCredentials("pn-uae", "token", null));
        templates.Setup(t => t.GetByNameAsync(1, 1, "support_update", "en", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WhatsAppTemplateDto(1, 1, "UAE", "support_update", "en", "UTILITY", "Approved", null, null));
        repo.Setup(r => r.InsertOutboundMessageAsync(
                1, 55, null, "template", It.IsAny<string?>(), "Queued", It.IsAny<CancellationToken>(), "support_update"))
            .ReturnsAsync(200);
        repo.Setup(r => r.SetOutboundStatusAsync(200, "Sending", null, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        cloud.Setup(c => c.SendTemplateAsync(Uae, "971501234567", "support_update", "en", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(WhatsAppCloudApiResult.Ok("wamid.T"));
        repo.Setup(r => r.SetOutboundMetaIdAsync(200, "wamid.T", "Sent", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new SendWhatsAppTemplateCommandHandler(
            repo.Object, templates.Object, cloud.Object, config.Object, resolver.Object,
            tenant.Object, Mock.Of<ICurrentUserService>(), realtime.Object);

        var result = await handler.Handle(
            new SendWhatsAppTemplateCommand(55, null, null, "support_update", "en"), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Data!.MetaMessageId.Should().Be("wamid.T");
        cloud.Verify(c => c.SendTemplateAsync(
            Uae, "971501234567", "support_update", "en", null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TextOutbound_InsideWindow_Succeeds()
    {
        var repo = new Mock<IWhatsAppInboxRepository>();
        var cloud = new Mock<IWhatsAppCloudApiService>();
        var config = new Mock<IWhatsAppAccountConfig>();
        var resolver = new Mock<IWhatsAppAccountResolver>();
        var tenant = new Mock<ITenantContext>();
        tenant.Setup(t => t.GetRequiredTenantId()).Returns(1);
        repo.Setup(r => r.GetConversationAsync(1, 55, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OpenWindowConversation());
        repo.Setup(r => r.GetAccountByIdAsync(1, 1, It.IsAny<CancellationToken>())).ReturnsAsync(Uae);
        config.Setup(c => c.ResolveCredentials(Uae))
            .Returns(new WhatsAppAccountCredentials("pn-uae", "token", null));
        repo.Setup(r => r.InsertOutboundMessageAsync(
                1, 55, null, "text", "Hi", "Queued", It.IsAny<CancellationToken>(), null))
            .ReturnsAsync(100);
        repo.Setup(r => r.SetOutboundStatusAsync(100, "Sending", null, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        cloud.Setup(c => c.SendTextAsync(Uae, "971501234567", "Hi", It.IsAny<CancellationToken>()))
            .ReturnsAsync(WhatsAppCloudApiResult.Ok("wamid.OK"));
        repo.Setup(r => r.SetOutboundMetaIdAsync(100, "wamid.OK", "Sent", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new SendWhatsAppOutboundCommandHandler(
            repo.Object, cloud.Object, config.Object, resolver.Object, tenant.Object,
            Mock.Of<ICurrentUserService>(), Mock.Of<IWhatsAppRealtimePublisher>());

        var result = await handler.Handle(
            new SendWhatsAppOutboundCommand(null, 55, null, "text", "Hi"), CancellationToken.None);

        result.Success.Should().BeTrue();
    }
}
