using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.FatigueDetection;
using SheikhTravelSystem.Application.Features.Notifications;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Infrastructure.Services.Fatigue;

/// <summary>
/// Polls open fatigue events: after ack timeout → buzzer → second buzzer → engine stop (when enabled).
/// Device hooks: GpsDeviceCommands (buzzer, engineStop) via <see cref="IGpsAutomatedCommandService"/>.
/// Gradual speed reduction depends on tracker firmware — documented in Backend/docs/FATIGUE_DETECTION.md.
/// </summary>
public sealed class FatigueEscalationHostedService(
    IServiceProvider serviceProvider,
    IOptions<FatigueDetectionSettings> options,
    ILogger<FatigueEscalationHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(5, options.Value.EscalationPollSeconds));
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Fatigue escalation scan failed.");
                }

                try
                {
                    await Task.Delay(interval, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    private async Task ProcessAsync(CancellationToken ct)
    {
        using var scope = serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IFatigueDetectionRepository>();
        var automatedCommands = scope.ServiceProvider.GetRequiredService<IGpsAutomatedCommandService>();
        var decisionEngine = scope.ServiceProvider.GetRequiredService<INotificationDecisionEngine>();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();

        var candidates = await repository.ListEscalationCandidatesAsync(ct);
        foreach (var row in candidates)
        {
            tenantContext.SetTenant(row.TenantId);
            var settings = await repository.GetOrCreateSettingsAsync(row.TenantId, ct);
            var stageInterval = TimeSpan.FromSeconds(settings.StageIntervalSeconds);

            if (row.Status == "AwaitingAck")
            {
                await repository.AdvanceEscalationAsync(
                    row.Id,
                    1,
                    "Escalating",
                    DateTime.UtcNow.Add(stageInterval),
                    "Ack window expired — stage 1 (cab buzzer).",
                    ct);
                await QueueDeviceActionAsync(automatedCommands, repository, row, "buzzer", "Fatigue ack timeout — buzzer", ct);
                await NotifyEscalationAsync(decisionEngine, repository, row, "Stage 1: cab buzzer sent.", ct);
                continue;
            }

            var nextStage = row.EscalationStage + 1;
            if (nextStage == 2)
            {
                await repository.AdvanceEscalationAsync(
                    row.Id,
                    2,
                    "Escalating",
                    DateTime.UtcNow.Add(stageInterval),
                    "Stage 2 — repeat buzzer / in-cab warning.",
                    ct);
                await QueueDeviceActionAsync(automatedCommands, repository, row, "buzzer", "Fatigue escalation stage 2", ct);
                await NotifyEscalationAsync(decisionEngine, repository, row, "Stage 2: repeat buzzer.", ct);
                continue;
            }

            if (nextStage >= 3)
            {
                if (row.AutoStopEnabled && settings.AutoStopEnabled && row.VehicleId is int vehicleId)
                {
                    await QueueDeviceActionAsync(
                        automatedCommands, repository, row with { VehicleId = vehicleId },
                        "engineStop",
                        "Fatigue auto-stop — no driver acknowledgment",
                        ct);
                    await repository.AdvanceEscalationAsync(
                        row.Id,
                        3,
                        "AutoStopped",
                        null,
                        "Stage 3 — engine stop / relay command queued (device layer).",
                        ct);
                    await NotifyEscalationAsync(decisionEngine, repository, row, "Auto-stop command queued.", ct);
                }
                else
                {
                    await repository.AdvanceEscalationAsync(
                        row.Id,
                        3,
                        "Escalating",
                        DateTime.UtcNow.Add(stageInterval),
                        "Auto-stop disabled — admin follow-up required.",
                        ct);
                    await NotifyEscalationAsync(decisionEngine, repository, row, "Auto-stop disabled for tenant.", ct);
                }
            }
        }
    }

    private static async Task QueueDeviceActionAsync(
        IGpsAutomatedCommandService automatedCommands,
        IFatigueDetectionRepository fatigueRepo,
        FatigueEscalationCandidateRow row,
        string commandType,
        string reason,
        CancellationToken ct)
    {
        if (row.VehicleId is not int vehicleId)
            return;

        var deviceId = await fatigueRepo.GetPrimaryGpsDeviceIdForVehicleAsync(row.TenantId, vehicleId, ct);
        if (deviceId is not int gpsDeviceId)
            return;

        await automatedCommands.QueueAsync(row.TenantId, gpsDeviceId, commandType, reason, ct);
    }

    private static async Task NotifyEscalationAsync(
        INotificationDecisionEngine decisionEngine,
        IFatigueDetectionRepository repository,
        FatigueEscalationCandidateRow row,
        string detail,
        CancellationToken ct)
    {
        var driver = await repository.GetDriverContactAsync(row.TenantId, row.DriverId, ct);
        var name = driver?.FullName ?? $"Driver #{row.DriverId}";
        await decisionEngine.DispatchIfAllowedAsync(new NotificationDecisionRequest(
            "driver_fatigue",
            $"Fatigue escalation — {name}",
            detail,
            NotificationType.DriverFatigue,
            ReferenceId: row.Id,
            TenantId: row.TenantId,
            SuggestedPriority: 4,
            RequestedChannels: [NotificationChannels.InApp, NotificationChannels.Push]),
            ct);
    }
}
