namespace SheikhTravelSystem.Application.Common.Interfaces;

public interface IAuditService
{
    Task LogAsync(string action, string entityName, int? entityId, CancellationToken cancellationToken = default);
}
