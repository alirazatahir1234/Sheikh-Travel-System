using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.WhatsApp;
using SheikhTravelSystem.Application.Features.WhatsApp.Commands;
using SheikhTravelSystem.Application.Features.WhatsApp.DTOs;

namespace SheikhTravelSystem.Tests.WhatsApp;

public class WhatsAppRoutingServiceTests
{
    private static WhatsAppRoutingService CreateService(Dictionary<string, string>? prefixes = null, string fallback = "UAE")
    {
        var opts = Options.Create(new WhatsAppRoutingOptions
        {
            DefaultAccountCode = fallback,
            CountryPrefixes = prefixes ?? new WhatsAppRoutingOptions().CountryPrefixes
        });
        return new WhatsAppRoutingService(opts);
    }

    [Theory]
    [InlineData("+971501234567", "UAE")]
    [InlineData("971557701219", "UAE")]
    public void Resolves_Uae(string phone, string code)
        => CreateService().ResolveAccountCode(phone).Should().Be(code);

    [Theory]
    [InlineData("+923177368305", "PK")]
    [InlineData("923001112233", "PK")]
    public void Resolves_Pakistan(string phone, string code)
        => CreateService().ResolveAccountCode(phone).Should().Be(code);

    [Theory]
    [InlineData("+966501234567", "UAE")]
    [InlineData("+974501234567", "UAE")]
    [InlineData("+968501234567", "UAE")]
    [InlineData("+965501234567", "UAE")]
    [InlineData("+973501234567", "UAE")]
    public void Resolves_Gcc_To_Uae(string phone, string code)
        => CreateService().ResolveAccountCode(phone).Should().Be(code);

    [Fact]
    public void LongestPrefix_Wins()
    {
        var service = CreateService(new Dictionary<string, string>
        {
            ["9"] = "PK",
            ["92"] = "PK",
            ["971"] = "UAE"
        });
        service.ResolveAccountCode("+97150").Should().Be("UAE");
    }

    [Fact]
    public void UnknownCountry_UsesDefault()
        => CreateService(fallback: "UAE").ResolveAccountCode("+15551234567").Should().Be("UAE");
}

public class WhatsAppAccountResolverTests
{
    private static readonly WhatsAppAccountRow Uae = new(
        1, 1, "UAE", "SheikhGo UAE", "+971557701219", "Sales", "pn-uae", null, true, true);
    private static readonly WhatsAppAccountRow Pk = new(
        2, 1, "PK", "SheikhGo Pakistan", "+923177368305", "Support", "pn-pk", null, true, false);

    [Fact]
    public async Task ExplicitAccountId_OverridesPhoneRouting()
    {
        var repo = new Mock<IWhatsAppInboxRepository>();
        repo.Setup(r => r.GetAccountByIdAsync(1, 2, It.IsAny<CancellationToken>())).ReturnsAsync(Pk);
        var routing = new Mock<IWhatsAppRoutingService>();
        routing.Setup(r => r.ResolveAccountCode(It.IsAny<string?>())).Returns("UAE");

        var resolver = new WhatsAppAccountResolver(repo.Object, routing.Object);
        var account = await resolver.ResolveAsync(1, whatsAppAccountId: 2, recipientPhone: "+971501111111");

        account.Should().Be(Pk);
        routing.Verify(r => r.ResolveAccountCode(It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task InvalidAccount_WrongTenant_ReturnsNull()
    {
        var repo = new Mock<IWhatsAppInboxRepository>();
        repo.Setup(r => r.GetAccountByIdAsync(1, 99, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WhatsAppAccountRow?)null);
        var resolver = new WhatsAppAccountResolver(repo.Object, Mock.Of<IWhatsAppRoutingService>());

        var account = await resolver.ResolveAsync(1, 99, "+97150");
        account.Should().BeNull();
    }

    [Fact]
    public async Task PhoneRouting_SelectsMatchingCode()
    {
        var repo = new Mock<IWhatsAppInboxRepository>();
        repo.Setup(r => r.ListActiveAccountRowsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Uae, Pk });
        var routing = new Mock<IWhatsAppRoutingService>();
        routing.Setup(r => r.ResolveAccountCode("+92300")).Returns("PK");

        var resolver = new WhatsAppAccountResolver(repo.Object, routing.Object);
        var account = await resolver.ResolveAsync(1, null, "+92300");
        account!.Code.Should().Be("PK");
    }
}

public class SendWhatsAppOutboundCommandHandlerTests
{
    private static readonly WhatsAppAccountRow Uae = new(
        1, 1, "UAE", "SheikhGo UAE", "+971557701219", "Sales", "pn-uae", null, true, true);
    private static readonly WhatsAppAccountRow Pk = new(
        2, 1, "PK", "SheikhGo Pakistan", "+923177368305", "Support", "pn-pk", null, true, false);

    [Fact]
    public async Task MissingConfiguration_FailsWithoutCloudCall()
    {
        var repo = new Mock<IWhatsAppInboxRepository>();
        var cloud = new Mock<IWhatsAppCloudApiService>(MockBehavior.Strict);
        var config = new Mock<IWhatsAppAccountConfig>();
        var resolver = new Mock<IWhatsAppAccountResolver>();
        var tenant = new Mock<ITenantContext>();
        var realtime = new Mock<IWhatsAppRealtimePublisher>();

        tenant.Setup(t => t.GetRequiredTenantId()).Returns(1);
        resolver.Setup(r => r.ResolveAsync(1, null, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Uae);
        config.Setup(c => c.ResolveCredentials(Uae)).Returns((WhatsAppAccountCredentials?)null);
        repo.Setup(r => r.EnsureConversationAsync(1, 1, It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(10);

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.Setup(c => c.UserId).Returns((int?)null);

        var handler = new SendWhatsAppOutboundCommandHandler(
            repo.Object, cloud.Object, config.Object, resolver.Object, tenant.Object, currentUser.Object, realtime.Object);

        var result = await handler.Handle(new SendWhatsAppOutboundCommand(
            null, null, "+971501234567", "text", "Hello"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("credentials");
        cloud.Verify(c => c.SendTextAsync(
            It.IsAny<WhatsAppAccountRow>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExplicitOverride_UsesSelectedAccount()
    {
        var repo = new Mock<IWhatsAppInboxRepository>();
        var cloud = new Mock<IWhatsAppCloudApiService>();
        var config = new Mock<IWhatsAppAccountConfig>();
        var resolver = new Mock<IWhatsAppAccountResolver>();
        var tenant = new Mock<ITenantContext>();
        var realtime = new Mock<IWhatsAppRealtimePublisher>();

        tenant.Setup(t => t.GetRequiredTenantId()).Returns(1);
        resolver.Setup(r => r.ResolveAsync(1, 2, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Pk);
        config.Setup(c => c.ResolveCredentials(Pk))
            .Returns(new WhatsAppAccountCredentials("pn-pk", "token", null));
        repo.Setup(r => r.EnsureConversationAsync(1, 2, It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(55);
        repo.Setup(r => r.GetConversationAsync(1, 55, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WhatsAppConversationDto(
                55, 2, "PK", 55, "+923001112233", null, null, "Open", DateTime.UtcNow, 0, null,
                LastIncomingMessageAt: DateTime.UtcNow.AddMinutes(-30)));
        repo.Setup(r => r.InsertOutboundMessageAsync(
                1, 55, null, "text", "Hi", "Queued", It.IsAny<CancellationToken>(), null))
            .ReturnsAsync(100);
        repo.Setup(r => r.SetOutboundStatusAsync(100, "Sending", null, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        cloud.Setup(c => c.SendTextAsync(Pk, "923001112233", "Hi", It.IsAny<CancellationToken>()))
            .ReturnsAsync(WhatsAppCloudApiResult.Ok("wamid.OK"));
        repo.Setup(r => r.SetOutboundMetaIdAsync(100, "wamid.OK", "Sent", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.Setup(c => c.UserId).Returns((int?)null);

        var handler = new SendWhatsAppOutboundCommandHandler(
            repo.Object, cloud.Object, config.Object, resolver.Object, tenant.Object, currentUser.Object, realtime.Object);

        var result = await handler.Handle(new SendWhatsAppOutboundCommand(
            2, null, "+923001112233", "text", "Hi"), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Data!.Status.Should().Be("Sent");
        result.Data.MetaMessageId.Should().Be("wamid.OK");
        cloud.Verify(c => c.SendTextAsync(Pk, "923001112233", "Hi", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TenantIsolation_ConversationNotFound()
    {
        var repo = new Mock<IWhatsAppInboxRepository>();
        var tenant = new Mock<ITenantContext>();
        tenant.Setup(t => t.GetRequiredTenantId()).Returns(1);
        repo.Setup(r => r.GetConversationAsync(1, 9, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WhatsAppConversationDto?)null);

        var handler = new SendWhatsAppOutboundCommandHandler(
            repo.Object,
            Mock.Of<IWhatsAppCloudApiService>(),
            Mock.Of<IWhatsAppAccountConfig>(),
            Mock.Of<IWhatsAppAccountResolver>(),
            tenant.Object,
            Mock.Of<ICurrentUserService>(),
            Mock.Of<IWhatsAppRealtimePublisher>());

        var result = await handler.Handle(new SendWhatsAppOutboundCommand(
            null, 9, null, "text", "Hi"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Conversation not found");
    }
}
