using Dapper;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Fleet;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories;

public sealed class FleetRepository(IDbConnectionFactory dbFactory) : IFleetRepository
{
    public async Task<FleetDashboardDto> GetDashboardAsync(
        int tenantId,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        return await connection.QuerySingleAsync<FleetDashboardDto>(new CommandDefinition("""
            SELECT
                (SELECT COUNT(*) FROM Vehicles WHERE IsDeleted = 0 AND TenantId = @TenantId AND Status <> 5) AS TotalVehicles,
                (SELECT COUNT(*) FROM Vehicles WHERE IsDeleted = 0 AND TenantId = @TenantId AND Status IN (1, 2)) AS ActiveVehicles,
                (SELECT COUNT(*) FROM Drivers WHERE IsDeleted = 0 AND TenantId = @TenantId AND Status IN (1, 2)) AS DriversOnDuty,
                (SELECT COUNT(*) FROM Maintenance m INNER JOIN Vehicles v ON m.VehicleId = v.Id
                    WHERE m.IsDeleted = 0 AND v.TenantId = @TenantId AND m.Status IN (1, 2)) AS MaintenanceDue,
                (SELECT ISNULL(SUM(f.TotalCost), 0) FROM FuelLogs f INNER JOIN Vehicles v ON f.VehicleId = v.Id
                    WHERE f.IsDeleted = 0 AND v.TenantId = @TenantId
                      AND f.FuelDate >= DATEFROMPARTS(YEAR(GETUTCDATE()), MONTH(GETUTCDATE()), 1)) AS MonthlyFuelCost,
                (SELECT COUNT(*) FROM ComplianceDocuments WHERE IsDeleted = 0 AND TenantId = @TenantId
                    AND ExpiryDate IS NOT NULL AND ExpiryDate <= DATEADD(DAY, 30, GETUTCDATE())) AS ComplianceAlerts
            """, new { TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<ComplianceDocumentDto>> GetComplianceDocumentsAsync(
        int tenantId,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        var rows = await connection.QueryAsync<ComplianceDocumentDto>(new CommandDefinition("""
            SELECT c.Id, c.EntityType,
                   CASE WHEN c.EntityType = 'Vehicle' THEN v.Name ELSE d.FullName END AS EntityName,
                   c.DocumentType, c.DocumentNumber, c.IssuedDate, c.ExpiryDate, c.Status, c.FileUrl
            FROM ComplianceDocuments c
            LEFT JOIN Vehicles v ON c.EntityType = 'Vehicle' AND c.EntityId = v.Id
            LEFT JOIN Drivers d ON c.EntityType = 'Driver' AND c.EntityId = d.Id
            WHERE c.IsDeleted = 0 AND c.TenantId = @TenantId
            ORDER BY c.ExpiryDate ASC
            """, new { TenantId = tenantId }, cancellationToken: cancellationToken));

        return rows.ToList();
    }

    public async Task<IReadOnlyList<InspectionDto>> GetInspectionsAsync(
        int tenantId,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        var rows = await connection.QueryAsync<InspectionDto>(new CommandDefinition("""
            SELECT i.Id, v.Name AS VehicleName, i.InspectedBy, i.InspectionDate, i.Result, i.OdometerReading
            FROM Inspections i
            INNER JOIN Vehicles v ON i.VehicleId = v.Id
            WHERE i.IsDeleted = 0 AND v.TenantId = @TenantId
            ORDER BY i.InspectionDate DESC
            """, new { TenantId = tenantId }, cancellationToken: cancellationToken));

        return rows.ToList();
    }

    public async Task<IReadOnlyList<AssignmentDto>> GetAssignmentsAsync(
        int tenantId,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        var rows = await connection.QueryAsync<AssignmentDto>(new CommandDefinition("""
            SELECT a.Id, v.Name AS VehicleName, d.FullName AS DriverName,
                   a.AssignmentType, a.Status, a.StartAt, a.EndAt
            FROM AssignmentHistory a
            INNER JOIN Vehicles v ON a.VehicleId = v.Id
            LEFT JOIN Drivers d ON a.DriverId = d.Id
            WHERE a.IsDeleted = 0 AND v.TenantId = @TenantId
            ORDER BY a.StartAt DESC
            """, new { TenantId = tenantId }, cancellationToken: cancellationToken));

        return rows.ToList();
    }
}
