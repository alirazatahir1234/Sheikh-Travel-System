using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.WhatsApp;
using SheikhTravelSystem.Application.Features.WhatsApp.Commands;
using SheikhTravelSystem.Application.Features.WhatsApp.DTOs;
using SheikhTravelSystem.Infrastructure.Services.WhatsApp;

namespace SheikhTravelSystem.Tests.WhatsApp;

public class WhatsAppWebhookVerifierTests
{
    [Fact]
    public void TryGetChallenge_Succeeds_WhenTokenMatches()
    {
        var ok = WhatsAppWebhookVerifier.TryGetChallenge(
            "subscribe", "dev-verify", "12345", "dev-verify", out var challenge);

        ok.Should().BeTrue();
        challenge.Should().Be("12345");
    }

    [Fact]
    public void TryGetChallenge_Rejects_WhenTokenMismatch()
    {
        var ok = WhatsAppWebhookVerifier.TryGetChallenge(
            "subscribe", "wrong", "12345", "dev-verify", out var challenge);

        ok.Should().BeFalse();
        challenge.Should().BeNull();
    }

    [Fact]
    public void TryGetChallenge_Rejects_WhenModeNotSubscribe()
    {
        WhatsAppWebhookVerifier.TryGetChallenge("unsubscribe", "dev-verify", "1", "dev-verify", out _)
            .Should().BeFalse();
    }
}

public class WhatsAppWebhookIngestTests
{
    private static readonly WhatsAppAccountRow Account = new(
        1, 1, "UAE", "SheikhGo UAE", "+971557701219", "Sales", "phone-uae", null, true);

    [Fact]
    public async Task ValidIncomingMessage_UpsertsOnce()
    {
        var repo = new Mock<IWhatsAppInboxRepository>(MockBehavior.Strict);
        var config = new Mock<IWhatsAppAccountConfig>(MockBehavior.Strict);
        var realtime = new Mock<IWhatsAppRealtimePublisher>();

        repo.Setup(r => r.ListActiveAccountRowsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Account });
        config.Setup(c => c.ResolveAccountByPhoneNumberId("phone-uae", It.IsAny<IReadOnlyList<WhatsAppAccountRow>>()))
            .Returns(Account);
        repo.Setup(r => r.MessageExistsAsync("wamid.1", It.IsAny<CancellationToken>())).ReturnsAsync(false);
        repo.Setup(r => r.UpsertInboundMessageAsync(It.IsAny<WhatsAppInboundPersistRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(42);
        repo.Setup(r => r.GetConversationsAsync(1, 1, It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int?>(), 1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Array.Empty<WhatsAppConversationDto>(), 0));

        var handler = CreateHandler(repo.Object, config.Object, realtime.Object);
        var payload = """
            {"entry":[{"changes":[{"value":{
              "metadata":{"phone_number_id":"phone-uae"},
              "contacts":[{"wa_id":"971501234567","profile":{"name":"Ali"}}],
              "messages":[{"id":"wamid.1","from":"971501234567","type":"text","text":{"body":"Hi"}}]
            }}]}]}
            """;

        var result = await handler.Handle(new IngestWhatsAppWebhookCommand(payload), CancellationToken.None);

        result.Success.Should().BeTrue();
        repo.Verify(r => r.UpsertInboundMessageAsync(
            It.Is<WhatsAppInboundPersistRequest>(p => p.MessageId == "wamid.1" && p.Type == "text" && p.Body == "Hi"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DuplicateMessage_DoesNotUpsert()
    {
        var repo = new Mock<IWhatsAppInboxRepository>(MockBehavior.Strict);
        var config = new Mock<IWhatsAppAccountConfig>(MockBehavior.Strict);
        var realtime = new Mock<IWhatsAppRealtimePublisher>();

        repo.Setup(r => r.ListActiveAccountRowsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Account });
        config.Setup(c => c.ResolveAccountByPhoneNumberId("phone-uae", It.IsAny<IReadOnlyList<WhatsAppAccountRow>>()))
            .Returns(Account);
        repo.Setup(r => r.MessageExistsAsync("wamid.dup", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var handler = CreateHandler(repo.Object, config.Object, realtime.Object);
        var payload = """
            {"entry":[{"changes":[{"value":{
              "metadata":{"phone_number_id":"phone-uae"},
              "messages":[{"id":"wamid.dup","from":"971501234567","type":"text","text":{"body":"Hi"}}]
            }}]}]}
            """;

        var result = await handler.Handle(new IngestWhatsAppWebhookCommand(payload), CancellationToken.None);

        result.Success.Should().BeTrue();
        repo.Verify(r => r.UpsertInboundMessageAsync(It.IsAny<WhatsAppInboundPersistRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UnknownMessageType_StillPersists()
    {
        var repo = new Mock<IWhatsAppInboxRepository>(MockBehavior.Strict);
        var config = new Mock<IWhatsAppAccountConfig>(MockBehavior.Strict);
        var realtime = new Mock<IWhatsAppRealtimePublisher>();

        repo.Setup(r => r.ListActiveAccountRowsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Account });
        config.Setup(c => c.ResolveAccountByPhoneNumberId("phone-uae", It.IsAny<IReadOnlyList<WhatsAppAccountRow>>()))
            .Returns(Account);
        repo.Setup(r => r.MessageExistsAsync("wamid.int", It.IsAny<CancellationToken>())).ReturnsAsync(false);
        repo.Setup(r => r.UpsertInboundMessageAsync(It.IsAny<WhatsAppInboundPersistRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(7);
        repo.Setup(r => r.GetConversationsAsync(1, 1, It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int?>(), 1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Array.Empty<WhatsAppConversationDto>(), 0));

        var handler = CreateHandler(repo.Object, config.Object, realtime.Object);
        var payload = """
            {"entry":[{"changes":[{"value":{
              "metadata":{"phone_number_id":"phone-uae"},
              "messages":[{"id":"wamid.int","from":"971501234567","type":"interactive","interactive":{"type":"button_reply"}}]
            }}]}]}
            """;

        var result = await handler.Handle(new IngestWhatsAppWebhookCommand(payload), CancellationToken.None);

        result.Success.Should().BeTrue();
        repo.Verify(r => r.UpsertInboundMessageAsync(
            It.Is<WhatsAppInboundPersistRequest>(p => p.Type == "interactive" && p.Body == "[interactive]"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StatusUpdate_Delivered_Updates()
    {
        var repo = new Mock<IWhatsAppInboxRepository>(MockBehavior.Strict);
        var config = new Mock<IWhatsAppAccountConfig>(MockBehavior.Strict);
        var realtime = new Mock<IWhatsAppRealtimePublisher>();

        repo.Setup(r => r.ListActiveAccountRowsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Account });
        config.Setup(c => c.ResolveAccountByPhoneNumberId("phone-uae", It.IsAny<IReadOnlyList<WhatsAppAccountRow>>()))
            .Returns(Account);
        repo.Setup(r => r.UpdateMessageStatusAsync("wamid.out", "delivered", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WhatsAppMessageStatusUpdate(1, 10, 5, "wamid.out", "Delivered"));

        var handler = CreateHandler(repo.Object, config.Object, realtime.Object);
        var payload = """
            {"entry":[{"changes":[{"value":{
              "metadata":{"phone_number_id":"phone-uae"},
              "statuses":[{"id":"wamid.out","status":"delivered"}]
            }}]}]}
            """;

        var result = await handler.Handle(new IngestWhatsAppWebhookCommand(payload), CancellationToken.None);

        result.Success.Should().BeTrue();
        repo.Verify(r => r.UpdateMessageStatusAsync("wamid.out", "delivered", null, It.IsAny<CancellationToken>()), Times.Once);
        realtime.Verify(r => r.PublishAsync(1, It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StatusUpdate_UnknownStatus_DoesNotUpdate()
    {
        var repo = new Mock<IWhatsAppInboxRepository>(MockBehavior.Strict);
        var config = new Mock<IWhatsAppAccountConfig>(MockBehavior.Strict);
        var realtime = new Mock<IWhatsAppRealtimePublisher>();

        repo.Setup(r => r.ListActiveAccountRowsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Account });
        config.Setup(c => c.ResolveAccountByPhoneNumberId("phone-uae", It.IsAny<IReadOnlyList<WhatsAppAccountRow>>()))
            .Returns(Account);

        var handler = CreateHandler(repo.Object, config.Object, realtime.Object);
        var payload = """
            {"entry":[{"changes":[{"value":{
              "metadata":{"phone_number_id":"phone-uae"},
              "statuses":[{"id":"wamid.out","status":"deleted"}]
            }}]}]}
            """;

        var result = await handler.Handle(new IngestWhatsAppWebhookCommand(payload), CancellationToken.None);

        result.Success.Should().BeTrue();
        repo.Verify(r => r.UpdateMessageStatusAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task InvalidPayload_DoesNotThrow()
    {
        var repo = new Mock<IWhatsAppInboxRepository>(MockBehavior.Strict);
        var config = new Mock<IWhatsAppAccountConfig>(MockBehavior.Strict);
        var realtime = new Mock<IWhatsAppRealtimePublisher>();

        var handler = CreateHandler(repo.Object, config.Object, realtime.Object);

        var badJson = await handler.Handle(new IngestWhatsAppWebhookCommand("not-json"), CancellationToken.None);
        badJson.Success.Should().BeTrue();

        var empty = await handler.Handle(new IngestWhatsAppWebhookCommand("{}"), CancellationToken.None);
        empty.Success.Should().BeTrue();
    }

    [Fact]
    public async Task UnknownPhoneNumberId_DeadLetters_NoUpsert()
    {
        var repo = new Mock<IWhatsAppInboxRepository>(MockBehavior.Strict);
        var config = new Mock<IWhatsAppAccountConfig>(MockBehavior.Strict);
        var realtime = new Mock<IWhatsAppRealtimePublisher>();

        repo.Setup(r => r.ListActiveAccountRowsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Account });
        config.Setup(c => c.ResolveAccountByPhoneNumberId("unknown-phone", It.IsAny<IReadOnlyList<WhatsAppAccountRow>>()))
            .Returns((WhatsAppAccountRow?)null);
        repo.Setup(r => r.InsertWebhookDeadLetterAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = CreateHandler(repo.Object, config.Object, realtime.Object);
        var payload = """
            {"entry":[{"changes":[{"value":{
              "metadata":{"phone_number_id":"unknown-phone"},
              "messages":[{"id":"wamid.x","from":"971501234567","type":"text","text":{"body":"Hi"}}]
            }}]}]}
            """;

        var result = await handler.Handle(new IngestWhatsAppWebhookCommand(payload), CancellationToken.None);

        result.Success.Should().BeTrue();
        repo.Verify(r => r.InsertWebhookDeadLetterAsync(
            It.Is<string>(s => s.Contains("unknown-phone")), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        repo.Verify(r => r.UpsertInboundMessageAsync(It.IsAny<WhatsAppInboundPersistRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static IngestWhatsAppWebhookCommandHandler CreateHandler(
        IWhatsAppInboxRepository repo,
        IWhatsAppAccountConfig config,
        IWhatsAppRealtimePublisher realtime)
    {
        var options = Options.Create(new WhatsAppOptions { Enabled = true });
        var bot = new Mock<IWhatsAppBotOrchestrator>();
        bot.Setup(b => b.HandleInboundAsync(
                It.IsAny<int>(),
                It.IsAny<WhatsAppAccountRow>(),
                It.IsAny<WhatsAppConversationDto>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>()))
            .Returns(Task.CompletedTask);
        var automation = new Mock<IWhatsAppAutomationRepository>();
        var mediator = new Mock<IMediator>();
        return new IngestWhatsAppWebhookCommandHandler(
            repo,
            config,
            realtime,
            bot.Object,
            automation.Object,
            mediator.Object,
            options,
            NullLogger<IngestWhatsAppWebhookCommandHandler>.Instance);
    }
}
