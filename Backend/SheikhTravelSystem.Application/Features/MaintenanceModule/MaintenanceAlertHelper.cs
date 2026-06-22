using Dapper;

namespace SheikhTravelSystem.Application.Features.MaintenanceModule;

public static class MaintenanceAlertHelper
{
    public static async Task InsertAlertAsync(
        System.Data.IDbConnection connection, int tenantId, int? vehicleId,
        string alertType, string severity, string title, string message,
        string? refType, int? refId, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO MaintenanceAlerts (TenantId, VehicleId, AlertType, Severity, Title, Message, ReferenceType, ReferenceId)
            VALUES (@TenantId, @VehicleId, @AlertType, @Severity, @Title, @Message, @ReferenceType, @ReferenceId)
            """, new
        {
            TenantId = tenantId,
            VehicleId = vehicleId,
            AlertType = alertType,
            Severity = severity,
            Title = title,
            Message = message,
            ReferenceType = refType,
            ReferenceId = refId
        }, cancellationToken: ct));
    }
}
