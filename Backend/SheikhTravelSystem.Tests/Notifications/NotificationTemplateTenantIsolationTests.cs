using FluentAssertions;
using Moq;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Notifications.Commands;
using SheikhTravelSystem.Application.Features.Notifications.DTOs;
using SheikhTravelSystem.Application.Features.Notifications.Queries;

namespace SheikhTravelSystem.Tests.Notifications;

public class NotificationTemplateTenantIsolationTests
{
    [Fact]
    public async Task GetTemplates_passes_required_tenantId()
    {
        var repo = new Mock<INotificationRepository>();
        var tenant = new Mock<ITenantContext>();
        tenant.Setup(t => t.GetRequiredTenantId()).Returns(11);
        repo.Setup(r => r.GetTemplatesAsync(11, "Email", It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var handler = new GetNotificationTemplatesQueryHandler(repo.Object, tenant.Object);
        await handler.Handle(new GetNotificationTemplatesQuery("Email"), CancellationToken.None);

        repo.Verify(r => r.GetTemplatesAsync(11, "Email", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateTemplate_other_tenant_returns_not_found()
    {
        var repo = new Mock<INotificationRepository>();
        var tenant = new Mock<ITenantContext>();
        tenant.Setup(t => t.GetRequiredTenantId()).Returns(2);
        repo.Setup(r => r.UpdateTemplateAsync(
                2, 99, It.IsAny<UpsertNotificationTemplateRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = new UpsertNotificationTemplateCommandHandler(repo.Object, tenant.Object);
        var result = await handler.Handle(
            new UpsertNotificationTemplateCommand(
                new UpsertNotificationTemplateRequest(
                    "k", "n", "s", "b", "Email", true, "en", null),
                99),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("not found");
        repo.Verify(r => r.UpdateTemplateAsync(
            2, 99, It.IsAny<UpsertNotificationTemplateRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteTemplate_scopes_to_tenant()
    {
        var repo = new Mock<INotificationRepository>();
        var tenant = new Mock<ITenantContext>();
        tenant.Setup(t => t.GetRequiredTenantId()).Returns(5);
        repo.Setup(r => r.SoftDeleteTemplateAsync(5, 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = new DeleteNotificationTemplateCommandHandler(repo.Object, tenant.Object);
        var result = await handler.Handle(new DeleteNotificationTemplateCommand(7), CancellationToken.None);

        result.Success.Should().BeTrue();
        repo.Verify(r => r.SoftDeleteTemplateAsync(5, 7, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void NotificationRepository_template_sql_requires_TenantId()
    {
        var root = FindBackendRoot();
        var path = Directory.EnumerateFiles(root, "NotificationRepository.cs", SearchOption.AllDirectories).First();
        var src = File.ReadAllText(path);
        src.Should().Contain("WHERE IsDeleted = 0 AND TenantId = @TenantId");
        src.Should().Contain("WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0");
        src.Should().Contain("INSERT INTO NotificationTemplates\n                (TenantId,");
        src.Should().Contain("(NotificationId, TenantId, UserId, DeliveryStatus");
    }

    private static string FindBackendRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var backend = Path.Combine(dir.FullName, "Backend");
            if (Directory.Exists(backend))
                return backend;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("Could not locate Backend root.");
    }
}
