using FluentAssertions;
using Moq;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.WhatsApp.Commands;
using SheikhTravelSystem.Application.Features.WhatsApp.DTOs;
using SheikhTravelSystem.Application.Features.WhatsApp.Queries;

namespace SheikhTravelSystem.Tests.WhatsApp;

public class WhatsAppInboxFilterAndMutationTests
{
    private static WhatsAppConversationDto Conv(
        int id = 1,
        int unread = 0,
        int? assigned = null,
        bool bot = false,
        string status = "Open")
        => new(
            id, 1, "UAE", id, "+971501111111", "Ali", null, status, DateTime.UtcNow, unread, "Hi",
            AssignedUserId: assigned, AssignedUserName: assigned is null ? null : "Agent",
            IsBotEnabled: bot, Country: "AE");

    [Fact]
    public async Task GetConversations_PassesFilterAndCurrentUser()
    {
        var repo = new Mock<IWhatsAppInboxRepository>();
        var tenant = new Mock<ITenantContext>();
        tenant.Setup(t => t.GetRequiredTenantId()).Returns(1);
        var user = new Mock<ICurrentUserService>();
        user.Setup(u => u.UserId).Returns(42);
        repo.Setup(r => r.GetConversationsAsync(1, null, null, "assigned_to_me", 42, 1, 30, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<WhatsAppConversationDto> { Conv(assigned: 42) }, 1));

        var handler = new GetWhatsAppConversationsQueryHandler(repo.Object, tenant.Object, user.Object);
        var result = await handler.Handle(new GetWhatsAppConversationsQuery(Filter: "assigned_to_me"), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Data!.Items.Should().HaveCount(1);
        repo.Verify(r => r.GetConversationsAsync(1, null, null, "assigned_to_me", 42, 1, 30, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetConversations_UnreadFilter_Forwarded()
    {
        var repo = new Mock<IWhatsAppInboxRepository>();
        var tenant = new Mock<ITenantContext>();
        tenant.Setup(t => t.GetRequiredTenantId()).Returns(1);
        var user = new Mock<ICurrentUserService>();
        user.Setup(u => u.UserId).Returns(1);
        repo.Setup(r => r.GetConversationsAsync(1, null, null, "unread", 1, 1, 30, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<WhatsAppConversationDto> { Conv(unread: 3) }, 1));

        var handler = new GetWhatsAppConversationsQueryHandler(repo.Object, tenant.Object, user.Object);
        var result = await handler.Handle(new GetWhatsAppConversationsQuery(Filter: "unread"), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Data!.Items[0].UnreadCount.Should().Be(3);
    }

    [Fact]
    public async Task Assignment_WrongTenant_Fails()
    {
        var repo = new Mock<IWhatsAppInboxRepository>();
        var realtime = new Mock<IWhatsAppRealtimePublisher>(MockBehavior.Strict);
        var tenant = new Mock<ITenantContext>();
        tenant.Setup(t => t.GetRequiredTenantId()).Returns(1);
        repo.Setup(r => r.GetConversationAsync(1, 99, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WhatsAppConversationDto?)null);

        var handler = new SetWhatsAppConversationAssignmentCommandHandler(repo.Object, realtime.Object, tenant.Object);
        var result = await handler.Handle(new SetWhatsAppConversationAssignmentCommand(99, 5), CancellationToken.None);

        result.Success.Should().BeFalse();
        repo.Verify(r => r.SetConversationAssignmentAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Assignment_PublishesConversationUpdated()
    {
        var repo = new Mock<IWhatsAppInboxRepository>();
        var realtime = new Mock<IWhatsAppRealtimePublisher>();
        var tenant = new Mock<ITenantContext>();
        tenant.Setup(t => t.GetRequiredTenantId()).Returns(1);
        var before = Conv();
        var after = Conv(assigned: 7);
        repo.Setup(r => r.GetConversationAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(before);
        repo.Setup(r => r.SetConversationAssignmentAsync(1, 1, 7, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repo.SetupSequence(r => r.GetConversationAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(before)
            .ReturnsAsync(after);

        var handler = new SetWhatsAppConversationAssignmentCommandHandler(repo.Object, realtime.Object, tenant.Object);
        var result = await handler.Handle(new SetWhatsAppConversationAssignmentCommand(1, 7), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Data!.AssignedUserId.Should().Be(7);
        realtime.Verify(r => r.PublishAsync(1, It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Status_Invalid_Fails()
    {
        var repo = new Mock<IWhatsAppInboxRepository>(MockBehavior.Strict);
        var realtime = new Mock<IWhatsAppRealtimePublisher>(MockBehavior.Strict);
        var tenant = new Mock<ITenantContext>();
        tenant.Setup(t => t.GetRequiredTenantId()).Returns(1);

        var handler = new SetWhatsAppConversationStatusCommandHandler(repo.Object, realtime.Object, tenant.Object);
        var result = await handler.Handle(new SetWhatsAppConversationStatusCommand(1, "Closed"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Open");
    }

    [Fact]
    public async Task BotToggle_PublishesUpdate()
    {
        var repo = new Mock<IWhatsAppInboxRepository>();
        var realtime = new Mock<IWhatsAppRealtimePublisher>();
        var tenant = new Mock<ITenantContext>();
        tenant.Setup(t => t.GetRequiredTenantId()).Returns(1);
        var before = Conv(bot: false);
        var after = Conv(bot: true);
        repo.SetupSequence(r => r.GetConversationAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(before)
            .ReturnsAsync(after);
        repo.Setup(r => r.SetConversationBotAsync(1, 1, true, "Idle", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var handler = new SetWhatsAppConversationBotCommandHandler(repo.Object, realtime.Object, tenant.Object);
        var result = await handler.Handle(new SetWhatsAppConversationBotCommand(1, true), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Data!.IsBotEnabled.Should().BeTrue();
        realtime.Verify(r => r.PublishAsync(1, It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MarkRead_PublishesUnreadAndConversationUpdated()
    {
        var repo = new Mock<IWhatsAppInboxRepository>();
        var realtime = new Mock<IWhatsAppRealtimePublisher>();
        var tenant = new Mock<ITenantContext>();
        tenant.Setup(t => t.GetRequiredTenantId()).Returns(1);
        var before = Conv(unread: 4);
        var after = Conv(unread: 0);
        repo.SetupSequence(r => r.GetConversationAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(before)
            .ReturnsAsync(after);
        repo.Setup(r => r.MarkConversationReadAsync(1, 1, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var handler = new MarkWhatsAppConversationReadCommandHandler(repo.Object, realtime.Object, tenant.Object);
        var result = await handler.Handle(new MarkWhatsAppConversationReadCommand(1), CancellationToken.None);

        result.Success.Should().BeTrue();
        realtime.Verify(r => r.PublishAsync(1, It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}
