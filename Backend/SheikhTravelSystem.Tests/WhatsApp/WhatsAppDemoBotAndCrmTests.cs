using FluentAssertions;
using Moq;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.WhatsApp;
using SheikhTravelSystem.Application.Features.WhatsApp.Bot;
using SheikhTravelSystem.Application.Features.WhatsApp.Commands;
using SheikhTravelSystem.Application.Features.WhatsApp.DTOs;

namespace SheikhTravelSystem.Tests.WhatsApp;

public class WhatsAppDemoQualificationBotTests
{
    private readonly WhatsAppDemoQualificationBot _bot = new();

    [Fact]
    public void Idle_IgnoresNonDemo()
    {
        var reply = _bot.Process(new WhatsAppDemoBotInput(WhatsAppDemoBotStates.Idle, true, "hello"));
        reply.Should().BeNull();
    }

    [Fact]
    public void Idle_Demo_StartsFleetType()
    {
        var reply = _bot.Process(new WhatsAppDemoBotInput(WhatsAppDemoBotStates.Idle, true, "DEMO"));
        reply.Should().NotBeNull();
        reply!.NextState.Should().Be(WhatsAppDemoBotStates.AwaitFleetType);
        reply.TextBody.Should().Contain("fleet");
        reply.ListOptions.Should().BeEquivalentTo(WhatsAppDemoBotOptions.FleetTypes);
        reply.QualifyLead.Should().BeFalse();
    }

    [Fact]
    public void BotDisabled_ReturnsNull()
    {
        var reply = _bot.Process(new WhatsAppDemoBotInput(WhatsAppDemoBotStates.Idle, false, "DEMO"));
        reply.Should().BeNull();
    }

    [Fact]
    public void AwaitFleetType_ValidLabel_Advances()
    {
        var reply = _bot.Process(new WhatsAppDemoBotInput(
            WhatsAppDemoBotStates.AwaitFleetType, true, "Car Rental"));
        reply!.NextState.Should().Be(WhatsAppDemoBotStates.AwaitFleetSize);
        reply.StoredFleetType.Should().Be("Car Rental");
        reply.ListOptions.Should().BeEquivalentTo(WhatsAppDemoBotOptions.FleetSizes);
    }

    [Fact]
    public void AwaitFleetType_ValidIndex_Advances()
    {
        var reply = _bot.Process(new WhatsAppDemoBotInput(
            WhatsAppDemoBotStates.AwaitFleetType, true, "3"));
        reply!.StoredFleetType.Should().Be("Logistics");
        reply.NextState.Should().Be(WhatsAppDemoBotStates.AwaitFleetSize);
    }

    [Fact]
    public void AwaitFleetType_Invalid_StaysAndReprompts()
    {
        var reply = _bot.Process(new WhatsAppDemoBotInput(
            WhatsAppDemoBotStates.AwaitFleetType, true, "spaceships"));
        reply!.NextState.Should().Be(WhatsAppDemoBotStates.AwaitFleetType);
        reply.TextBody.Should().Contain("Please choose");
        reply.StoredFleetType.Should().BeNull();
    }

    [Fact]
    public void AwaitFleetSize_Valid_Advances()
    {
        var reply = _bot.Process(new WhatsAppDemoBotInput(
            WhatsAppDemoBotStates.AwaitFleetSize, true, "26-50"));
        reply!.NextState.Should().Be(WhatsAppDemoBotStates.AwaitChallenge);
        reply.StoredFleetSize.Should().Be("26-50");
        reply.ListOptions.Should().BeEquivalentTo(WhatsAppDemoBotOptions.Challenges);
    }

    [Fact]
    public void AwaitFleetSize_Invalid_Stays()
    {
        var reply = _bot.Process(new WhatsAppDemoBotInput(
            WhatsAppDemoBotStates.AwaitFleetSize, true, "999"));
        reply!.NextState.Should().Be(WhatsAppDemoBotStates.AwaitFleetSize);
        reply.TextBody.Should().Contain("Please choose");
    }

    [Fact]
    public void AwaitChallenge_Valid_Qualifies()
    {
        var reply = _bot.Process(new WhatsAppDemoBotInput(
            WhatsAppDemoBotStates.AwaitChallenge, true, "Fuel Management"));
        reply!.NextState.Should().Be(WhatsAppDemoBotStates.Completed);
        reply.StoredChallenge.Should().Be("Fuel Management");
        reply.QualifyLead.Should().BeTrue();
        reply.TextBody.Should().Contain("team member will contact you");
        reply.ListOptions.Should().BeNull();
    }

    [Fact]
    public void AwaitChallenge_OptId_Qualifies()
    {
        var reply = _bot.Process(new WhatsAppDemoBotInput(
            WhatsAppDemoBotStates.AwaitChallenge, true, "opt_1"));
        reply!.StoredChallenge.Should().Be("GPS Tracking");
        reply.QualifyLead.Should().BeTrue();
    }

    [Fact]
    public void AwaitChallenge_Invalid_Stays()
    {
        var reply = _bot.Process(new WhatsAppDemoBotInput(
            WhatsAppDemoBotStates.AwaitChallenge, true, "nada"));
        reply!.NextState.Should().Be(WhatsAppDemoBotStates.AwaitChallenge);
        reply.QualifyLead.Should().BeFalse();
    }

    [Fact]
    public void Completed_Demo_Restarts()
    {
        var reply = _bot.Process(new WhatsAppDemoBotInput(
            WhatsAppDemoBotStates.Completed, true, "demo"));
        reply!.NextState.Should().Be(WhatsAppDemoBotStates.AwaitFleetType);
    }

    [Fact]
    public void ActiveAlias_TreatedAsIdle()
    {
        var reply = _bot.Process(new WhatsAppDemoBotInput("Active", true, "DEMO"));
        reply!.NextState.Should().Be(WhatsAppDemoBotStates.AwaitFleetType);
    }
}

public class WhatsAppCrmLeadServiceTests
{
    private static WhatsAppConversationDto Conv(int? customerId = null, int? leadId = null) => new(
        10, 1, "UAE", 10, "+971501234567", "Ali", customerId, "Open", null, 0, null,
        LeadId: leadId, IsBotEnabled: true, CurrentBotState: "Idle");

    [Fact]
    public async Task CreatesLead_WhenNoneExists()
    {
        var repo = new Mock<IWhatsAppInboxRepository>();
        repo.Setup(r => r.FindCustomerIdByPhoneAsync(1, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int?)null);
        repo.Setup(r => r.FindWebsiteContactRequestIdByPhoneAsync(1, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int?)null);
        repo.Setup(r => r.CreateWhatsAppLeadAsync(It.IsAny<WhatsAppLeadCreateRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(99);
        repo.Setup(r => r.LinkCustomerAndLeadAsync(1, 10, null, 99, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var svc = new WhatsAppCrmLeadService(repo.Object);
        await svc.EnsureLeadLinkedAsync(1, Conv(), "+971501234567", "Ali", CancellationToken.None);

        repo.Verify(r => r.CreateWhatsAppLeadAsync(
            It.Is<WhatsAppLeadCreateRequest>(x => x.PhoneE164 == "+971501234567" && x.ConversationId == 10),
            It.IsAny<CancellationToken>()), Times.Once);
        repo.Verify(r => r.LinkCustomerAndLeadAsync(1, 10, null, 99, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReusesExistingLead_DoesNotCreate()
    {
        var repo = new Mock<IWhatsAppInboxRepository>();
        repo.Setup(r => r.FindCustomerIdByPhoneAsync(1, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);
        repo.Setup(r => r.FindWebsiteContactRequestIdByPhoneAsync(1, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(44);
        repo.Setup(r => r.AttachWhatsAppLeadLinksAsync(
                1, 44, 10, 1, "WhatsApp", It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        repo.Setup(r => r.LinkCustomerAndLeadAsync(1, 10, 5, 44, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var svc = new WhatsAppCrmLeadService(repo.Object);
        await svc.EnsureLeadLinkedAsync(1, Conv(), "+971501234567", "Ali", CancellationToken.None);

        repo.Verify(r => r.CreateWhatsAppLeadAsync(It.IsAny<WhatsAppLeadCreateRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        repo.Verify(r => r.AttachWhatsAppLeadLinksAsync(
            1, 44, 10, 1, "WhatsApp", It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConversationAlreadyLinked_SkipsLookupCreate()
    {
        var repo = new Mock<IWhatsAppInboxRepository>();
        repo.Setup(r => r.AttachWhatsAppLeadLinksAsync(
                1, 7, 10, 1, "WhatsApp", It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        repo.Setup(r => r.LinkCustomerAndLeadAsync(1, 10, 3, 7, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var svc = new WhatsAppCrmLeadService(repo.Object);
        await svc.EnsureLeadLinkedAsync(1, Conv(customerId: 3, leadId: 7), "+971501234567", null, CancellationToken.None);

        repo.Verify(r => r.FindWebsiteContactRequestIdByPhoneAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        repo.Verify(r => r.CreateWhatsAppLeadAsync(It.IsAny<WhatsAppLeadCreateRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("+923001112233", "Pakistan")]
    [InlineData("+971501234567", "United Arab Emirates")]
    public void DeriveCountry_FromPrefix(string phone, string expected)
    {
        WhatsAppCrmLeadService.DeriveCountry(phone).Should().Be(expected);
    }
}

public class WhatsAppHandoffTests
{
    private static WhatsAppConversationDto Conv(bool bot = true, int? assigned = null) => new(
        1, 1, "UAE", 1, "+971501234567", "Ali", null, "Open", DateTime.UtcNow, 0, null,
        AssignedUserId: assigned, IsBotEnabled: bot, CurrentBotState: bot ? "Idle" : null,
        LastIncomingMessageAt: DateTime.UtcNow.AddMinutes(-10));

    [Fact]
    public async Task Assignment_DisablesBot()
    {
        var repo = new Mock<IWhatsAppInboxRepository>();
        var realtime = new Mock<IWhatsAppRealtimePublisher>();
        var tenant = new Mock<ITenantContext>();
        tenant.Setup(t => t.GetRequiredTenantId()).Returns(1);
        repo.SetupSequence(r => r.GetConversationAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Conv(bot: true))
            .ReturnsAsync(Conv(bot: false, assigned: 42));
        repo.Setup(r => r.SetConversationAssignmentAsync(1, 1, 42, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repo.Setup(r => r.SetConversationBotAsync(1, 1, false, WhatsAppDemoBotStates.HandedOff, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = new SetWhatsAppConversationAssignmentCommandHandler(repo.Object, realtime.Object, tenant.Object);
        var result = await handler.Handle(new SetWhatsAppConversationAssignmentCommand(1, 42), CancellationToken.None);

        result.Success.Should().BeTrue();
        repo.Verify(r => r.SetConversationBotAsync(1, 1, false, WhatsAppDemoBotStates.HandedOff, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReEnableBot_SetsIdle()
    {
        var repo = new Mock<IWhatsAppInboxRepository>();
        var realtime = new Mock<IWhatsAppRealtimePublisher>();
        var tenant = new Mock<ITenantContext>();
        tenant.Setup(t => t.GetRequiredTenantId()).Returns(1);
        repo.SetupSequence(r => r.GetConversationAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Conv(bot: false))
            .ReturnsAsync(Conv(bot: true));
        repo.Setup(r => r.SetConversationBotAsync(1, 1, true, WhatsAppDemoBotStates.Idle, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = new SetWhatsAppConversationBotCommandHandler(repo.Object, realtime.Object, tenant.Object);
        var result = await handler.Handle(new SetWhatsAppConversationBotCommand(1, true), CancellationToken.None);

        result.Success.Should().BeTrue();
        repo.Verify(r => r.SetConversationBotAsync(1, 1, true, WhatsAppDemoBotStates.Idle, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AgentSend_DisablesBotAndAssigns()
    {
        var repo = new Mock<IWhatsAppInboxRepository>();
        var cloud = new Mock<IWhatsAppCloudApiService>();
        var config = new Mock<IWhatsAppAccountConfig>();
        var resolver = new Mock<IWhatsAppAccountResolver>();
        var tenant = new Mock<ITenantContext>();
        var currentUser = new Mock<ICurrentUserService>();
        var realtime = new Mock<IWhatsAppRealtimePublisher>();

        var account = new WhatsAppAccountRow(1, 1, "UAE", "UAE", "+971557701219", "Sales", "pn", null, true, true);
        tenant.Setup(t => t.GetRequiredTenantId()).Returns(1);
        currentUser.Setup(c => c.UserId).Returns(77);
        repo.Setup(r => r.GetConversationAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Conv(bot: true));
        repo.Setup(r => r.GetAccountByIdAsync(1, 1, It.IsAny<CancellationToken>())).ReturnsAsync(account);
        config.Setup(c => c.ResolveCredentials(account))
            .Returns(new WhatsAppAccountCredentials("pn", "tok", null));
        repo.Setup(r => r.SetConversationBotAsync(1, 1, false, WhatsAppDemoBotStates.HandedOff, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        repo.Setup(r => r.SetConversationAssignmentAsync(1, 1, 77, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repo.Setup(r => r.InsertOutboundMessageAsync(1, 1, null, "text", "Hi", "Queued", It.IsAny<CancellationToken>(), null))
            .ReturnsAsync(5);
        repo.Setup(r => r.SetOutboundStatusAsync(5, "Sending", null, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        cloud.Setup(c => c.SendTextAsync(account, "971501234567", "Hi", It.IsAny<CancellationToken>()))
            .ReturnsAsync(WhatsAppCloudApiResult.Ok("wamid.1"));
        repo.Setup(r => r.SetOutboundMetaIdAsync(5, "wamid.1", "Sent", It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var handler = new SendWhatsAppOutboundCommandHandler(
            repo.Object, cloud.Object, config.Object, resolver.Object, tenant.Object, currentUser.Object, realtime.Object);

        var result = await handler.Handle(new SendWhatsAppOutboundCommand(null, 1, null, "text", "Hi"), CancellationToken.None);

        result.Success.Should().BeTrue();
        repo.Verify(r => r.SetConversationBotAsync(1, 1, false, WhatsAppDemoBotStates.HandedOff, It.IsAny<CancellationToken>()), Times.Once);
        repo.Verify(r => r.SetConversationAssignmentAsync(1, 1, 77, It.IsAny<CancellationToken>()), Times.Once);
    }
}
