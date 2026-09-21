using System.Data;
using Dapper;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Assignments;
using SheikhTravelSystem.Application.Features.Drivers;
using SheikhTravelSystem.Domain.Enums;
using SheikhTravelSystem.Infrastructure.Persistence.Repositories.Sql;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories;

public sealed class AssignmentRepository(IDbConnectionFactory dbFactory) : IAssignmentRepository
{
    public async Task<AssignmentValidationResultDto> ValidateAsync(
        int tenantId,
        ValidateAssignmentRequest request,
        int? excludeAssignmentId = null,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var issues = new List<AssignmentValidationIssueDto>();

        try
        {
            await DriverAssignmentOps.EnsureVehicleAssignableAsync(
                connection, tenantId, request.VehicleId, cancellationToken);
        }
        catch (ConflictException ex)
        {
            issues.Add(new("VehicleInvalid", ex.Message, "Error"));
        }
        catch (NotFoundException ex)
        {
            issues.Add(new("VehicleNotFound", ex.Message, "Error"));
        }

        var driver = await connection.QuerySingleOrDefaultAsync<(string? VerificationStatus, DateTime LicenseExpiry, bool IsActive, int Status, string FullName)>(
            new CommandDefinition(
                "SELECT VerificationStatus, LicenseExpiryDate, IsActive, Status, FullName FROM Drivers WHERE Id = @DriverId AND TenantId = @TenantId AND IsDeleted = 0",
                new { request.DriverId, TenantId = tenantId },
                cancellationToken: cancellationToken));

        if (driver.VerificationStatus is null)
        {
            issues.Add(new("DriverNotFound", $"Driver {request.DriverId} was not found.", "Error"));
        }
        else
        {
            try
            {
                DriverAssignmentGuard.EnsureAssignable(
                    driver.IsActive, (DriverStatus)driver.Status, driver.VerificationStatus, driver.LicenseExpiry);
            }
            catch (ConflictException ex)
            {
                issues.Add(new("DriverInvalid", ex.Message, "Error"));
            }

            if (!request.SkipSoftWarnings
                && driver.LicenseExpiry.Date >= DateTime.UtcNow.Date
                && driver.LicenseExpiry.Date <= DateTime.UtcNow.Date.AddDays(30))
            {
                var days = (driver.LicenseExpiry.Date - DateTime.UtcNow.Date).Days;
                issues.Add(new("LicenseExpiring", $"Driver license expires in {days} days.", "Warning"));
            }
        }

        try
        {
            await DriverAssignmentOps.EnsureDriverNotOnActiveTripAsync(
                connection, tenantId, request.DriverId, cancellationToken);
        }
        catch (ConflictException ex)
        {
            issues.Add(new("DriverOnTrip", ex.Message, "Error"));
        }

        try
        {
            await DriverAssignmentOps.EnsureVehicleAvailableForDriverAsync(
                connection, tenantId, request.VehicleId, request.DriverId, excludeAssignmentId, cancellationToken);
        }
        catch (ConflictException ex)
        {
            issues.Add(new("VehicleConflict", ex.Message, "Error"));
        }

        var driverAssigned = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                @"SELECT CASE WHEN EXISTS(
                    SELECT 1 FROM AssignmentHistory
                    WHERE DriverId = @DriverId AND TenantId = @TenantId
                      AND Status IN (N'Active', N'Scheduled') AND IsDeleted = 0
                      AND (@ExcludeId IS NULL OR Id <> @ExcludeId)
                      AND VehicleId <> @VehicleId
                  ) THEN 1 ELSE 0 END",
                new { request.DriverId, TenantId = tenantId, request.VehicleId, ExcludeId = excludeAssignmentId },
                cancellationToken: cancellationToken));

        if (driverAssigned)
            issues.Add(new("DriverConflict", "Driver already has another active assignment.", "Error"));

        if (!request.SkipSoftWarnings)
        {
            var maintenanceDue = await connection.ExecuteScalarAsync<bool>(
                new CommandDefinition(
                    @"SELECT CASE WHEN EXISTS(
                        SELECT 1 FROM Maintenance m
                        INNER JOIN Vehicles v ON v.Id = m.VehicleId
                        WHERE m.VehicleId = @VehicleId AND v.TenantId = @TenantId AND m.IsDeleted = 0
                          AND m.Status IN (1, 2)
                          AND m.MaintenanceDate <= DATEADD(day, 1, CAST(GETUTCDATE() AS DATE))
                      ) THEN 1 ELSE 0 END",
                    new { request.VehicleId, TenantId = tenantId },
                    cancellationToken: cancellationToken));

            if (maintenanceDue)
                issues.Add(new("MaintenanceDue", "Vehicle has maintenance scheduled within 24 hours.", "Warning"));
        }

        var canProceed = !issues.Any(i => i.Severity == "Error");
        return new AssignmentValidationResultDto(canProceed, issues);
    }

    public async Task<PagedResult<AssignmentListItemDto>> ListAsync(
        int tenantId, int page, int pageSize, string? search, string? status, string? assignmentType,
        int? vehicleId, int? driverId, int? branchId, int? departmentId, DateTime? dateFrom, DateTime? dateTo,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var offset = (Math.Max(1, page) - 1) * pageSize;
        var filter = BuildFilter(tenantId, search, status, assignmentType, vehicleId, driverId, branchId, departmentId, dateFrom, dateTo);

        var countSql = $"""
            SELECT COUNT(*)
            {AssignmentSql.ListFrom}
            WHERE {filter.Condition}
            """;

        var dataSql = $"""
            SELECT {AssignmentSql.ListSelect}
            {AssignmentSql.ListFrom}
            WHERE {filter.Condition}
            ORDER BY a.StartAt DESC
            OFFSET {offset} ROWS FETCH NEXT {pageSize} ROWS ONLY
            """;

        var totalCount = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(countSql, filter.Params, cancellationToken: cancellationToken));

        var rows = await connection.QueryAsync<AssignmentListItemDto>(
            new CommandDefinition(dataSql, filter.Params, cancellationToken: cancellationToken));

        return new PagedResult<AssignmentListItemDto>
        {
            Items = rows.ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<AssignmentStatsDto> GetStatsAsync(int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        return await connection.QuerySingleAsync<AssignmentStatsDto>(new CommandDefinition("""
            SELECT
                (SELECT COUNT(*) FROM AssignmentHistory WHERE IsDeleted = 0 AND TenantId = @TenantId) AS TotalAssignments,
                (SELECT COUNT(*) FROM AssignmentHistory WHERE IsDeleted = 0 AND TenantId = @TenantId AND Status IN (N'Active', N'Scheduled')) AS ActiveAssignments,
                (SELECT COUNT(*) FROM AssignmentHistory WHERE IsDeleted = 0 AND TenantId = @TenantId AND Status = N'Completed') AS CompletedAssignments,
                (SELECT COUNT(*) FROM AssignmentHistory WHERE IsDeleted = 0 AND TenantId = @TenantId AND Status = N'Cancelled') AS CancelledAssignments,
                (SELECT COUNT(*) FROM Vehicles WHERE IsDeleted = 0 AND TenantId = @TenantId AND Status <> 5
                    AND Id NOT IN (SELECT VehicleId FROM AssignmentHistory WHERE IsDeleted = 0 AND TenantId = @TenantId AND Status IN (N'Active', N'Scheduled'))) AS UnassignedVehicles,
                (SELECT COUNT(*) FROM Vehicles WHERE IsDeleted = 0 AND TenantId = @TenantId AND Status = 1
                    AND Id NOT IN (SELECT VehicleId FROM AssignmentHistory WHERE IsDeleted = 0 AND TenantId = @TenantId AND Status IN (N'Active', N'Scheduled'))) AS AvailableVehicles,
                (SELECT COUNT(*) FROM Drivers WHERE IsDeleted = 0 AND TenantId = @TenantId AND IsActive = 1 AND Status = 1
                    AND Id NOT IN (SELECT DriverId FROM AssignmentHistory WHERE IsDeleted = 0 AND TenantId = @TenantId AND Status IN (N'Active', N'Scheduled') AND DriverId IS NOT NULL)) AS AvailableDrivers,
                (SELECT COUNT(*) FROM Drivers WHERE IsDeleted = 0 AND TenantId = @TenantId AND IsActive = 1
                    AND LicenseExpiryDate IS NOT NULL AND LicenseExpiryDate <= DATEADD(DAY, 30, GETUTCDATE())) AS ExpiringLicenses,
                (SELECT COUNT(*) FROM Bookings WHERE IsDeleted = 0 AND TenantId = @TenantId AND Status = @Started) AS OngoingTrips,
                (SELECT COUNT(*) FROM AssignmentHistory WHERE IsDeleted = 0 AND TenantId = @TenantId
                    AND (Status = N'Scheduled' OR (Status = N'Active' AND StartAt BETWEEN GETUTCDATE() AND DATEADD(hour, 24, GETUTCDATE())))) AS UpcomingAssignments,
                (SELECT COUNT(*) FROM AssignmentHistory WHERE IsDeleted = 0 AND TenantId = @TenantId
                    AND Status = N'Active' AND EndAt IS NOT NULL AND EndAt < GETUTCDATE()) AS OverdueReturns,
                (SELECT COUNT(*) FROM (
                    SELECT v.Id FROM Vehicles v WHERE v.IsDeleted = 0 AND v.TenantId = @TenantId
                      AND v.InsuranceExpiryDate IS NOT NULL AND v.InsuranceExpiryDate < CAST(GETUTCDATE() AS DATE)
                    UNION
                    SELECT d.Id FROM Drivers d WHERE d.IsDeleted = 0 AND d.TenantId = @TenantId
                      AND d.LicenseExpiryDate < CAST(GETUTCDATE() AS DATE)
                ) x) AS ExpiredDocuments,
                (SELECT COUNT(*) FROM Drivers WHERE IsDeleted = 0 AND TenantId = @TenantId AND Status = @OnLeave) AS DriversOnLeave,
                (SELECT COUNT(*) FROM Vehicles WHERE IsDeleted = 0 AND TenantId = @TenantId AND Status = @Maintenance) AS VehiclesUnderMaintenance,
                CAST(CASE WHEN (SELECT COUNT(*) FROM Vehicles WHERE IsDeleted = 0 AND TenantId = @TenantId AND Status <> 5) = 0 THEN 0
                    ELSE (SELECT COUNT(DISTINCT VehicleId) * 100.0 / NULLIF((SELECT COUNT(*) FROM Vehicles WHERE IsDeleted = 0 AND TenantId = @TenantId AND Status <> 5), 0)
                          FROM AssignmentHistory WHERE IsDeleted = 0 AND TenantId = @TenantId AND Status IN (N'Active', N'Scheduled'))
                END AS DECIMAL(5,2)) AS AssignmentUtilizationPct
            """, new
        {
            TenantId = tenantId,
            Started = (int)BookingStatus.Started,
            OnLeave = (int)DriverStatus.OnLeave,
            Maintenance = (int)VehicleStatus.Maintenance
        }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<AssignmentChangelogDto>> GetChangelogAsync(
        int tenantId, int assignmentId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        var rows = await connection.QueryAsync<AssignmentChangelogDto>(new CommandDefinition("""
            SELECT
                c.Id, c.ActionType,
                c.OldVehicleId, ov.Name AS OldVehicleName,
                c.NewVehicleId, nv.Name AS NewVehicleName,
                c.OldDriverId, od.FullName AS OldDriverName,
                c.NewDriverId, nd.FullName AS NewDriverName,
                c.Reason, c.CreatedBy, c.CreatedAt
            FROM FleetAssignmentChangelog c
            INNER JOIN AssignmentHistory a ON c.AssignmentId = a.Id
            INNER JOIN Vehicles av ON a.VehicleId = av.Id
            LEFT JOIN Vehicles ov ON c.OldVehicleId = ov.Id
            LEFT JOIN Vehicles nv ON c.NewVehicleId = nv.Id
            LEFT JOIN Drivers od ON c.OldDriverId = od.Id
            LEFT JOIN Drivers nd ON c.NewDriverId = nd.Id
            WHERE c.AssignmentId = @AssignmentId
              AND av.TenantId = @TenantId
            ORDER BY c.CreatedAt DESC
            """, new { AssignmentId = assignmentId, TenantId = tenantId }, cancellationToken: cancellationToken));

        return rows.ToList();
    }

    public async Task<IReadOnlyList<AssignmentCalendarItemDto>> GetCalendarAsync(
        int tenantId, DateTime from, DateTime to, int? branchId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        var where = "a.IsDeleted = 0 AND v.TenantId = @TenantId AND a.StartAt <= @To AND (a.EndAt IS NULL OR a.EndAt >= @From)";
        if (branchId.HasValue)
            where += " AND (v.BranchId = @BranchId OR d.BranchId = @BranchId)";

        var rows = await connection.QueryAsync<AssignmentCalendarItemDto>(new CommandDefinition($"""
            SELECT a.Id,
                ISNULL(a.AssignmentNo, CONCAT(N'ASN-', RIGHT(CONCAT(N'000000', CAST(a.Id AS NVARCHAR)), 6))) AS AssignmentNo,
                a.VehicleId, v.Name AS VehicleName, a.DriverId, d.FullName AS DriverName,
                a.Status, a.StartAt, a.EndAt, a.AssignmentType
            FROM AssignmentHistory a
            INNER JOIN Vehicles v ON a.VehicleId = v.Id
            LEFT JOIN Drivers d ON a.DriverId = d.Id
            WHERE {where}
            ORDER BY a.StartAt
            """, new { TenantId = tenantId, From = from, To = to, BranchId = branchId }, cancellationToken: cancellationToken));

        return rows.ToList();
    }

    public async Task<AssignmentUtilizationReportDto> GetUtilizationReportAsync(
        int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        return await connection.QuerySingleAsync<AssignmentUtilizationReportDto>(new CommandDefinition("""
            SELECT
                (SELECT COUNT(*) FROM Vehicles WHERE IsDeleted = 0 AND TenantId = @TenantId AND Status <> 5) AS TotalVehicles,
                (SELECT COUNT(DISTINCT VehicleId) FROM AssignmentHistory WHERE IsDeleted = 0 AND TenantId = @TenantId AND Status IN (N'Active', N'Scheduled')) AS AssignedVehicles,
                CAST(CASE WHEN (SELECT COUNT(*) FROM Vehicles WHERE IsDeleted = 0 AND TenantId = @TenantId AND Status <> 5) = 0 THEN 0
                    ELSE (SELECT COUNT(DISTINCT VehicleId) * 100.0 / NULLIF((SELECT COUNT(*) FROM Vehicles WHERE IsDeleted = 0 AND TenantId = @TenantId AND Status <> 5), 0)
                          FROM AssignmentHistory WHERE IsDeleted = 0 AND TenantId = @TenantId AND Status IN (N'Active', N'Scheduled'))
                END AS DECIMAL(5,2)) AS UtilizationPct,
                (SELECT COUNT(*) FROM Drivers WHERE IsDeleted = 0 AND TenantId = @TenantId AND IsActive = 1) AS TotalDrivers,
                (SELECT COUNT(DISTINCT DriverId) FROM AssignmentHistory WHERE IsDeleted = 0 AND TenantId = @TenantId AND Status IN (N'Active', N'Scheduled') AND DriverId IS NOT NULL) AS AssignedDrivers,
                CAST(CASE WHEN (SELECT COUNT(*) FROM Drivers WHERE IsDeleted = 0 AND TenantId = @TenantId AND IsActive = 1) = 0 THEN 0
                    ELSE (SELECT COUNT(DISTINCT DriverId) * 100.0 / NULLIF((SELECT COUNT(*) FROM Drivers WHERE IsDeleted = 0 AND TenantId = @TenantId AND IsActive = 1), 0)
                          FROM AssignmentHistory WHERE IsDeleted = 0 AND TenantId = @TenantId AND Status IN (N'Active', N'Scheduled') AND DriverId IS NOT NULL)
                END AS DECIMAL(5,2)) AS DriverUtilizationPct,
                (SELECT COUNT(*) FROM AssignmentHistory WHERE IsDeleted = 0 AND TenantId = @TenantId AND Status IN (N'Active', N'Scheduled')) AS ActiveAssignments,
                (SELECT COUNT(*) FROM AssignmentHistory WHERE IsDeleted = 0 AND TenantId = @TenantId AND Status = N'Completed'
                    AND EndAt >= DATEADD(month, DATEDIFF(month, 0, GETUTCDATE()), 0)) AS CompletedThisMonth
            """, new { TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    public async Task<int> CreateAsync(
        int tenantId, CreateAssignmentRequest body, string createdBy, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        var validation = await ValidateAsync(tenantId,
            new ValidateAssignmentRequest(body.VehicleId, body.DriverId, body.StartDate, body.AssignmentType),
            cancellationToken: cancellationToken);

        if (!validation.CanProceed)
            throw new ConflictException(string.Join(" ", validation.Issues.Where(i => i.Severity == "Error").Select(i => i.Message)));

        DriverAssignmentOps.OpenConnection(connection);
        using var transaction = connection.BeginTransaction();
        try
        {
            await DriverAssignmentOps.CompleteActiveAssignmentsAsync(
                connection, tenantId, body.DriverId, body.VehicleId, transaction, cancellationToken);

            var startAt = body.StartDate.ToUniversalTime();
            var status = AssignmentValidation.ResolveInitialStatus(startAt, body.AssignmentType.Trim());
            var assignmentType = body.AssignmentType.Trim();
            var purpose = string.IsNullOrWhiteSpace(body.Purpose) ? assignmentType : body.Purpose.Trim();

            var assignmentId = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                @"INSERT INTO AssignmentHistory
                    (TenantId, VehicleId, DriverId, BookingId, AssignmentType, Status, StartAt, EndAt,
                     Purpose, PickupLocation, DropLocation, OdometerStart, Reason, Notes, CreatedBy, CreatedAt, IsDeleted)
                  VALUES
                    (@TenantId, @VehicleId, @DriverId, @BookingId, @AssignmentType, @Status, @StartAt, @EndAt,
                     @Purpose, @PickupLocation, @DropLocation, @OdometerStart, @Reason, @Notes, @CreatedBy, GETUTCDATE(), 0);
                  SELECT CAST(SCOPE_IDENTITY() AS INT);",
                new
                {
                    TenantId = tenantId,
                    body.VehicleId,
                    body.DriverId,
                    body.BookingId,
                    AssignmentType = assignmentType,
                    Status = status,
                    StartAt = startAt,
                    EndAt = body.EndDate?.ToUniversalTime(),
                    Purpose = purpose,
                    PickupLocation = NullIfEmpty(body.PickupLocation),
                    DropLocation = NullIfEmpty(body.DropLocation),
                    body.OdometerStart,
                    Reason = NullIfEmpty(body.Reason),
                    Notes = NullIfEmpty(body.Notes),
                    CreatedBy = createdBy
                }, transaction: transaction, cancellationToken: cancellationToken));

            await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE AssignmentHistory SET AssignmentNo = @No WHERE Id = @Id",
                new { No = $"ASN-{assignmentId:D6}", Id = assignmentId },
                transaction: transaction, cancellationToken: cancellationToken));

            await WriteChangelogAsync(connection, tenantId, assignmentId,
                null, body.VehicleId, null, body.DriverId,
                status == "PendingApproval" ? "Submitted" : "Created",
                body.Reason, createdBy, cancellationToken, transaction);

            transaction.Commit();
            return assignmentId;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<int> TransferAsync(
        int tenantId, int assignmentId, TransferAssignmentRequest body, string modifiedBy,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var transferType = body.TransferType.Trim();

        var existing = await connection.QuerySingleOrDefaultAsync<(int Id, int VehicleId, int? DriverId, string Status, string AssignmentType)>(
            new CommandDefinition(
                @"SELECT a.Id, a.VehicleId, a.DriverId, a.Status, a.AssignmentType
                  FROM AssignmentHistory a INNER JOIN Vehicles v ON a.VehicleId = v.Id
                  WHERE a.Id = @Id AND v.TenantId = @TenantId AND a.IsDeleted = 0",
                new { Id = assignmentId, TenantId = tenantId }, cancellationToken: cancellationToken));

        if (existing.Id == 0) throw new NotFoundException("Assignment", assignmentId);
        if (!AssignmentValidation.IsOpenStatus(existing.Status))
            throw new ConflictException("Only open assignments can be transferred.");

        var newVehicleId = body.NewVehicleId ?? existing.VehicleId;
        var newDriverId = body.NewDriverId ?? existing.DriverId
            ?? throw new ConflictException("Assignment has no driver to transfer.");

        if (transferType.Equals("Vehicle", StringComparison.OrdinalIgnoreCase) && !body.NewVehicleId.HasValue)
            throw new ConflictException("New vehicle is required for vehicle transfer.");
        if (transferType.Equals("Driver", StringComparison.OrdinalIgnoreCase) && !body.NewDriverId.HasValue)
            throw new ConflictException("New driver is required for driver transfer.");

        var skipSoft = transferType.Equals("Emergency", StringComparison.OrdinalIgnoreCase);
        var validation = await ValidateAsync(tenantId,
            new ValidateAssignmentRequest(newVehicleId, newDriverId, DateTime.UtcNow, existing.AssignmentType, skipSoft),
            assignmentId, cancellationToken);

        if (!validation.CanProceed)
            throw new ConflictException(string.Join(" ", validation.Issues.Where(i => i.Severity == "Error").Select(i => i.Message)));

        DriverAssignmentOps.OpenConnection(connection);
        using var transaction = connection.BeginTransaction();
        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                @"UPDATE AssignmentHistory SET Status = N'Completed', EndAt = GETUTCDATE(), ModifiedAt = GETUTCDATE(), ModifiedBy = @ModifiedBy
                  WHERE Id = @Id",
                new { Id = assignmentId, ModifiedBy = modifiedBy },
                transaction: transaction, cancellationToken: cancellationToken));

            var newId = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                @"INSERT INTO AssignmentHistory
                    (TenantId, VehicleId, DriverId, AssignmentType, Status, StartAt, TransferType,
                     Reason, Notes, CreatedBy, CreatedAt, IsDeleted)
                  VALUES
                    (@TenantId, @VehicleId, @DriverId, @AssignmentType, N'Active', GETUTCDATE(), @TransferType,
                     @Reason, @Notes, @CreatedBy, GETUTCDATE(), 0);
                  SELECT CAST(SCOPE_IDENTITY() AS INT);",
                new
                {
                    TenantId = tenantId,
                    VehicleId = newVehicleId,
                    DriverId = newDriverId,
                    AssignmentType = transferType.Equals("Temporary", StringComparison.OrdinalIgnoreCase) ? "Temporary" : "Transfer",
                    TransferType = transferType,
                    Reason = NullIfEmpty(body.Reason),
                    Notes = NullIfEmpty(body.Notes),
                    CreatedBy = modifiedBy
                }, transaction: transaction, cancellationToken: cancellationToken));

            await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE AssignmentHistory SET AssignmentNo = @No WHERE Id = @Id",
                new { No = $"ASN-{newId:D6}", Id = newId },
                transaction: transaction, cancellationToken: cancellationToken));

            await WriteChangelogAsync(connection, tenantId, newId,
                existing.VehicleId, newVehicleId, existing.DriverId, newDriverId,
                "Transferred", body.Reason, modifiedBy, cancellationToken, transaction);

            transaction.Commit();
            return newId;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task CompleteAsync(
        int tenantId, int assignmentId, CompleteAssignmentRequest body, string? modifiedBy,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        var existing = await connection.QuerySingleOrDefaultAsync<(int Id, int VehicleId, int? DriverId, string Status)>(
            new CommandDefinition(
                @"SELECT a.Id, a.VehicleId, a.DriverId, a.Status
                  FROM AssignmentHistory a INNER JOIN Vehicles v ON a.VehicleId = v.Id
                  WHERE a.Id = @Id AND v.TenantId = @TenantId AND a.IsDeleted = 0",
                new { Id = assignmentId, TenantId = tenantId }, cancellationToken: cancellationToken));

        if (existing.Id == 0) throw new NotFoundException("Assignment", assignmentId);
        if (!AssignmentValidation.IsOpenStatus(existing.Status))
            throw new ConflictException("Only open assignments can be completed.");

        await connection.ExecuteAsync(new CommandDefinition(
            @"UPDATE AssignmentHistory SET Status = N'Completed', EndAt = GETUTCDATE(),
              OdometerEnd = ISNULL(@OdometerEnd, OdometerEnd),
              Reason = ISNULL(@Reason, Reason), ModifiedAt = GETUTCDATE(), ModifiedBy = @ModifiedBy
              WHERE Id = @Id",
            new
            {
                Id = assignmentId,
                body.OdometerEnd,
                Reason = NullIfEmpty(body.Reason),
                ModifiedBy = modifiedBy
            },
            cancellationToken: cancellationToken));

        await WriteChangelogAsync(connection, tenantId, assignmentId,
            existing.VehicleId, null, existing.DriverId, null,
            "Completed", body.Reason, modifiedBy, cancellationToken);
    }

    public async Task CancelAsync(
        int tenantId, int assignmentId, CancelAssignmentRequest body, string? modifiedBy,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        var existing = await connection.QuerySingleOrDefaultAsync<(int Id, int VehicleId, string Status)>(
            new CommandDefinition(
                @"SELECT a.Id, a.VehicleId, a.Status
                  FROM AssignmentHistory a INNER JOIN Vehicles v ON a.VehicleId = v.Id
                  WHERE a.Id = @Id AND v.TenantId = @TenantId AND a.IsDeleted = 0",
                new { Id = assignmentId, TenantId = tenantId }, cancellationToken: cancellationToken));

        if (existing.Id == 0) throw new NotFoundException("Assignment", assignmentId);
        if (existing.Status is "Completed" or "Cancelled")
            throw new ConflictException("Assignment is already closed.");

        await connection.ExecuteAsync(new CommandDefinition(
            @"UPDATE AssignmentHistory SET Status = N'Cancelled', EndAt = GETUTCDATE(),
              Reason = ISNULL(@Reason, Reason), ModifiedAt = GETUTCDATE(), ModifiedBy = @ModifiedBy
              WHERE Id = @Id",
            new { Id = assignmentId, Reason = NullIfEmpty(body.Reason), ModifiedBy = modifiedBy },
            cancellationToken: cancellationToken));

        await WriteChangelogAsync(connection, tenantId, assignmentId,
            existing.VehicleId, null, null, null,
            "Cancelled", body.Reason, modifiedBy, cancellationToken);
    }

    public async Task ApproveAsync(
        int tenantId, int assignmentId, string? notes, string approvedBy,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        var row = await connection.QuerySingleOrDefaultAsync<(int Id, int VehicleId, int? DriverId, string Status)>(
            new CommandDefinition(
                @"SELECT a.Id, a.VehicleId, a.DriverId, a.Status
                  FROM AssignmentHistory a INNER JOIN Vehicles v ON a.VehicleId = v.Id
                  WHERE a.Id = @Id AND v.TenantId = @TenantId AND a.IsDeleted = 0",
                new { Id = assignmentId, TenantId = tenantId }, cancellationToken: cancellationToken));

        if (row.Id == 0) throw new NotFoundException("Assignment", assignmentId);
        if (row.Status != "PendingApproval")
            throw new ConflictException("Only pending assignments can be approved.");

        var newStatus = "Scheduled";
        await connection.ExecuteAsync(new CommandDefinition(
            @"UPDATE AssignmentHistory SET Status = @Status, ApprovedBy = @ApprovedBy,
              ModifiedAt = GETUTCDATE(), ModifiedBy = @ModifiedBy
              WHERE Id = @Id",
            new { Id = assignmentId, Status = newStatus, ApprovedBy = approvedBy, ModifiedBy = approvedBy },
            cancellationToken: cancellationToken));

        await WriteChangelogAsync(connection, tenantId, assignmentId,
            row.VehicleId, row.VehicleId, row.DriverId, row.DriverId,
            "Approved", notes, approvedBy, cancellationToken);
    }

    public async Task RejectAsync(
        int tenantId, int assignmentId, string reason, string modifiedBy,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        var row = await connection.QuerySingleOrDefaultAsync<(int Id, int VehicleId, string Status)>(
            new CommandDefinition(
                @"SELECT a.Id, a.VehicleId, a.Status
                  FROM AssignmentHistory a INNER JOIN Vehicles v ON a.VehicleId = v.Id
                  WHERE a.Id = @Id AND v.TenantId = @TenantId AND a.IsDeleted = 0",
                new { Id = assignmentId, TenantId = tenantId }, cancellationToken: cancellationToken));

        if (row.Id == 0) throw new NotFoundException("Assignment", assignmentId);
        if (row.Status != "PendingApproval")
            throw new ConflictException("Only pending assignments can be rejected.");

        await connection.ExecuteAsync(new CommandDefinition(
            @"UPDATE AssignmentHistory SET Status = N'Cancelled', EndAt = GETUTCDATE(),
              Reason = @Reason, ModifiedAt = GETUTCDATE(), ModifiedBy = @ModifiedBy
              WHERE Id = @Id",
            new { Id = assignmentId, Reason = reason, ModifiedBy = modifiedBy },
            cancellationToken: cancellationToken));

        await WriteChangelogAsync(connection, tenantId, assignmentId,
            row.VehicleId, null, null, null, "Rejected", reason, modifiedBy, cancellationToken);
    }

    private static (string Condition, object Params) BuildFilter(
        int tenantId, string? search, string? status, string? assignmentType,
        int? vehicleId, int? driverId, int? branchId, int? departmentId, DateTime? dateFrom, DateTime? dateTo)
    {
        var clauses = new List<string> { "a.IsDeleted = 0", "v.TenantId = @TenantId" };
        var p = new System.Dynamic.ExpandoObject() as IDictionary<string, object?>;
        p["TenantId"] = tenantId;

        if (!string.IsNullOrWhiteSpace(status))
        {
            var st = status.Trim();
            if (st.Equals("Overdue", StringComparison.OrdinalIgnoreCase))
                clauses.Add("a.Status = N'Active' AND a.EndAt IS NOT NULL AND a.EndAt < GETUTCDATE()");
            else if (st.Equals("Assigned", StringComparison.OrdinalIgnoreCase))
                clauses.Add("a.Status = N'Active' AND a.StartAt > GETUTCDATE()");
            else
            {
                clauses.Add("a.Status = @Status");
                p["Status"] = st;
            }
        }

        if (!string.IsNullOrWhiteSpace(assignmentType))
        {
            clauses.Add("a.AssignmentType = @AssignmentType");
            p["AssignmentType"] = assignmentType.Trim();
        }

        if (vehicleId.HasValue)
        {
            clauses.Add("a.VehicleId = @VehicleId");
            p["VehicleId"] = vehicleId.Value;
        }

        if (driverId.HasValue)
        {
            clauses.Add("a.DriverId = @DriverId");
            p["DriverId"] = driverId.Value;
        }

        if (branchId.HasValue)
        {
            clauses.Add("(v.BranchId = @BranchId OR d.BranchId = @BranchId)");
            p["BranchId"] = branchId.Value;
        }

        if (departmentId.HasValue)
        {
            clauses.Add("(v.DepartmentId = @DepartmentId OR d.DepartmentId = @DepartmentId)");
            p["DepartmentId"] = departmentId.Value;
        }

        if (dateFrom.HasValue)
        {
            clauses.Add("a.StartAt >= @DateFrom");
            p["DateFrom"] = dateFrom.Value;
        }

        if (dateTo.HasValue)
        {
            clauses.Add("a.StartAt <= @DateTo");
            p["DateTo"] = dateTo.Value;
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            clauses.Add("(v.RegistrationNumber LIKE @Search OR v.Name LIKE @Search OR d.FullName LIKE @Search OR a.AssignmentNo LIKE @Search)");
            p["Search"] = $"%{search.Trim()}%";
        }

        return (string.Join(" AND ", clauses), p);
    }

    private static Task WriteChangelogAsync(IDbConnection conn, int tenantId, int assignmentId,
        int? oldVehicleId, int? newVehicleId, int? oldDriverId, int? newDriverId,
        string action, string? reason, string? by, CancellationToken ct,
        IDbTransaction? transaction = null)
        => conn.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO FleetAssignmentChangelog (TenantId, AssignmentId, OldVehicleId, NewVehicleId,
              OldDriverId, NewDriverId, ActionType, Reason, CreatedBy, CreatedAt)
              VALUES (@TenantId, @AssignmentId, @OldVehicleId, @NewVehicleId,
              @OldDriverId, @NewDriverId, @ActionType, @Reason, @CreatedBy, GETUTCDATE())",
            new
            {
                TenantId = tenantId,
                AssignmentId = assignmentId,
                OldVehicleId = oldVehicleId,
                NewVehicleId = newVehicleId,
                OldDriverId = oldDriverId,
                NewDriverId = newDriverId,
                ActionType = action,
                Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
                CreatedBy = by
            }, transaction: transaction, cancellationToken: ct));

    private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
