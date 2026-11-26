using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Platform;
using SheikhTravelSystem.Application.Features.TravelNexus.Reminders;

namespace SheikhTravelSystem.Tests.TravelNexus.LiveSql;

/// <summary>
/// Runs without SQL: the production dependency-injection graph used by the live tests must resolve every TravelNexus
/// request handler (and the module-enable handler), so a wiring gap shows up in every CI run, not only when a database
/// is available. No connection is ever opened — handlers only receive their repositories.
/// </summary>
public class TravelNexusHarnessWiringTests
{
    [Fact]
    public void Production_graph_resolves_every_TravelNexus_handler_without_a_database()
    {
        var root = Path.Combine(Path.GetTempPath(), $"SheikhGo_TravelNexus_Wiring_{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            using var provider = TravelNexusLiveDbFixture.BuildServices(
                "Server=127.0.0.1,1;Database=SheikhGo_Never_Opened;User Id=none;Password=none;TrustServerCertificate=True;Connect Timeout=1;",
                root, Mock.Of<INotificationService>());
            using var scope = provider.CreateScope();
            scope.ServiceProvider.GetRequiredService<E2ESessionState>().TenantId = 1;

            var requests = typeof(TravelPermissions).Assembly.GetTypes()
                .Where(t => !t.IsAbstract && !t.IsGenericTypeDefinition
                            && (t.Namespace ?? string.Empty).StartsWith("SheikhTravelSystem.Application.Features.TravelNexus", StringComparison.Ordinal))
                .Select(t => (Request: t, Iface: t.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>))))
                .Where(x => x.Iface is not null)
                .ToList();
            requests.Should().HaveCountGreaterThan(100, "TravelNexus has well over a hundred commands and queries");

            var failures = new List<string>();
            foreach (var (request, iface) in requests)
            {
                var handlerType = typeof(IRequestHandler<,>).MakeGenericType(request, iface!.GetGenericArguments()[0]);
                try { scope.ServiceProvider.GetRequiredService(handlerType); }
                catch (Exception ex) { failures.Add($"{request.Name}: {ex.GetBaseException().Message}"); }
            }
            failures.Should().BeEmpty(string.Join("\n", failures));

            scope.ServiceProvider.GetRequiredService<IRequestHandler<SetTenantModulesCommand, ApiResponse<bool>>>().Should().NotBeNull();
            scope.ServiceProvider.GetRequiredService<ITravelReminderRunner>().Should().NotBeNull();
            scope.ServiceProvider.GetRequiredService<ITenantRoleSeedService>().Should().NotBeNull();
            scope.ServiceProvider.GetRequiredService<ITenantContext>().GetRequiredTenantId().Should().Be(1);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
