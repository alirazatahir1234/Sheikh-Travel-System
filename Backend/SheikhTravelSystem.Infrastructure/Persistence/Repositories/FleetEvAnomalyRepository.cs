using Dapper;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Fleet.EvAnomaly;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories;

public sealed class FleetEvAnomalyRepository(IDbConnectionFactory dbFactory) : IFleetEvAnomalyRepository
{
    private const int ElectricFuelType = (int)FuelType.Electric;

    public async Task<EvFleetDashboardDto> GetEvDashboardAsync(
        int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        var vehicles = (await connection.QueryAsync<EvFleetVehicleRowDto>(new CommandDefinition("""
            SELECT
                v.Id AS VehicleId,
                v.Name AS VehicleName,
                v.RegistrationNumber,
                vcl.FuelLevel AS BatteryPercent,
                v.ElectricRangeKm,
                CASE
                    WHEN v.ElectricRangeKm IS NOT NULL AND vcl.FuelLevel IS NOT NULL
                    THEN v.ElectricRangeKm * (vcl.FuelLevel / 100.0)
                    ELSE v.ElectricRangeKm
                END AS EstimatedRangeKm,
                vcl.Latitude,
                vcl.Longitude,
                CASE
                    WHEN vcl.FuelLevel IS NOT NULL AND vcl.FuelLevel < 20 THEN 1
                    WHEN v.ElectricRangeKm IS NOT NULL AND vcl.FuelLevel IS NOT NULL
                         AND (v.ElectricRangeKm * (vcl.FuelLevel / 100.0)) < 40 THEN 1
                    ELSE 0
                END AS RangeAlert
            FROM Vehicles v
            LEFT JOIN VehicleCurrentLocation vcl ON vcl.VehicleId = v.Id
            WHERE v.TenantId = @TenantId AND v.IsDeleted = 0 AND v.FuelType = @Electric
            ORDER BY v.Name
            """, new { TenantId = tenantId, Electric = ElectricFuelType }, cancellationToken: cancellationToken))).ToList();

        var stations = await ListChargingStationsAsync(tenantId, cancellationToken);
        var avgBattery = vehicles.Where(v => v.BatteryPercent.HasValue).Select(v => v.BatteryPercent!.Value).DefaultIfEmpty()
            .Average();

        return new EvFleetDashboardDto(
            vehicles.Count,
            vehicles.Count(v => v.RangeAlert),
            vehicles.Any(v => v.BatteryPercent.HasValue) ? Math.Round(avgBattery, 1) : null,
            vehicles,
            stations);
    }

    public async Task<IReadOnlyList<ChargingStationDto>> ListChargingStationsAsync(
        int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.QueryAsync<ChargingStationDto>(new CommandDefinition("""
            SELECT Id, Name, Latitude, Longitude, Address, ConnectorType, IsActive
            FROM ChargingStations
            WHERE TenantId = @TenantId AND IsDeleted = 0
            ORDER BY Name
            """, new { TenantId = tenantId }, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<int> UpsertChargingStationAsync(
        int tenantId, UpsertChargingStationRequest request, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        if (request.Id is > 0)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE ChargingStations
                SET Name = @Name, Latitude = @Latitude, Longitude = @Longitude,
                    Address = @Address, ConnectorType = @ConnectorType, IsActive = @IsActive,
                    UpdatedAt = GETUTCDATE()
                WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0
                """, new
            {
                request.Id,
                TenantId = tenantId,
                request.Name,
                request.Latitude,
                request.Longitude,
                request.Address,
                request.ConnectorType,
                request.IsActive
            }, cancellationToken: cancellationToken));
            return request.Id.Value;
        }

        return await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            INSERT INTO ChargingStations (TenantId, Name, Latitude, Longitude, Address, ConnectorType, IsActive)
            OUTPUT INSERTED.Id
            VALUES (@TenantId, @Name, @Latitude, @Longitude, @Address, @ConnectorType, @IsActive)
            """, new
        {
            TenantId = tenantId,
            request.Name,
            request.Latitude,
            request.Longitude,
            request.Address,
            request.ConnectorType,
            request.IsActive
        }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<FleetAnomalyEventDto>> ListAnomaliesAsync(
        int tenantId, bool unreviewedOnly, int? vehicleId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.QueryAsync<FleetAnomalyEventDto>(new CommandDefinition("""
            SELECT
                a.Id, a.VehicleId, v.Name AS VehicleName, a.DriverId, d.FullName AS DriverName,
                a.AnomalyType, a.Severity, a.Latitude, a.Longitude, a.SpeedKmh, a.Details,
                a.IsReviewed, a.CreatedAtUtc, a.ReviewNotes
            FROM FleetAnomalyEvents a
            INNER JOIN Vehicles v ON v.Id = a.VehicleId
            LEFT JOIN Drivers d ON d.Id = a.DriverId
            WHERE a.TenantId = @TenantId AND a.IsDeleted = 0
              AND (@UnreviewedOnly = 0 OR a.IsReviewed = 0)
              AND (@VehicleId IS NULL OR a.VehicleId = @VehicleId)
            ORDER BY a.CreatedAtUtc DESC
            """, new { TenantId = tenantId, UnreviewedOnly = unreviewedOnly ? 1 : 0, VehicleId = vehicleId },
            cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<bool> ReviewAnomalyAsync(
        int tenantId, long anomalyId, string reviewedBy, string? notes, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var affected = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE FleetAnomalyEvents
            SET IsReviewed = 1, ReviewedAt = GETUTCDATE(), ReviewedBy = @ReviewedBy, ReviewNotes = @Notes
            WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0
            """, new { Id = anomalyId, TenantId = tenantId, ReviewedBy = reviewedBy, Notes = notes },
            cancellationToken: cancellationToken));
        return affected > 0;
    }

    public async Task<long> InsertAnomalyAsync(
        FleetAnomalyInsert insert, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<long>(new CommandDefinition("""
            INSERT INTO FleetAnomalyEvents
                (TenantId, VehicleId, DriverId, AnomalyType, Severity, Latitude, Longitude, SpeedKmh, Details)
            OUTPUT INSERTED.Id
            VALUES
                (@TenantId, @VehicleId, @DriverId, @AnomalyType, @Severity, @Latitude, @Longitude, @SpeedKmh, @Details)
            """, insert, cancellationToken: cancellationToken));
    }

    public async Task<DriverEvStatusDto?> GetDriverEvStatusAsync(
        int tenantId, int driverId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var vehicle = await connection.QuerySingleOrDefaultAsync<(int Id, string Name, decimal? RangeKm, decimal? Battery)>(
            new CommandDefinition("""
                SELECT TOP 1 v.Id, v.Name, v.ElectricRangeKm, vcl.FuelLevel
                FROM AssignmentHistory ah
                INNER JOIN Vehicles v ON v.Id = ah.VehicleId
                LEFT JOIN VehicleCurrentLocation vcl ON vcl.VehicleId = v.Id
                WHERE ah.DriverId = @DriverId AND ah.TenantId = @TenantId AND ah.IsDeleted = 0
                  AND ah.Status = N'Active' AND v.FuelType = @Electric AND v.IsDeleted = 0
                ORDER BY ah.StartAt DESC
                """, new { DriverId = driverId, TenantId = tenantId, Electric = ElectricFuelType },
                cancellationToken: cancellationToken));

        if (vehicle.Id <= 0)
            return null;

        var estimated = vehicle.RangeKm.HasValue && vehicle.Battery.HasValue
            ? vehicle.RangeKm * (vehicle.Battery / 100m)
            : vehicle.RangeKm;
        var rangeAlert = vehicle.Battery is < 20
            || (estimated.HasValue && estimated < 40);

        var stations = (await ListChargingStationsAsync(tenantId, cancellationToken)).Take(12).ToList();

        return new DriverEvStatusDto(
            vehicle.Id,
            vehicle.Name,
            vehicle.Battery,
            vehicle.RangeKm,
            estimated,
            rangeAlert,
            stations);
    }

    public async Task<IReadOnlyList<VehicleDocumentTrackerItemDto>> GetVehicleDocumentTrackerAsync(
        int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var fromCompliance = await connection.QueryAsync<VehicleDocumentTrackerItemDto>(new CommandDefinition("""
            SELECT c.Id AS ComplianceDocumentId, c.EntityId AS VehicleId, v.Name AS VehicleName,
                   c.DocumentType, c.ExpiryDate, c.Status, N'ComplianceDocuments' AS Source
            FROM ComplianceDocuments c
            INNER JOIN Vehicles v ON v.Id = c.EntityId
            WHERE c.IsDeleted = 0 AND c.TenantId = @TenantId AND c.EntityType = N'Vehicle'
            """, new { TenantId = tenantId }, cancellationToken: cancellationToken));

        var fromVehicleColumns = await connection.QueryAsync<VehicleDocumentTrackerItemDto>(new CommandDefinition("""
            SELECT NULL AS ComplianceDocumentId, v.Id AS VehicleId, v.Name AS VehicleName,
                   dt.DocumentType, dt.ExpiryDate,
                   CASE
                       WHEN dt.ExpiryDate IS NULL THEN N'Unknown'
                       WHEN dt.ExpiryDate < CAST(GETUTCDATE() AS DATE) THEN N'Expired'
                       WHEN dt.ExpiryDate <= DATEADD(DAY, 30, CAST(GETUTCDATE() AS DATE)) THEN N'Expiring'
                       ELSE N'Valid'
                   END AS Status,
                   N'Vehicle' AS Source
            FROM Vehicles v
            CROSS APPLY (VALUES
                (N'Insurance', v.InsuranceExpiryDate),
                (N'Registration', v.RegistrationExpiryDate),
                (N'Fitness Certificate', v.FitnessCertificateExpiryDate)
            ) dt(DocumentType, ExpiryDate)
            WHERE v.TenantId = @TenantId AND v.IsDeleted = 0 AND dt.ExpiryDate IS NOT NULL
            """, new { TenantId = tenantId }, cancellationToken: cancellationToken));

        return fromCompliance.Concat(fromVehicleColumns)
            .OrderBy(r => r.ExpiryDate ?? DateTime.MaxValue)
            .ToList();
    }

    public async Task<int> UpsertComplianceDocumentAsync(
        int tenantId, UpsertComplianceDocumentRequest request, string? actor, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var status = StatusFor(request.ExpiryDate);

        if (request.Id is > 0)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE ComplianceDocuments
                SET DocumentType = @DocumentType, DocumentNumber = @DocumentNumber,
                    IssuedDate = @IssuedDate, ExpiryDate = @ExpiryDate, FileUrl = @FileUrl,
                    Notes = @Notes, Status = @Status, UpdatedAt = GETUTCDATE(), UpdatedBy = @Actor
                WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0
                """, new
            {
                request.Id,
                TenantId = tenantId,
                request.DocumentType,
                request.DocumentNumber,
                request.IssuedDate,
                request.ExpiryDate,
                request.FileUrl,
                request.Notes,
                Status = status,
                Actor = actor
            }, cancellationToken: cancellationToken));
            return request.Id.Value;
        }

        return await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            INSERT INTO ComplianceDocuments
                (TenantId, EntityType, EntityId, DocumentType, DocumentNumber, IssuedDate, ExpiryDate, Status, FileUrl, Notes, CreatedBy)
            OUTPUT INSERTED.Id
            VALUES
                (@TenantId, N'Vehicle', @VehicleId, @DocumentType, @DocumentNumber, @IssuedDate, @ExpiryDate, @Status, @FileUrl, @Notes, @Actor)
            """, new
        {
            TenantId = tenantId,
            request.VehicleId,
            request.DocumentType,
            request.DocumentNumber,
            request.IssuedDate,
            request.ExpiryDate,
            Status = status,
            request.FileUrl,
            request.Notes,
            Actor = actor
        }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<VehicleDocumentExpiryDto>> GetDriverVehicleDocumentExpiriesAsync(
        int tenantId, int driverId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var vehicleId = await connection.ExecuteScalarAsync<int?>(new CommandDefinition("""
            SELECT TOP 1 ah.VehicleId
            FROM AssignmentHistory ah
            WHERE ah.DriverId = @DriverId AND ah.TenantId = @TenantId AND ah.IsDeleted = 0 AND ah.Status = N'Active'
            ORDER BY ah.StartAt DESC
            """, new { DriverId = driverId, TenantId = tenantId }, cancellationToken: cancellationToken));

        if (!vehicleId.HasValue)
            return [];

        var tracker = await GetVehicleDocumentTrackerAsync(tenantId, cancellationToken);
        return tracker
            .Where(t => t.VehicleId == vehicleId.Value && t.ExpiryDate.HasValue)
            .Select(t =>
            {
                var days = (int)Math.Ceiling((t.ExpiryDate!.Value.Date - DateTime.UtcNow.Date).TotalDays);
                return new VehicleDocumentExpiryDto(t.DocumentType, t.ExpiryDate, days, t.Status);
            })
            .ToList();
    }

    private static string StatusFor(DateTime? expiry)
    {
        if (!expiry.HasValue) return "Unknown";
        var today = DateTime.UtcNow.Date;
        if (expiry.Value.Date < today) return "Expired";
        if (expiry.Value.Date <= today.AddDays(30)) return "Expiring";
        return "Valid";
    }
}
