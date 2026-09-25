using System.Data;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.WhatsApp.Automation;

namespace SheikhTravelSystem.Application.Features.WhatsApp.Automation;

public interface IWhatsAppAutomationTrigger
{
    /// <summary>
    /// Inserts an automation event (optionally in the caller's transaction) and enqueues after commit when tx is null.
    /// When tx is provided, caller must enqueue after successful commit via EnqueueAfterCommitAsync.
    /// </summary>
    Task<long> RaiseAsync(AutomationEventRequest request, IDbTransaction? tx = null, CancellationToken ct = default);

    Task EnqueueAfterCommitAsync(long eventId, CancellationToken ct = default);
}

public sealed record AutomationEventRequest(
    int TenantId,
    string EventType,
    int? BookingId,
    int? TripId,
    string DedupeKey,
    DateTime? DueAtUtc = null,
    string? PayloadJson = null);

public sealed class WhatsAppAutomationTrigger(
    IWhatsAppAutomationRepository repository,
    IWhatsAppWorkQueue workQueue) : IWhatsAppAutomationTrigger
{
    public async Task<long> RaiseAsync(
        AutomationEventRequest request, IDbTransaction? tx = null, CancellationToken ct = default)
    {
        var dueAt = request.DueAtUtc ?? DateTime.UtcNow;
        var id = await repository.InsertEventAsync(new AutomationEventInsert(
            request.TenantId,
            request.EventType,
            request.BookingId,
            request.TripId,
            request.DedupeKey,
            dueAt,
            request.PayloadJson), tx, ct);

        if (id > 0 && tx is null && dueAt <= DateTime.UtcNow.AddSeconds(5))
            await workQueue.EnqueueAutomationAsync(new WhatsAppAutomationWorkItem(id), ct);

        return id;
    }

    public Task EnqueueAfterCommitAsync(long eventId, CancellationToken ct = default)
        => eventId > 0
            ? workQueue.EnqueueAutomationAsync(new WhatsAppAutomationWorkItem(eventId), ct).AsTask()
            : Task.CompletedTask;
}
