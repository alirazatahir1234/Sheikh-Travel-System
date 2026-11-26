using SheikhTravelSystem.Application.Features.TravelNexus.Reminders.DTOs;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Reminders;

/// <summary>Runs one tenant's reminder pass (implemented by the Infrastructure scanner; used by the hosted service and "Run now").</summary>
public interface ITravelReminderRunner
{
    Task<TravelReminderRunResultDto> RunTenantAsync(int tenantId, CancellationToken cancellationToken);
}
