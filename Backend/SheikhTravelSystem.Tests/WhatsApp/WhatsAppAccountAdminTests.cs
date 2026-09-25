using FluentAssertions;
using Moq;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.WhatsApp;
using SheikhTravelSystem.Application.Features.WhatsApp.Commands;
using SheikhTravelSystem.Application.Features.WhatsApp.DTOs;
using SheikhTravelSystem.Application.Features.WhatsApp.Queries;

namespace SheikhTravelSystem.Tests.WhatsApp;

public class WhatsAppAccountAdminTests
{
    private static readonly WhatsAppAccountDto UaeDto = new(
        1, "UAE", "SheikhGo UAE", "+971557701219", "Sales", "pn-uae", true, false,
        Country: "AE", CountryCode: "+971", Status: "Active", IsDefault: true);

    private static readonly WhatsAppAccountDto PkInactiveDto = new(
        2, "PK", "SheikhGo Pakistan", "+923177368305", "Support", "pn-pk", false, false,
        Country: "PK", CountryCode: "+92", Status: "Inactive", IsDefault: false);

    private static readonly WhatsAppAccountRow UaeRow = new(
        1, 1, "UAE", "SheikhGo UAE", "+971557701219", "Sales", "pn-uae", null, true, true);

    [Fact]
    public async Task GetAccounts_IncludeInactive_RequiresManage()
    {
        var user = new Mock<ICurrentUserService>();
        user.Setup(u => u.HasPermission(WhatsAppPermissions.Manage)).Returns(false);
        var tenant = new Mock<ITenantContext>();
        var repo = new Mock<IWhatsAppInboxRepository>();
        var config = new Mock<IWhatsAppAccountConfig>();

        var handler = new GetWhatsAppAccountsQueryHandler(repo.Object, config.Object, tenant.Object, user.Object);
        var result = await handler.Handle(new GetWhatsAppAccountsQuery(IncludeInactive: true), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("WhatsApp.ManageAccounts");
        repo.Verify(r => r.GetAccountsAsync(It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetAccounts_IncludeInactive_WithManageAccounts_ListsAll()
    {
        var user = new Mock<ICurrentUserService>();
        user.Setup(u => u.HasPermission(WhatsAppPermissions.ManageAccounts)).Returns(true);
        var tenant = new Mock<ITenantContext>();
        tenant.Setup(t => t.GetRequiredTenantId()).Returns(1);
        var repo = new Mock<IWhatsAppInboxRepository>();
        repo.Setup(r => r.GetAccountsAsync(1, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WhatsAppAccountDto> { UaeDto, PkInactiveDto });
        var config = new Mock<IWhatsAppAccountConfig>();
        config.Setup(c => c.Enrich(It.IsAny<WhatsAppAccountDto>()))
            .Returns<WhatsAppAccountDto>(a => a with { HasAccessToken = true });

        var handler = new GetWhatsAppAccountsQueryHandler(repo.Object, config.Object, tenant.Object, user.Object);
        var result = await handler.Handle(new GetWhatsAppAccountsQuery(true), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Data!.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAccounts_IncludeInactive_WithManage_ListsAll_Legacy()
    {
        var user = new Mock<ICurrentUserService>();
        user.Setup(u => u.HasPermission(WhatsAppPermissions.Manage)).Returns(true);
        var tenant = new Mock<ITenantContext>();
        tenant.Setup(t => t.GetRequiredTenantId()).Returns(1);
        var repo = new Mock<IWhatsAppInboxRepository>();
        repo.Setup(r => r.GetAccountsAsync(1, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WhatsAppAccountDto> { UaeDto, PkInactiveDto });
        var config = new Mock<IWhatsAppAccountConfig>();
        config.Setup(c => c.Enrich(It.IsAny<WhatsAppAccountDto>()))
            .Returns<WhatsAppAccountDto>(a => a with { HasAccessToken = true });

        var handler = new GetWhatsAppAccountsQueryHandler(repo.Object, config.Object, tenant.Object, user.Object);
        var result = await handler.Handle(new GetWhatsAppAccountsQuery(true), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Data!.Should().HaveCount(2);
        result.Data.Should().Contain(a => !a.IsActive);
        result.Data![0].HasAccessToken.Should().BeTrue();
        typeof(WhatsAppAccountDto).GetProperty("AccessToken").Should().BeNull();
    }

    [Fact]
    public async Task SetActive_WrongTenant_Fails()
    {
        var tenant = new Mock<ITenantContext>();
        tenant.Setup(t => t.GetRequiredTenantId()).Returns(1);
        var repo = new Mock<IWhatsAppInboxRepository>();
        repo.Setup(r => r.GetAccountByIdAsync(1, 99, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WhatsAppAccountRow?)null);
        var config = new Mock<IWhatsAppAccountConfig>();

        var handler = new SetWhatsAppAccountActiveCommandHandler(repo.Object, config.Object, tenant.Object);
        var result = await handler.Handle(new SetWhatsAppAccountActiveCommand(99, false), CancellationToken.None);

        result.Success.Should().BeFalse();
        repo.Verify(r => r.SetAccountActiveAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SetActive_DisablesAccount()
    {
        var tenant = new Mock<ITenantContext>();
        tenant.Setup(t => t.GetRequiredTenantId()).Returns(1);
        var repo = new Mock<IWhatsAppInboxRepository>();
        repo.Setup(r => r.GetAccountByIdAsync(1, 1, It.IsAny<CancellationToken>())).ReturnsAsync(UaeRow);
        repo.Setup(r => r.SetAccountActiveAsync(1, 1, false, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var disabled = UaeDto with { IsActive = false, Status = "Inactive" };
        repo.Setup(r => r.GetAccountsAsync(1, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WhatsAppAccountDto> { disabled });
        var config = new Mock<IWhatsAppAccountConfig>();
        config.Setup(c => c.Enrich(It.IsAny<WhatsAppAccountDto>())).Returns<WhatsAppAccountDto>(a => a);

        var handler = new SetWhatsAppAccountActiveCommandHandler(repo.Object, config.Object, tenant.Object);
        var result = await handler.Handle(new SetWhatsAppAccountActiveCommand(1, false), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Data!.IsActive.Should().BeFalse();
        result.Data.Status.Should().Be("Inactive");
    }

    [Fact]
    public async Task Health_Misconfigured_DoesNotCallCloud()
    {
        var tenant = new Mock<ITenantContext>();
        tenant.Setup(t => t.GetRequiredTenantId()).Returns(1);
        var repo = new Mock<IWhatsAppInboxRepository>();
        repo.Setup(r => r.GetAccountByIdAsync(1, 1, It.IsAny<CancellationToken>())).ReturnsAsync(UaeRow);
        var config = new Mock<IWhatsAppAccountConfig>();
        config.Setup(c => c.ResolveCredentials(UaeRow)).Returns((WhatsAppAccountCredentials?)null);
        var cloud = new Mock<IWhatsAppCloudApiService>(MockBehavior.Strict);

        var handler = new CheckWhatsAppAccountHealthCommandHandler(
            repo.Object, config.Object, cloud.Object, tenant.Object);
        var result = await handler.Handle(new CheckWhatsAppAccountHealthCommand(1), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Data!.Status.Should().Be("Misconfigured");
        repo.Verify(r => r.UpdateAccountHealthAsync(
            1, 1, "Misconfigured", It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        cloud.Verify(c => c.CheckPhoneNumberAsync(It.IsAny<WhatsAppAccountRow>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Health_Healthy_PersistsStatus()
    {
        var tenant = new Mock<ITenantContext>();
        tenant.Setup(t => t.GetRequiredTenantId()).Returns(1);
        var repo = new Mock<IWhatsAppInboxRepository>();
        repo.Setup(r => r.GetAccountByIdAsync(1, 1, It.IsAny<CancellationToken>())).ReturnsAsync(UaeRow);
        var config = new Mock<IWhatsAppAccountConfig>();
        config.Setup(c => c.ResolveCredentials(UaeRow))
            .Returns(new WhatsAppAccountCredentials("pn-uae", "token", null));
        var cloud = new Mock<IWhatsAppCloudApiService>();
        cloud.Setup(c => c.CheckPhoneNumberAsync(UaeRow, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WhatsAppCloudApiResult(true, "pn-uae", WhatsAppCloudErrorKind.None, null, "Verified: UAE"));

        var handler = new CheckWhatsAppAccountHealthCommandHandler(
            repo.Object, config.Object, cloud.Object, tenant.Object);
        var result = await handler.Handle(new CheckWhatsAppAccountHealthCommand(1), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Data!.Status.Should().Be("Healthy");
        repo.Verify(r => r.UpdateAccountHealthAsync(
            1, 1, "Healthy", It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Health_Unhealthy_PersistsStatus()
    {
        var tenant = new Mock<ITenantContext>();
        tenant.Setup(t => t.GetRequiredTenantId()).Returns(1);
        var repo = new Mock<IWhatsAppInboxRepository>();
        repo.Setup(r => r.GetAccountByIdAsync(1, 1, It.IsAny<CancellationToken>())).ReturnsAsync(UaeRow);
        var config = new Mock<IWhatsAppAccountConfig>();
        config.Setup(c => c.ResolveCredentials(UaeRow))
            .Returns(new WhatsAppAccountCredentials("pn-uae", "token", null));
        var cloud = new Mock<IWhatsAppCloudApiService>();
        cloud.Setup(c => c.CheckPhoneNumberAsync(UaeRow, It.IsAny<CancellationToken>()))
            .ReturnsAsync(WhatsAppCloudApiResult.Fail(WhatsAppCloudErrorKind.InvalidToken, "Invalid OAuth token"));

        var handler = new CheckWhatsAppAccountHealthCommandHandler(
            repo.Object, config.Object, cloud.Object, tenant.Object);
        var result = await handler.Handle(new CheckWhatsAppAccountHealthCommand(1), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Data!.Status.Should().Be("Unhealthy");
        repo.Verify(r => r.UpdateAccountHealthAsync(
            1, 1, "Unhealthy", "Invalid OAuth token", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
