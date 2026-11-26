using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Reminders;

namespace SheikhTravelSystem.Infrastructure.Services.TravelNexus;

/// <summary>
/// Runs the TravelNexus reminder pass for every active tenant with the travel module, every <c>TravelNexus:Reminders:IntervalMinutes</c>
/// (default 30). A tick is skipped while the previous one is still running. Each tenant runs in its own scope with the tenant set,
/// so repository calls and notifications resolve against that tenant only.
/// </summary>
public sealed class TravelReminderHostedService(IServiceProvider serviceProvider, IConfiguration configuration, ILogger<TravelReminderHostedService> logger)
    : BackgroundService
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(60);
    private int _running;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("TravelNexus:Reminders:Enabled", true))
        {
            logger.LogInformation("TravelNexus reminders are disabled by configuration.");
            return;
        }
        var interval = TimeSpan.FromMinutes(Math.Clamp(configuration.GetValue("TravelNexus:Reminders:IntervalMinutes", 30), 5, 24 * 60));
        try { await Task.Delay(StartupDelay, stoppingToken); }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            if (Interlocked.CompareExchange(ref _running, 1, 0) == 0)
            {
                try { await RunAllTenantsAsync(stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception ex) { logger.LogError(ex, "TravelNexus reminder pass failed."); }
                finally { Interlocked.Exchange(ref _running, 0); }
            }
            else
            {
                logger.LogDebug("TravelNexus reminder pass skipped — previous tick still running.");
            }
            try { await Task.Delay(interval, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }

    private async Task RunAllTenantsAsync(CancellationToken ct)
    {
        IReadOnlyList<int> tenants;
        using (var scope = serviceProvider.CreateScope())
            tenants = await scope.ServiceProvider.GetRequiredService<ITravelReminderRepository>().GetActiveTenantIdsAsync(ct);

        foreach (var tenantId in tenants)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                using var scope = serviceProvider.CreateScope();
                var modules = scope.ServiceProvider.GetRequiredService<ITenantModuleService>();
                if (!await modules.IsModuleEnabledAsync(tenantId, TenantModuleCatalog.TravelAgencyModuleCode, ct)) continue;
                scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(tenantId);
                var result = await scope.ServiceProvider.GetRequiredService<ITravelReminderRunner>().RunTenantAsync(tenantId, ct);
                if (result.Sent + result.Failed + result.Skipped > 0)
                    logger.LogInformation("TravelNexus reminders for tenant {TenantId}: sent {Sent}, skipped {Skipped}, failed {Failed}, held {Held}",
                        tenantId, result.Sent, result.Skipped, result.Failed, result.Held);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                logger.LogError(ex, "TravelNexus reminder pass failed for tenant {TenantId}", tenantId);
            }
        }
    }
}
