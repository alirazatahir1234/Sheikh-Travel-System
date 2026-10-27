using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.SmartOps;
using SheikhTravelSystem.Domain.Enums;
using AccidentRiskScanner = SheikhTravelSystem.Infrastructure.Services.AccidentRisk.AccidentRiskProactiveScanner;

namespace SheikhTravelSystem.Infrastructure.Services.SmartOperations;

/// <summary>
/// Evaluates deterministic Smart Ops rules A–E against tenant data and raises alerts.
/// </summary>
public class SmartOpsRuleScanner(
    IDbConnectionFactory dbFactory,
    ISmartAlertService smartAlerts,
    AccidentRiskScanner accidentRiskScanner,
    ILogger<SmartOpsRuleScanner> logger)
{
    public async Task ScanAllTenantsAsync(CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenants = (await connection.QueryAsync<int>(new CommandDefinition("""
            SELECT Id FROM Tenants WHERE IsActive = 1 AND (Status = N'Active' OR Status IS NULL)
            """, cancellationToken: cancellationToken))).ToList();

        if (tenants.Count == 0)
        {
            // Fallback: tenants that own vehicles (dev DBs without Status column values)
            tenants = (await connection.QueryAsync<int>(new CommandDefinition("""
                SELECT DISTINCT TenantId FROM Vehicles
                WHERE TenantId IS NOT NULL AND TenantId > 0 AND IsDeleted = 0
                """, cancellationToken: cancellationToken))).ToList();
        }

        foreach (var tenantId in tenants)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await ScanTenantAsync(tenantId, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Smart Ops scan failed for tenant {TenantId}", tenantId);
            }
        }
    }

    public async Task ScanTenantAsync(int tenantId, CancellationToken cancellationToken)
    {
        var asOf = DateTime.UtcNow;
        await ScanMaintenanceDueAsync(tenantId, asOf, cancellationToken);
        await ScanDocumentExpiryAsync(tenantId, asOf, cancellationToken);
        await ScanUnassignedTripsAsync(tenantId, asOf, cancellationToken);
        await ScanOverdueInvoicesAsync(tenantId, asOf, cancellationToken);
        await ScanFuelAnomaliesAsync(tenantId, asOf, cancellationToken);
        await accidentRiskScanner.ScanTenantAsync(tenantId, cancellationToken);
    }

    private async Task ScanMaintenanceDueAsync(int tenantId, DateTime asOf, CancellationToken ct)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.QueryAsync<(
            int ScheduleId, int VehicleId, string VehicleName, string ServiceTypeName,
            decimal CurrentMileage, decimal? NextDueMileage, DateTime? NextDueDate)>(
            new CommandDefinition("""
                SELECT s.Id AS ScheduleId, s.VehicleId, v.Name AS VehicleName, s.ServiceTypeName,
                       v.CurrentMileage, s.NextDueMileage, s.NextDueDate
                FROM VehicleMaintenanceSchedules s
                INNER JOIN Vehicles v ON v.Id = s.VehicleId AND v.IsDeleted = 0
                WHERE s.TenantId = @TenantId AND s.IsDeleted = 0 AND s.IsActive = 1
                  AND v.TenantId = @TenantId
                """, new { TenantId = tenantId }, cancellationToken: ct));

        foreach (var row in rows)
        {
            ct.ThrowIfCancellationRequested();
            if (!SmartOpsRuleLogic.IsMaintenanceDue(row.CurrentMileage, row.NextDueMileage, row.NextDueDate, asOf))
                continue;

            var overdue = SmartOpsRuleLogic.IsMaintenanceOverdue(
                row.CurrentMileage, row.NextDueMileage, row.NextDueDate, asOf);
            var severity = overdue ? SmartAlertSeverity.Critical : SmartAlertSeverity.Warning;
            var kmPart = row.NextDueMileage.HasValue
                ? $"mileage {row.CurrentMileage:N0}/{row.NextDueMileage:N0} km"
                : null;
            var datePart = row.NextDueDate.HasValue
                ? $"due {row.NextDueDate:yyyy-MM-dd}"
                : null;
            var detail = string.Join("; ", new[] { kmPart, datePart }.Where(x => x is not null));

            await smartAlerts.RaiseAsync(new CreateSmartAlertRequest(
                tenantId,
                SmartAlertRuleType.MaintenanceDue,
                severity,
                $"{row.ServiceTypeName} for {row.VehicleName} is {(overdue ? "overdue" : "due soon")} ({detail}).",
                "Vehicle",
                row.VehicleId,
                SmartOpsRuleLogic.BuildDailyDedupeKey(
                    SmartAlertRuleTypeNames.MaintenanceDue, "Vehicle", row.VehicleId, asOf)
                + $":sch{row.ScheduleId}"), ct);
        }
    }

    private async Task ScanDocumentExpiryAsync(int tenantId, DateTime asOf, CancellationToken ct)
    {
        using var connection = dbFactory.CreateConnection();

        // Compliance documents (Registration / Insurance / License / Permit) within 30 days
        var docs = await connection.QueryAsync<(
            int EntityId, string EntityType, string DocType, DateTime Expiry, string? DisplayName)>(
            new CommandDefinition("""
                SELECT d.EntityId, d.EntityType, d.DocumentType AS DocType, d.ExpiryDate AS Expiry,
                       CASE
                         WHEN d.EntityType = N'Vehicle' THEN v.Name
                         WHEN d.EntityType = N'Driver' THEN dr.FullName
                         ELSE NULL
                       END AS DisplayName
                FROM ComplianceDocuments d
                LEFT JOIN Vehicles v ON d.EntityType = N'Vehicle' AND v.Id = d.EntityId AND v.IsDeleted = 0
                LEFT JOIN Drivers dr ON d.EntityType = N'Driver' AND dr.Id = d.EntityId AND dr.IsDeleted = 0
                WHERE d.TenantId = @TenantId AND d.IsDeleted = 0 AND d.ExpiryDate IS NOT NULL
                  AND d.ExpiryDate <= DATEADD(DAY, @LeadDays, @AsOf)
                """, new { TenantId = tenantId, LeadDays = SmartOpsRuleLogic.DocumentExpiryLeadDays, AsOf = asOf },
                cancellationToken: ct));

        foreach (var doc in docs)
        {
            ct.ThrowIfCancellationRequested();
            var expired = doc.Expiry.Date < asOf.Date;
            var severity = expired || (doc.Expiry.Date - asOf.Date).TotalDays <= 7
                ? SmartAlertSeverity.Critical
                : SmartAlertSeverity.Warning;
            var label = doc.DisplayName ?? $"{doc.EntityType} #{doc.EntityId}";

            await smartAlerts.RaiseAsync(new CreateSmartAlertRequest(
                tenantId,
                SmartAlertRuleType.DocumentExpiry,
                severity,
                $"{doc.DocType} for {label} {(expired ? "expired" : "expires")} on {doc.Expiry:yyyy-MM-dd}.",
                doc.EntityType,
                doc.EntityId,
                SmartOpsRuleLogic.BuildDailyDedupeKey(
                    SmartAlertRuleTypeNames.DocumentExpiry, $"{doc.EntityType}:{doc.DocType}", doc.EntityId, asOf)), ct);
        }

        // Vehicle insurance column (when no ComplianceDocuments row)
        var insurance = await connection.QueryAsync<(int Id, string Name, DateTime Expiry)>(new CommandDefinition("""
            SELECT v.Id,
                   COALESCE(
                       NULLIF(LTRIM(RTRIM(CONCAT(ISNULL(v.Make, N''), N' ', ISNULL(v.Model, N'')))), N''),
                       NULLIF(LTRIM(RTRIM(v.Name)), N''),
                       v.PlateNumber,
                       CONCAT(N'Vehicle #', v.Id)) AS Name,
                   v.InsuranceExpiryDate AS Expiry
            FROM Vehicles v
            WHERE v.TenantId = @TenantId AND v.IsDeleted = 0 AND v.InsuranceExpiryDate IS NOT NULL
              AND v.InsuranceExpiryDate <= DATEADD(DAY, @LeadDays, @AsOf)
              AND NOT EXISTS (
                  SELECT 1 FROM ComplianceDocuments d
                  WHERE d.TenantId = @TenantId AND d.IsDeleted = 0
                    AND d.EntityType = N'Vehicle' AND d.EntityId = v.Id
                    AND d.DocumentType = N'Insurance' AND d.ExpiryDate IS NOT NULL)
            """, new { TenantId = tenantId, LeadDays = SmartOpsRuleLogic.DocumentExpiryLeadDays, AsOf = asOf },
            cancellationToken: ct));

        foreach (var v in insurance)
        {
            ct.ThrowIfCancellationRequested();
            var expired = v.Expiry.Date < asOf.Date;
            await smartAlerts.RaiseAsync(new CreateSmartAlertRequest(
                tenantId,
                SmartAlertRuleType.DocumentExpiry,
                expired ? SmartAlertSeverity.Critical : SmartAlertSeverity.Warning,
                $"Insurance for {v.Name} {(expired ? "expired" : "expires")} on {v.Expiry:yyyy-MM-dd}.",
                "Vehicle",
                v.Id,
                SmartOpsRuleLogic.BuildDailyDedupeKey(
                    SmartAlertRuleTypeNames.DocumentExpiry, "Vehicle:Insurance", v.Id, asOf)), ct);
        }

        // Driver licenses
        var licenses = await connection.QueryAsync<(int Id, string Name, DateTime Expiry)>(new CommandDefinition("""
            SELECT d.Id, d.FullName AS Name, d.LicenseExpiryDate AS Expiry
            FROM Drivers d
            WHERE d.TenantId = @TenantId AND d.IsDeleted = 0 AND d.IsActive = 1
              AND d.LicenseExpiryDate IS NOT NULL
              AND d.LicenseExpiryDate <= DATEADD(DAY, @LeadDays, @AsOf)
            """, new { TenantId = tenantId, LeadDays = SmartOpsRuleLogic.DocumentExpiryLeadDays, AsOf = asOf },
            cancellationToken: ct));

        foreach (var d in licenses)
        {
            ct.ThrowIfCancellationRequested();
            var expired = d.Expiry.Date < asOf.Date;
            await smartAlerts.RaiseAsync(new CreateSmartAlertRequest(
                tenantId,
                SmartAlertRuleType.DocumentExpiry,
                expired ? SmartAlertSeverity.Critical : SmartAlertSeverity.Warning,
                $"Driver license for {d.Name} {(expired ? "expired" : "expires")} on {d.Expiry:yyyy-MM-dd}.",
                "Driver",
                d.Id,
                SmartOpsRuleLogic.BuildDailyDedupeKey(
                    SmartAlertRuleTypeNames.DocumentExpiry, "Driver:License", d.Id, asOf)), ct);
        }
    }

    private async Task ScanUnassignedTripsAsync(int tenantId, DateTime asOf, CancellationToken ct)
    {
        using var connection = dbFactory.CreateConnection();
        var bookings = await connection.QueryAsync<(
            int Id, string BookingNumber, DateTime PickupTime, int? DriverId, int? VehicleId)>(
            new CommandDefinition("""
                SELECT b.Id, b.BookingNumber, b.PickupTime, b.DriverId, b.VehicleId
                FROM Bookings b
                WHERE b.TenantId = @TenantId AND b.IsDeleted = 0
                  AND b.Status = @Confirmed
                  AND b.PickupTime >= @AsOf
                  AND b.PickupTime <= DATEADD(HOUR, @LeadHours, @AsOf)
                  AND (b.DriverId IS NULL OR b.VehicleId IS NULL)
                """, new
            {
                TenantId = tenantId,
                Confirmed = (int)BookingStatus.Confirmed,
                AsOf = asOf,
                LeadHours = SmartOpsRuleLogic.UnassignedTripLeadHours
            }, cancellationToken: ct));

        foreach (var b in bookings)
        {
            ct.ThrowIfCancellationRequested();
            if (!SmartOpsRuleLogic.IsUnassignedTripImminent(
                    b.PickupTime, asOf, b.DriverId.HasValue, b.VehicleId.HasValue))
                continue;

            var missing = (b.DriverId is null, b.VehicleId is null) switch
            {
                (true, true) => "driver and vehicle",
                (true, false) => "driver",
                _ => "vehicle"
            };

            await smartAlerts.RaiseAsync(new CreateSmartAlertRequest(
                tenantId,
                SmartAlertRuleType.UnassignedTrip,
                SmartAlertSeverity.Warning,
                $"Confirmed booking {b.BookingNumber} starts at {b.PickupTime:u} and is missing {missing}.",
                "Booking",
                b.Id,
                SmartOpsRuleLogic.BuildDailyDedupeKey(
                    SmartAlertRuleTypeNames.UnassignedTrip, "Booking", b.Id, asOf)), ct);
        }
    }

    private async Task ScanOverdueInvoicesAsync(int tenantId, DateTime asOf, CancellationToken ct)
    {
        using var connection = dbFactory.CreateConnection();

        // CustomerInvoices may not exist yet on older DBs — guard with OBJECT_ID check via try.
        try
        {
            var invoices = await connection.QueryAsync<(
                int Id, string InvoiceNumber, decimal Balance, DateTime? DueDate, DateTime? IssuedAt)>(
                new CommandDefinition("""
                    SELECT Id, InvoiceNumber, Balance, DueDate, IssuedAt
                    FROM CustomerInvoices
                    WHERE TenantId = @TenantId AND IsDeleted = 0
                      AND Status IN (N'Issued', N'Pending')
                      AND Balance > 0
                    """, new { TenantId = tenantId }, cancellationToken: ct));

            foreach (var inv in invoices)
            {
                ct.ThrowIfCancellationRequested();
                if (!SmartOpsRuleLogic.IsOverdueInvoice(inv.DueDate, inv.IssuedAt, inv.Balance, asOf))
                    continue;

                var due = inv.DueDate ?? inv.IssuedAt;
                await smartAlerts.RaiseAsync(new CreateSmartAlertRequest(
                    tenantId,
                    SmartAlertRuleType.OverdueInvoice,
                    SmartAlertSeverity.Warning,
                    $"Invoice {inv.InvoiceNumber} is overdue (due {due:yyyy-MM-dd}, balance {inv.Balance:N2}).",
                    "Invoice",
                    inv.Id,
                    SmartOpsRuleLogic.BuildDailyDedupeKey(
                        SmartAlertRuleTypeNames.OverdueInvoice, "Invoice", inv.Id, asOf)), ct);
            }
        }
        catch (Exception ex) when (ex.Message.Contains("Invalid object name", StringComparison.OrdinalIgnoreCase)
                                   || ex.Message.Contains("Invalid column name", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogDebug(ex, "Overdue invoice scan skipped (schema gap) for tenant {TenantId}", tenantId);
        }
    }

    private async Task ScanFuelAnomaliesAsync(int tenantId, DateTime asOf, CancellationToken ct)
    {
        using var connection = dbFactory.CreateConnection();

        try
        {
            // Over-capacity (investigation only). Skip when TankCapacityLiters is null.
            var overCap = await connection.QueryAsync<(
                int FuelLogId, int VehicleId, string VehicleName, decimal Liters, decimal TankCapacity)>(
                new CommandDefinition("""
                    SELECT f.Id AS FuelLogId, f.VehicleId, v.Name AS VehicleName, f.Liters, v.TankCapacityLiters AS TankCapacity
                    FROM FuelLogs f
                    INNER JOIN Vehicles v ON v.Id = f.VehicleId AND v.IsDeleted = 0 AND v.TenantId = @TenantId
                    WHERE f.IsDeleted = 0 AND f.TenantId = @TenantId
                      AND v.TankCapacityLiters IS NOT NULL AND v.TankCapacityLiters > 0
                      AND f.Liters > v.TankCapacityLiters
                      AND f.FuelDate >= DATEADD(DAY, -2, @AsOf)
                    """, new { TenantId = tenantId, AsOf = asOf }, cancellationToken: ct));

            foreach (var row in overCap)
            {
                ct.ThrowIfCancellationRequested();
                await smartAlerts.RaiseAsync(new CreateSmartAlertRequest(
                    tenantId,
                    SmartAlertRuleType.FuelAnomaly,
                    SmartAlertSeverity.Warning,
                    $"Investigation: fuel entry #{row.FuelLogId} for {row.VehicleName} is {row.Liters:N1} L (tank capacity {row.TankCapacity:N1} L). Not proof of misconduct.",
                    "FuelLog",
                    row.FuelLogId,
                    SmartOpsRuleLogic.BuildFuelAnomalyDedupeKey(row.VehicleId, row.FuelLogId, "OverCapacity", asOf)), ct);
            }

            // Duplicate fill within 1 hour for same vehicle
            var dupes = await connection.QueryAsync<(
                int FuelLogId, int OtherId, int VehicleId, string VehicleName, DateTime FuelDate)>(
                new CommandDefinition("""
                    SELECT f.Id AS FuelLogId, o.Id AS OtherId, f.VehicleId, v.Name AS VehicleName, f.FuelDate
                    FROM FuelLogs f
                    INNER JOIN Vehicles v ON v.Id = f.VehicleId AND v.IsDeleted = 0 AND v.TenantId = @TenantId
                    INNER JOIN FuelLogs o ON o.VehicleId = f.VehicleId AND o.IsDeleted = 0 AND o.Id < f.Id
                      AND ABS(DATEDIFF(MINUTE, o.FuelDate, f.FuelDate)) < 60
                    WHERE f.IsDeleted = 0 AND f.TenantId = @TenantId
                      AND f.FuelDate >= DATEADD(DAY, -2, @AsOf)
                    """, new { TenantId = tenantId, AsOf = asOf }, cancellationToken: ct));

            foreach (var row in dupes)
            {
                ct.ThrowIfCancellationRequested();
                await smartAlerts.RaiseAsync(new CreateSmartAlertRequest(
                    tenantId,
                    SmartAlertRuleType.FuelAnomaly,
                    SmartAlertSeverity.Info,
                    $"Investigation: two fuel entries for {row.VehicleName} within 1 hour (#{row.OtherId} and #{row.FuelLogId}). Not proof of misconduct.",
                    "FuelLog",
                    row.FuelLogId,
                    SmartOpsRuleLogic.BuildFuelAnomalyDedupeKey(row.VehicleId, row.FuelLogId, "DuplicateHour", asOf)), ct);
            }
        }
        catch (Exception ex) when (ex.Message.Contains("Invalid column name", StringComparison.OrdinalIgnoreCase)
                                   || ex.Message.Contains("Invalid object name", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogDebug(ex, "Fuel anomaly scan skipped (schema gap) for tenant {TenantId}", tenantId);
        }
    }
}
