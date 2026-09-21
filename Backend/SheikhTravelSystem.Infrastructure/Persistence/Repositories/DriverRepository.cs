using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Infrastructure.Persistence;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Drivers;
using SheikhTravelSystem.Application.Features.Drivers.DTOs;
using SheikhTravelSystem.Domain.Enums;
using SheikhTravelSystem.Infrastructure.Persistence.Repositories.Sql;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories;

public sealed class DriverRepository(IDbConnectionFactory dbFactory) : IDriverRepository
{
    // ── Uniqueness ──────────────────────────────────────────────────────────

    public Task EnsureUniqueAsync(
        int tenantId, string phone, string? email, string licenseNumber, int? excludeId,
        CancellationToken cancellationToken = default)
        => EnsureUniqueCoreAsync(null, tenantId, phone, email, licenseNumber, excludeId, cancellationToken);

    public async Task<DriverAvailabilityDto> CheckAvailabilityAsync(
        int tenantId, string? phone, string? email, string? licenseNumber, int? excludeId,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var phoneAvailable = string.IsNullOrWhiteSpace(phone)
            || !await ExistsUniqueAsync(connection, tenantId, "Phone", phone.Trim(), excludeId, cancellationToken);
        var emailAvailable = string.IsNullOrWhiteSpace(email)
            || !await ExistsUniqueAsync(connection, tenantId, "Email", email.Trim(), excludeId, cancellationToken);
        var licenseAvailable = string.IsNullOrWhiteSpace(licenseNumber)
            || !await ExistsUniqueAsync(connection, tenantId, "LicenseNumber", licenseNumber.Trim(), excludeId, cancellationToken);
        return new DriverAvailabilityDto(phoneAvailable, emailAvailable, licenseAvailable);
    }

    private async Task EnsureUniqueCoreAsync(
        IDbConnection? existing, int tenantId, string phone, string? email, string licenseNumber, int? excludeId,
        CancellationToken cancellationToken)
    {
        var owns = existing is null;
        var connection = existing ?? dbFactory.CreateConnection();
        try
        {
            if (!string.IsNullOrWhiteSpace(licenseNumber)
                && await ExistsUniqueAsync(connection, tenantId, "LicenseNumber", licenseNumber.Trim(), excludeId, cancellationToken))
                throw new ConflictException($"Driver with license '{licenseNumber}' already exists.");

            if (await ExistsUniqueAsync(connection, tenantId, "Phone", phone.Trim(), excludeId, cancellationToken))
                throw new ConflictException($"A driver with mobile number '{phone}' already exists.");

            if (!string.IsNullOrWhiteSpace(email)
                && await ExistsUniqueAsync(connection, tenantId, "Email", email.Trim(), excludeId, cancellationToken))
                throw new ConflictException($"A driver with email '{email}' already exists.");
        }
        finally
        {
            if (owns) connection.Dispose();
        }
    }

    private static async Task<bool> ExistsUniqueAsync(
        IDbConnection connection, int tenantId, string column, string value, int? excludeId,
        CancellationToken cancellationToken)
    {
        var sql = column switch
        {
            "Phone" => """
                SELECT CASE WHEN EXISTS(
                    SELECT 1 FROM Drivers WHERE Phone = @Value AND IsDeleted = 0 AND TenantId = @TenantId
                      AND (@ExcludeId IS NULL OR Id != @ExcludeId)
                  ) THEN 1 ELSE 0 END
                """,
            "Email" => """
                SELECT CASE WHEN EXISTS(
                    SELECT 1 FROM Drivers WHERE Email = @Value AND IsDeleted = 0 AND TenantId = @TenantId
                      AND (@ExcludeId IS NULL OR Id != @ExcludeId)
                  ) THEN 1 ELSE 0 END
                """,
            "LicenseNumber" => """
                SELECT CASE WHEN EXISTS(
                    SELECT 1 FROM Drivers WHERE LicenseNumber = @Value AND IsDeleted = 0 AND TenantId = @TenantId
                      AND (@ExcludeId IS NULL OR Id != @ExcludeId)
                  ) THEN 1 ELSE 0 END
                """,
            _ => throw new ArgumentOutOfRangeException(nameof(column), column, "Unsupported uniqueness column.")
        };

        return await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(sql, new { Value = value, TenantId = tenantId, ExcludeId = excludeId },
                cancellationToken: cancellationToken));
    }

    // ── CRUD ────────────────────────────────────────────────────────────────

    public async Task<int> CreateAsync(
        int tenantId, CreateDriverDto dto, string fullName, string driverCode,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        await EnsureUniqueCoreAsync(connection, tenantId, dto.Phone, dto.Email, dto.LicenseNumber, null, cancellationToken);

        return await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                @"INSERT INTO Drivers (TenantId, FullName, FirstName, LastName, Phone, LicenseNumber, LicenseExpiryDate,
                  CNIC, Address, DriverCode, Nationality, Email, DateOfBirth, Gender, EmergencyContactName, EmergencyContact,
                  HireDate, BranchId, DepartmentId, VerificationStatus, Status, IsActive, CreatedAt, IsDeleted)
                  VALUES (@TenantId, @FullName, @FirstName, @LastName, @Phone, @LicenseNumber, @LicenseExpiryDate,
                  @CNIC, @Address, @DriverCode, @Nationality, @Email, @DateOfBirth, @Gender, @EmergencyContactName, @EmergencyContact,
                  @HireDate, @BranchId, @DepartmentId, N'Pending', @Status, 1, @CreatedAt, 0);
                  SELECT CAST(SCOPE_IDENTITY() AS INT);",
                new
                {
                    TenantId = tenantId,
                    FullName = fullName,
                    FirstName = dto.FirstName.Trim(),
                    LastName = dto.LastName.Trim(),
                    dto.Phone,
                    dto.LicenseNumber,
                    dto.LicenseExpiryDate,
                    dto.CNIC,
                    dto.Address,
                    DriverCode = driverCode,
                    dto.Nationality,
                    dto.Email,
                    dto.DateOfBirth,
                    dto.Gender,
                    dto.EmergencyContactName,
                    dto.EmergencyContact,
                    dto.HireDate,
                    dto.BranchId,
                    dto.DepartmentId,
                    Status = (int)DriverStatus.Available,
                    CreatedAt = DateTime.UtcNow
                },
                cancellationToken: cancellationToken));
    }

    public async Task UpdateAsync(
        int tenantId, int id, UpdateDriverDto dto, string fullName,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        var exists = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT CASE WHEN EXISTS(SELECT 1 FROM Drivers WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0) THEN 1 ELSE 0 END",
                new { Id = id, TenantId = tenantId },
                cancellationToken: cancellationToken));

        if (!exists)
            throw new NotFoundException("Driver", id);

        await EnsureUniqueCoreAsync(connection, tenantId, dto.Phone, dto.Email, dto.LicenseNumber, id, cancellationToken);

        await connection.ExecuteAsync(
            new CommandDefinition(
                @"UPDATE Drivers SET FullName = @FullName, FirstName = @FirstName, LastName = @LastName,
                  Phone = @Phone, LicenseNumber = @LicenseNumber, LicenseExpiryDate = @LicenseExpiryDate,
                  CNIC = @CNIC, Address = @Address, Nationality = @Nationality, Email = @Email,
                  DateOfBirth = @DateOfBirth, Gender = @Gender,
                  EmergencyContactName = @EmergencyContactName, EmergencyContact = @EmergencyContact,
                  HireDate = @HireDate, BranchId = @BranchId, DepartmentId = @DepartmentId,
                  Status = @Status, IsActive = @IsActive, UpdatedAt = @UpdatedAt
                  WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
                new
                {
                    FullName = fullName,
                    FirstName = dto.FirstName.Trim(),
                    LastName = dto.LastName.Trim(),
                    dto.Phone,
                    dto.LicenseNumber,
                    dto.LicenseExpiryDate,
                    dto.CNIC,
                    dto.Address,
                    dto.Nationality,
                    dto.Email,
                    dto.DateOfBirth,
                    dto.Gender,
                    dto.EmergencyContactName,
                    dto.EmergencyContact,
                    dto.HireDate,
                    dto.BranchId,
                    dto.DepartmentId,
                    Status = (int)dto.Status,
                    dto.IsActive,
                    UpdatedAt = DateTime.UtcNow,
                    Id = id,
                    TenantId = tenantId
                },
                cancellationToken: cancellationToken));
    }

    public async Task<string?> SoftDeleteAsync(int tenantId, int id, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        var exists = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT CASE WHEN EXISTS(SELECT 1 FROM Drivers WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0) THEN 1 ELSE 0 END",
                new { Id = id, TenantId = tenantId },
                cancellationToken: cancellationToken));

        if (!exists)
            throw new NotFoundException("Driver", id);

        var photoUrl = await connection.ExecuteScalarAsync<string?>(
            new CommandDefinition(
                "SELECT PhotoUrl FROM Drivers WHERE Id = @Id AND TenantId = @TenantId",
                new { Id = id, TenantId = tenantId },
                cancellationToken: cancellationToken));

        await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE Drivers SET IsDeleted = 1, UpdatedAt = @UpdatedAt WHERE Id = @Id AND TenantId = @TenantId",
                new { UpdatedAt = DateTime.UtcNow, Id = id, TenantId = tenantId },
                cancellationToken: cancellationToken));

        return photoUrl;
    }

    public async Task<bool> ExistsAsync(int tenantId, int id, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT CASE WHEN EXISTS(SELECT 1 FROM Drivers WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0) THEN 1 ELSE 0 END",
                new { Id = id, TenantId = tenantId },
                cancellationToken: cancellationToken));
    }

    public async Task<DriverDto> GetByIdAsync(int tenantId, int id, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        var driver = await connection.QuerySingleOrDefaultAsync<DriverDto>(
            new CommandDefinition(
                $@"SELECT {DriverSql.DetailColumns}
                  {DriverSql.DetailFrom}
                  WHERE d.Id = @Id AND d.TenantId = @TenantId AND d.IsDeleted = 0",
                new
                {
                    Id = id,
                    TenantId = tenantId,
                    OnTrip = (int)DriverStatus.OnTrip,
                    OffDuty = (int)DriverStatus.OffDuty,
                    Available = (int)DriverStatus.Available,
                    OnLeave = (int)DriverStatus.OnLeave,
                    Suspended = (int)DriverStatus.Suspended
                },
                cancellationToken: cancellationToken));

        if (driver is null)
            throw new NotFoundException("Driver", id);

        return driver;
    }

    public async Task<DriverListQueryResult> GetPagedAsync(
        int tenantId, int page, int pageSize, string? q, DriverStatus? status, int? branchId,
        string? licenseExpiry, string? verificationStatus, string? availability, DataScopeResult? scope,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var offset = (page - 1) * pageSize;

        var where = new List<string> { "d.IsDeleted = 0", "d.TenantId = @TenantId" };
        var parameters = new DynamicParameters(new
        {
            TenantId = tenantId,
            Offset = offset,
            PageSize = pageSize,
            OnTrip = (int)DriverStatus.OnTrip,
            OffDuty = (int)DriverStatus.OffDuty,
            Available = (int)DriverStatus.Available,
            OnLeave = (int)DriverStatus.OnLeave,
            Suspended = (int)DriverStatus.Suspended
        });

        if (!string.IsNullOrWhiteSpace(q))
        {
            where.Add(@"(d.FullName LIKE @Search OR d.FirstName LIKE @Search OR d.LastName LIKE @Search OR d.DriverCode LIKE @Search OR d.LicenseNumber LIKE @Search OR d.Phone LIKE @Search)");
            parameters.Add("Search", $"%{q.Trim()}%");
        }

        if (status.HasValue)
        {
            where.Add("d.Status = @Status");
            parameters.Add("Status", (int)status.Value);
        }

        if (scope is not null)
        {
            if (!Application.Common.DataScopeSql.TryIntersectOptional(scope, branchId, null, out _, out _, out var scopeError))
                return new DriverListQueryResult([], 0, scopeError ?? "Outside data scope.");

            DataScopeSqlBuilder.ApplyDriverScope(parameters, scope, "d", where, branchId);
        }
        else if (branchId.HasValue)
        {
            where.Add("d.BranchId = @BranchId");
            parameters.Add("BranchId", branchId.Value);
        }

        if (!string.IsNullOrWhiteSpace(verificationStatus))
        {
            where.Add("d.VerificationStatus = @VerificationStatus");
            parameters.Add("VerificationStatus", verificationStatus.Trim());
        }

        switch (licenseExpiry?.Trim().ToUpperInvariant())
        {
            case "EXPIRED":
                where.Add("d.LicenseExpiryDate < CAST(GETUTCDATE() AS DATE)");
                break;
            case "EXPIRING":
                where.Add("d.LicenseExpiryDate >= CAST(GETUTCDATE() AS DATE)");
                where.Add("d.LicenseExpiryDate <= DATEADD(day, 30, CAST(GETUTCDATE() AS DATE))");
                break;
            case "VALID":
                where.Add("d.LicenseExpiryDate > DATEADD(day, 30, CAST(GETUTCDATE() AS DATE))");
                break;
        }

        if (!string.IsNullOrWhiteSpace(availability))
        {
            where.Add($"({DriverSql.BucketSqlExpression}) = @AvailabilityBucket");
            parameters.Add("AvailabilityBucket", availability.Trim());
        }

        var whereClause = string.Join(" AND ", where);

        var drivers = (await connection.QueryAsync<DriverListItemDto>(
            new CommandDefinition(
                $@"SELECT {DriverSql.ListSelect}
                  {DriverSql.ListFrom}
                  WHERE {whereClause}
                  ORDER BY d.CreatedAt DESC
                  OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY",
                parameters,
                cancellationToken: cancellationToken))).ToList();

        var totalCount = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                $@"SELECT COUNT(*) {DriverSql.ListFrom} WHERE {whereClause}",
                parameters,
                cancellationToken: cancellationToken));

        return new DriverListQueryResult(drivers, totalCount);
    }

    public async Task<DriverStatsDto> GetStatsAsync(int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var bucketSql = DriverSql.BucketSqlExpression;

        return await connection.QuerySingleAsync<DriverStatsDto>(
            new CommandDefinition(
                $@"SELECT
                    COUNT(*) AS TotalDrivers,
                    SUM(CASE WHEN IsActive = 1 THEN 1 ELSE 0 END) AS Active,
                    SUM(CASE WHEN IsActive = 0 THEN 1 ELSE 0 END) AS Inactive,
                    SUM(CASE WHEN Status = @OnTrip THEN 1 ELSE 0 END) AS OnTrip,
                    SUM(CASE WHEN Status = @OffDuty THEN 1 ELSE 0 END) AS OffDuty,
                    SUM(CASE WHEN Status = @Available THEN 1 ELSE 0 END) AS Available,
                    SUM(CASE WHEN bucket = N'Busy' THEN 1 ELSE 0 END) AS Busy,
                    SUM(CASE WHEN Status = @OnLeave THEN 1 ELSE 0 END) AS OnLeave,
                    SUM(CASE WHEN Status = @Suspended THEN 1 ELSE 0 END) AS Suspended,
                    SUM(CASE WHEN gpsOnline = 1 THEN 1 ELSE 0 END) AS GpsOnline,
                    SUM(CASE WHEN LicenseExpiryDate >= CAST(GETUTCDATE() AS DATE)
                              AND LicenseExpiryDate <= DATEADD(day, 30, CAST(GETUTCDATE() AS DATE))
                         THEN 1 ELSE 0 END) AS LicensesExpiringSoon,
                    SUM(CASE WHEN LicenseExpiryDate >= CAST(GETUTCDATE() AS DATE)
                              AND LicenseExpiryDate <= DATEADD(day, 7, CAST(GETUTCDATE() AS DATE))
                         THEN 1 ELSE 0 END) AS LicensesExpiringIn7Days,
                    SUM(CASE WHEN LicenseExpiryDate < CAST(GETUTCDATE() AS DATE)
                         THEN 1 ELSE 0 END) AS LicensesExpired,
                    SUM(CASE WHEN VerificationStatus = N'Verified' THEN 1 ELSE 0 END) AS VerifiedDrivers,
                    SUM(CASE WHEN VerificationStatus = N'Pending'  THEN 1 ELSE 0 END) AS PendingVerification,
                    SUM(CASE WHEN isAssigned = 1 THEN 1 ELSE 0 END) AS AssignedDrivers
                  FROM (
                    SELECT d.*,
                           CASE WHEN av.GpsDeviceId IS NOT NULL AND gd.LastSeenAt >= DATEADD(minute, -15, GETUTCDATE())
                                THEN 1 ELSE 0 END AS gpsOnline,
                           CASE WHEN av.HasAssignment = 1 THEN 1 ELSE 0 END AS isAssigned,
                           {bucketSql} AS bucket
                    FROM Drivers d
                    OUTER APPLY (
                        SELECT TOP 1 v.GpsDeviceId, 1 AS HasAssignment
                        FROM AssignmentHistory ah
                        INNER JOIN Vehicles v ON v.Id = ah.VehicleId AND v.IsDeleted = 0
                        WHERE ah.DriverId = d.Id AND ah.IsDeleted = 0 AND ah.Status = N'Active'
                        ORDER BY ah.StartAt DESC
                    ) av
                    LEFT JOIN GpsDevices gd ON gd.Id = av.GpsDeviceId AND gd.IsDeleted = 0
                    WHERE d.IsDeleted = 0 AND d.TenantId = @TenantId
                  ) d",
                new
                {
                    TenantId = tenantId,
                    OnTrip = (int)DriverStatus.OnTrip,
                    OffDuty = (int)DriverStatus.OffDuty,
                    Available = (int)DriverStatus.Available,
                    OnLeave = (int)DriverStatus.OnLeave,
                    Suspended = (int)DriverStatus.Suspended
                },
                cancellationToken: cancellationToken));
    }

    // ── Status ──────────────────────────────────────────────────────────────

    public async Task<bool> ToggleActiveAsync(int tenantId, int id, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        var current = await connection.QuerySingleOrDefaultAsync<bool?>(
            new CommandDefinition(
                "SELECT IsActive FROM Drivers WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
                new { Id = id, TenantId = tenantId },
                cancellationToken: cancellationToken));

        if (current is null)
            throw new NotFoundException("Driver", id);

        var newValue = !current.Value;
        await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE Drivers SET IsActive = @IsActive, UpdatedAt = GETUTCDATE() WHERE Id = @Id AND TenantId = @TenantId",
                new { IsActive = newValue, Id = id, TenantId = tenantId },
                cancellationToken: cancellationToken));

        return newValue;
    }

    public async Task ChangeStatusAsync(int tenantId, int id, DriverStatus status, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        var rows = await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE Drivers SET Status = @Status, UpdatedAt = GETUTCDATE() WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
                new { Status = (int)status, Id = id, TenantId = tenantId },
                cancellationToken: cancellationToken));

        if (rows == 0)
            throw new NotFoundException("Driver", id);
    }

    // ── Photo / documents / verification ────────────────────────────────────

    public async Task UpdatePhotoUrlAsync(int tenantId, int driverId, string photoUrl, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        if (!await ExistsAsync(tenantId, driverId, cancellationToken))
            throw new NotFoundException("Driver", driverId);

        await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE Drivers SET PhotoUrl = @PhotoUrl, UpdatedAt = GETUTCDATE() WHERE Id = @Id AND TenantId = @TenantId",
                new { PhotoUrl = photoUrl, Id = driverId, TenantId = tenantId },
                cancellationToken: cancellationToken));
    }

    public async Task SoftDeleteDocumentsByTypeAsync(
        int tenantId, int driverId, string documentType, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            @"UPDATE ComplianceDocuments SET IsDeleted = 1, UpdatedAt = GETUTCDATE()
              WHERE TenantId = @TenantId AND EntityType = N'Driver' AND EntityId = @DriverId
                AND DocumentType = @DocumentType AND IsDeleted = 0",
            new { TenantId = tenantId, DriverId = driverId, DocumentType = documentType },
            cancellationToken: cancellationToken));
    }

    public async Task<int> InsertDocumentAsync(
        int tenantId, int driverId, string documentType, string fileUrl, DateTime? expiryDate,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                @"INSERT INTO ComplianceDocuments (TenantId, EntityType, EntityId, DocumentType, FileUrl,
                  ExpiryDate, Status, CreatedAt, IsDeleted)
                  VALUES (@TenantId, N'Driver', @DriverId, @DocumentType, @FileUrl, @ExpiryDate, N'Pending', GETUTCDATE(), 0);
                  SELECT CAST(SCOPE_IDENTITY() AS INT);",
                new
                {
                    TenantId = tenantId,
                    DriverId = driverId,
                    DocumentType = documentType,
                    FileUrl = fileUrl,
                    ExpiryDate = expiryDate
                },
                cancellationToken: cancellationToken));
    }

    public async Task UpdateVerificationStatusAsync(
        int tenantId, int driverId, string verificationStatus, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.ExecuteAsync(
            new CommandDefinition(
                @"UPDATE Drivers SET VerificationStatus = @VerificationStatus, UpdatedAt = GETUTCDATE()
                  WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
                new { VerificationStatus = verificationStatus.Trim(), Id = driverId, TenantId = tenantId },
                cancellationToken: cancellationToken));

        if (rows == 0)
            throw new NotFoundException("Driver", driverId);
    }

    public async Task UpdateDocumentStatusAsync(
        int tenantId, int driverId, int documentId, string status, string? rejectionReason, string reviewer,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.ExecuteAsync(new CommandDefinition(
            @"UPDATE ComplianceDocuments
              SET    Status          = @Status,
                     RejectionReason = @RejectionReason,
                     UpdatedBy       = @Reviewer,
                     UpdatedAt       = GETUTCDATE()
              WHERE  Id          = @DocumentId
                AND  EntityType  = N'Driver'
                AND  EntityId    = @DriverId
                AND  TenantId    = @TenantId
                AND  IsDeleted   = 0",
            new
            {
                Status = status,
                RejectionReason = rejectionReason,
                Reviewer = reviewer,
                DocumentId = documentId,
                DriverId = driverId,
                TenantId = tenantId
            },
            cancellationToken: cancellationToken));

        if (rows == 0)
            throw new NotFoundException("Document", documentId);
    }

    public async Task<int> InsertReviewNoteAsync(
        int tenantId, int driverId, string note, string? documentType, string createdBy,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        if (!await ExistsOn(connection, tenantId, driverId, cancellationToken))
            throw new NotFoundException("Driver", driverId);

        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            @"INSERT INTO DriverReviewNotes (TenantId, DriverId, Note, DocumentType, CreatedBy, CreatedAt, IsDeleted)
              VALUES (@TenantId, @DriverId, @Note, @DocumentType, @CreatedBy, GETUTCDATE(), 0);
              SELECT CAST(SCOPE_IDENTITY() AS INT);",
            new
            {
                TenantId = tenantId,
                DriverId = driverId,
                Note = note,
                DocumentType = documentType,
                CreatedBy = createdBy
            },
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<DriverDocumentDetailedDto>> GetDocumentsAsync(
        int tenantId, int driverId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = (await connection.QueryAsync<DriverDocumentDetailedDto>(
            new CommandDefinition(
                @"SELECT Id,
                         DocumentType,
                         FileUrl,
                         ExpiryDate,
                         Status,
                         RejectionReason,
                         UpdatedBy   AS ReviewedBy,
                         UpdatedAt   AS ReviewedAt,
                         CreatedAt
                  FROM   ComplianceDocuments
                  WHERE  EntityType = N'Driver'
                    AND  EntityId   = @DriverId
                    AND  TenantId   = @TenantId
                    AND  IsDeleted  = 0
                  ORDER BY CreatedAt DESC",
                new { DriverId = driverId, TenantId = tenantId },
                cancellationToken: cancellationToken))).ToList();
        return rows;
    }

    public async Task<IReadOnlyList<DriverReviewNoteDto>> GetReviewNotesAsync(
        int tenantId, int driverId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = (await connection.QueryAsync<DriverReviewNoteDto>(
            new CommandDefinition(
                @"SELECT Id,
                         Note,
                         DocumentType,
                         CreatedBy,
                         CreatedAt
                  FROM   DriverReviewNotes
                  WHERE  DriverId  = @DriverId
                    AND  TenantId  = @TenantId
                    AND  IsDeleted = 0
                  ORDER BY CreatedAt DESC",
                new { DriverId = driverId, TenantId = tenantId },
                cancellationToken: cancellationToken))).ToList();
        return rows;
    }

    // ── Assignment ──────────────────────────────────────────────────────────

    private (IDbConnection Conn, bool Owns, IDbTransaction? Tx) ResolveConn(
        IDbConnection? connection, IDbTransaction? transaction)
    {
        if (connection is not null)
            return (connection, false, transaction);
        var c = dbFactory.CreateConnection();
        return (c, true, null);
    }

    public async Task<(string VerificationStatus, DateTime LicenseExpiry, bool IsActive, int Status)?> GetAssignmentGuardRowAsync(
        int tenantId, int driverId, IDbConnection? connection = null, IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        var (conn, owns, tx) = ResolveConn(connection, transaction);
        try
        {
            var driver = await conn.QuerySingleOrDefaultAsync<(string VerificationStatus, DateTime LicenseExpiry, bool IsActive, int Status)>(
                new CommandDefinition(
                    "SELECT VerificationStatus, LicenseExpiryDate, IsActive, Status FROM Drivers WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
                    new { Id = driverId, TenantId = tenantId },
                    transaction: tx,
                    cancellationToken: cancellationToken));

            if (driver.VerificationStatus is null)
                return null;
            return driver;
        }
        finally
        {
            if (owns) conn.Dispose();
        }
    }

    public async Task EnsureDriverNotOnActiveTripAsync(
        int tenantId, int driverId, IDbConnection? connection = null, IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        var (conn, owns, tx) = ResolveConn(connection, transaction);
        try
        {
            await DriverAssignmentOps.EnsureDriverNotOnActiveTripAsync(conn, tenantId, driverId, cancellationToken, tx);
        }
        finally
        {
            if (owns) conn.Dispose();
        }
    }

    public async Task EnsureVehicleAvailableForDriverAsync(
        int tenantId, int vehicleId, int driverId, int? excludeAssignmentId,
        IDbConnection? connection = null, IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        var (conn, owns, tx) = ResolveConn(connection, transaction);
        try
        {
            await DriverAssignmentOps.EnsureVehicleAvailableForDriverAsync(
                conn, tenantId, vehicleId, driverId, excludeAssignmentId, cancellationToken, tx);
        }
        finally
        {
            if (owns) conn.Dispose();
        }
    }

    public async Task EnsureVehicleAssignableAsync(
        int tenantId, int vehicleId, IDbConnection? connection = null, IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        var (conn, owns, tx) = ResolveConn(connection, transaction);
        try
        {
            await DriverAssignmentOps.EnsureVehicleAssignableAsync(conn, tenantId, vehicleId, cancellationToken, tx);
        }
        finally
        {
            if (owns) conn.Dispose();
        }
    }

    public async Task<int> CompleteActiveAssignmentsAsync(
        int tenantId, int driverId, int? vehicleId, IDbConnection? connection = null, IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        var (conn, owns, tx) = ResolveConn(connection, transaction);
        try
        {
            return await DriverAssignmentOps.CompleteActiveAssignmentsAsync(
                conn, tenantId, driverId, vehicleId, tx, cancellationToken);
        }
        finally
        {
            if (owns) conn.Dispose();
        }
    }

    public async Task<bool> VehicleExistsAsync(
        int tenantId, int vehicleId, IDbConnection? connection = null, IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        var (conn, owns, tx) = ResolveConn(connection, transaction);
        try
        {
            return await conn.ExecuteScalarAsync<bool>(
                new CommandDefinition(
                    "SELECT CASE WHEN EXISTS(SELECT 1 FROM Vehicles WHERE Id = @VehicleId AND TenantId = @TenantId AND IsDeleted = 0) THEN 1 ELSE 0 END",
                    new { VehicleId = vehicleId, TenantId = tenantId },
                    transaction: tx,
                    cancellationToken: cancellationToken));
        }
        finally
        {
            if (owns) conn.Dispose();
        }
    }

    public async Task<int> InsertAssignmentAsync(
        int tenantId, int driverId, int vehicleId, int? bookingId, string assignmentType, DateTime startAt,
        string? notes, string createdBy, IDbConnection? connection = null, IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        var (conn, owns, tx) = ResolveConn(connection, transaction);
        try
        {
            return await conn.ExecuteScalarAsync<int>(
                new CommandDefinition(
                    @"INSERT INTO AssignmentHistory (TenantId, VehicleId, DriverId, BookingId, AssignmentType,
                      Status, StartAt, CreatedAt, CreatedBy, Notes, IsDeleted)
                      VALUES (@TenantId, @VehicleId, @DriverId, @BookingId, @AssignmentType,
                      N'Active', @StartAt, GETUTCDATE(), @CreatedBy, @Notes, 0);
                      SELECT CAST(SCOPE_IDENTITY() AS INT);",
                    new
                    {
                        TenantId = tenantId,
                        VehicleId = vehicleId,
                        DriverId = driverId,
                        BookingId = bookingId,
                        AssignmentType = assignmentType,
                        StartAt = startAt,
                        Notes = notes,
                        CreatedBy = createdBy
                    },
                    transaction: tx,
                    cancellationToken: cancellationToken));
        }
        finally
        {
            if (owns) conn.Dispose();
        }
    }

    public async Task<int> UnassignVehicleAsync(int tenantId, int driverId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        if (!await ExistsOn(connection, tenantId, driverId, cancellationToken))
            throw new NotFoundException("Driver", driverId);

        await DriverAssignmentOps.EnsureDriverNotOnActiveTripAsync(connection, tenantId, driverId, cancellationToken);
        return await DriverAssignmentOps.CompleteActiveAssignmentsAsync(
            connection, tenantId, driverId, vehicleId: null, transaction: null, cancellationToken);
    }

    public async Task<int> AssignVehicleAsync(
        int tenantId, int driverId, AssignDriverVehicleRequest body, string createdBy,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        DriverAssignmentOps.OpenConnection(connection);
        using var transaction = connection.BeginTransaction();

        try
        {
            var driver = await connection.QuerySingleOrDefaultAsync<(string VerificationStatus, DateTime LicenseExpiry, bool IsActive, int Status)>(
                new CommandDefinition(
                    "SELECT VerificationStatus, LicenseExpiryDate, IsActive, Status FROM Drivers WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
                    new { Id = driverId, TenantId = tenantId },
                    transaction: transaction,
                    cancellationToken: cancellationToken));

            if (driver.VerificationStatus is null)
                throw new NotFoundException("Driver", driverId);

            DriverAssignmentGuard.EnsureAssignable(
                driver.IsActive,
                (DriverStatus)driver.Status,
                driver.VerificationStatus,
                driver.LicenseExpiry);

            await DriverAssignmentOps.EnsureDriverNotOnActiveTripAsync(
                connection, tenantId, driverId, cancellationToken, transaction);

            var vehicleExists = await connection.ExecuteScalarAsync<bool>(
                new CommandDefinition(
                    "SELECT CASE WHEN EXISTS(SELECT 1 FROM Vehicles WHERE Id = @VehicleId AND TenantId = @TenantId AND IsDeleted = 0) THEN 1 ELSE 0 END",
                    new { body.VehicleId, TenantId = tenantId },
                    transaction: transaction,
                    cancellationToken: cancellationToken));

            if (!vehicleExists)
                throw new NotFoundException("Vehicle", body.VehicleId);

            await DriverAssignmentOps.CompleteActiveAssignmentsAsync(
                connection, tenantId, driverId, body.VehicleId, transaction, cancellationToken);

            var assignmentType = string.IsNullOrWhiteSpace(body.AssignmentType) ? "Manual" : body.AssignmentType.Trim();
            var startAt = body.EffectiveFrom?.ToUniversalTime() ?? DateTime.UtcNow;
            var notes = string.IsNullOrWhiteSpace(body.Remarks) ? null : body.Remarks.Trim();
            if (body.EffectiveTo is DateTime plannedEnd)
            {
                var plannedNote = $"Planned end: {plannedEnd:yyyy-MM-dd}";
                notes = string.IsNullOrWhiteSpace(notes) ? plannedNote : $"{notes} ({plannedNote})";
            }

            var assignmentId = await connection.ExecuteScalarAsync<int>(
                new CommandDefinition(
                    @"INSERT INTO AssignmentHistory (TenantId, VehicleId, DriverId, BookingId, AssignmentType,
                      Status, StartAt, CreatedAt, CreatedBy, Notes, IsDeleted)
                      VALUES (@TenantId, @VehicleId, @DriverId, @BookingId, @AssignmentType,
                      N'Active', @StartAt, GETUTCDATE(), @CreatedBy, @Notes, 0);
                      SELECT CAST(SCOPE_IDENTITY() AS INT);",
                    new
                    {
                        TenantId = tenantId,
                        body.VehicleId,
                        DriverId = driverId,
                        body.BookingId,
                        AssignmentType = assignmentType,
                        StartAt = startAt,
                        Notes = notes,
                        CreatedBy = createdBy
                    },
                    transaction: transaction,
                    cancellationToken: cancellationToken));

            transaction.Commit();
            return assignmentId;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    // ── Extended queries ────────────────────────────────────────────────────

    public async Task<IReadOnlyList<DriverTimelineEventDto>> GetTimelineAsync(
        int tenantId, int driverId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return (await connection.QueryAsync<DriverTimelineEventDto>(
            new CommandDefinition(
                @"SELECT * FROM (
                    SELECT d.Id AS Id, N'Registered' AS EventType, N'Driver registered' AS Title,
                      CONCAT(N'Code: ', ISNULL(d.DriverCode, N'—')) AS Description, d.CreatedAt AS OccurredAt
                    FROM Drivers d
                    WHERE d.Id = @DriverId AND d.TenantId = @TenantId AND d.IsDeleted = 0
                    UNION ALL
                    SELECT c.Id, N'DocumentUploaded', CONCAT(N'Document uploaded: ', c.DocumentType),
                      NULL, c.CreatedAt
                    FROM ComplianceDocuments c
                    WHERE c.EntityType = N'Driver' AND c.EntityId = @DriverId AND c.TenantId = @TenantId AND c.IsDeleted = 0
                    UNION ALL
                    SELECT a.Id, N'AssignedVehicle', N'Vehicle assigned',
                      CONCAT(N'Assignment #', a.Id), a.StartAt
                    FROM AssignmentHistory a
                    WHERE a.DriverId = @DriverId AND a.TenantId = @TenantId AND a.IsDeleted = 0
                  ) timeline
                  ORDER BY OccurredAt DESC",
                new { DriverId = driverId, TenantId = tenantId },
                cancellationToken: cancellationToken))).ToList();
    }

    public async Task<(IReadOnlyList<DriverTripSummaryRow> RecentTrips, int FuelLogCount, bool HasGpsAssignment)> GetActiveDutyAsync(
        int tenantId, int driverId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        var trips = (await connection.QueryAsync<DriverTripSummaryRow>(
            new CommandDefinition(
                @"SELECT TOP 5 b.Id, CAST(b.Status AS NVARCHAR(20)) AS Status, b.PickupTime AS TripDate, r.Name AS Route
                  FROM Bookings b
                  LEFT JOIN Routes r ON r.Id = b.RouteId
                  WHERE b.DriverId = @DriverId AND b.TenantId = @TenantId AND b.IsDeleted = 0
                  ORDER BY b.PickupTime DESC",
                new { DriverId = driverId, TenantId = tenantId },
                cancellationToken: cancellationToken))).ToList();

        var fuelCount = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                "SELECT COUNT(*) FROM FuelLogs WHERE DriverId = @DriverId AND TenantId = @TenantId AND IsDeleted = 0",
                new { DriverId = driverId, TenantId = tenantId },
                cancellationToken: cancellationToken));

        var hasGps = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                @"SELECT CASE WHEN EXISTS(
                    SELECT 1 FROM AssignmentHistory ah
                    INNER JOIN Vehicles v ON v.Id = ah.VehicleId
                    WHERE ah.DriverId = @DriverId AND ah.TenantId = @TenantId
                      AND ah.Status = N'Active' AND ah.IsDeleted = 0 AND v.GpsDeviceId IS NOT NULL
                  ) THEN 1 ELSE 0 END",
                new { DriverId = driverId, TenantId = tenantId },
                cancellationToken: cancellationToken));

        return (trips, fuelCount, hasGps);
    }

    public async Task<PagedResult<DriverAssignmentDto>> GetAssignmentsAsync(
        int tenantId, int driverId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var offset = (page - 1) * pageSize;

        var items = (await connection.QueryAsync<DriverAssignmentDto>(
            new CommandDefinition(
                @"SELECT ah.Id, ah.VehicleId, v.RegistrationNumber AS VehicleRegistration, v.VehicleCode,
                         v.Name AS VehicleName, v.Make AS VehicleMake, v.Model AS VehicleModel, v.Color AS VehicleColor,
                         ah.AssignmentType, ah.Status, ah.StartAt, ah.EndAt, ah.BookingId,
                         ah.CreatedBy AS AssignedBy, ah.Notes AS Remarks
                  FROM AssignmentHistory ah
                  INNER JOIN Vehicles v ON v.Id = ah.VehicleId AND v.IsDeleted = 0
                  WHERE ah.DriverId = @DriverId AND ah.TenantId = @TenantId AND ah.IsDeleted = 0
                  ORDER BY ah.StartAt DESC
                  OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY",
                new { DriverId = driverId, TenantId = tenantId, Offset = offset, PageSize = pageSize },
                cancellationToken: cancellationToken))).ToList();

        var total = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                "SELECT COUNT(*) FROM AssignmentHistory WHERE DriverId = @DriverId AND TenantId = @TenantId AND IsDeleted = 0",
                new { DriverId = driverId, TenantId = tenantId },
                cancellationToken: cancellationToken));

        return new PagedResult<DriverAssignmentDto>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<DriversAvailabilitySummaryDto> GetAvailabilitySummaryAsync(
        int tenantId, int? branchId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var bucketSql = DriverSql.BucketSqlExpression;

        var where = "d.IsDeleted = 0 AND d.TenantId = @TenantId";
        if (branchId.HasValue)
            where += " AND d.BranchId = @BranchId";

        return await connection.QuerySingleAsync<DriversAvailabilitySummaryDto>(
            new CommandDefinition(
                $@"SELECT
                    SUM(CASE WHEN bucket = N'Available' THEN 1 ELSE 0 END) AS Available,
                    SUM(CASE WHEN bucket = N'Busy' THEN 1 ELSE 0 END) AS Busy,
                    SUM(CASE WHEN bucket = N'OnTrip' THEN 1 ELSE 0 END) AS OnTrip,
                    SUM(CASE WHEN bucket = N'Unavailable' THEN 1 ELSE 0 END) AS Unavailable
                  FROM (
                    SELECT {bucketSql} AS bucket
                    FROM Drivers d
                    WHERE {where}
                  ) x",
                new
                {
                    TenantId = tenantId,
                    BranchId = branchId,
                    OnTrip = (int)DriverStatus.OnTrip,
                    OffDuty = (int)DriverStatus.OffDuty,
                    Available = (int)DriverStatus.Available,
                    OnLeave = (int)DriverStatus.OnLeave,
                    Suspended = (int)DriverStatus.Suspended
                },
                cancellationToken: cancellationToken));
    }

    public async Task<DriverAvailabilityDetailDto?> GetAvailabilityDetailAsync(
        int tenantId, int driverId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        var exists = await ExistsOn(connection, tenantId, driverId, cancellationToken);
        if (!exists)
            return null;

        var row = await connection.QuerySingleAsync<(bool IsActive, int Status, bool LicenseExpired, bool HasAssignment)>(
            new CommandDefinition(
                @"SELECT d.IsActive, d.Status,
                         CASE WHEN d.LicenseExpiryDate < CAST(GETUTCDATE() AS DATE) THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END,
                         CASE WHEN EXISTS (
                            SELECT 1 FROM AssignmentHistory ah
                            WHERE ah.DriverId = d.Id AND ah.IsDeleted = 0 AND ah.Status = N'Active'
                         ) THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END
                  FROM Drivers d
                  WHERE d.Id = @Id AND d.TenantId = @TenantId AND d.IsDeleted = 0",
                new { Id = driverId, TenantId = tenantId },
                cancellationToken: cancellationToken));

        var bucket = DriverAvailabilityHelper.Compute(
            row.IsActive,
            (DriverStatus)row.Status,
            row.HasAssignment,
            row.LicenseExpired);

        return new DriverAvailabilityDetailDto(driverId, bucket.ToString(), (DriverStatus)row.Status, row.HasAssignment);
    }

    // ── Performance ─────────────────────────────────────────────────────────

    public async Task UpdateRatingAsync(int tenantId, int driverId, decimal rating, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE Drivers SET Rating = @Rating, UpdatedAt = GETUTCDATE() WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
                new { Rating = rating, Id = driverId, TenantId = tenantId },
                cancellationToken: cancellationToken));

        if (rows == 0)
            throw new NotFoundException("Driver", driverId);
    }

    public async Task<int> CreateViolationAsync(
        int tenantId, int driverId, CreateDriverViolationRequest body, string createdBy,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                @"INSERT INTO DriverViolations (TenantId, DriverId, ViolationType, Severity, OccurredAt, Description, BookingId, GpsAlertId, Status, CreatedBy, CreatedAt)
                  VALUES (@TenantId, @DriverId, @ViolationType, @Severity, @OccurredAt, @Description, @BookingId, @GpsAlertId, N'Open', @CreatedBy, GETUTCDATE());
                  SELECT CAST(SCOPE_IDENTITY() AS INT);",
                new
                {
                    TenantId = tenantId,
                    DriverId = driverId,
                    ViolationType = body.ViolationType.Trim(),
                    Severity = body.Severity.Trim(),
                    body.OccurredAt,
                    body.Description,
                    body.BookingId,
                    body.GpsAlertId,
                    CreatedBy = createdBy
                },
                cancellationToken: cancellationToken));
    }

    public async Task<int> CreateAttendanceAsync(
        int tenantId, int driverId, CreateDriverAttendanceRequest body,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                @"INSERT INTO DriverAttendance (TenantId, DriverId, AttendanceDate, Status, CheckInAt, CheckOutAt, Notes, CreatedAt)
                  VALUES (@TenantId, @DriverId, @AttendanceDate, @Status, @CheckInAt, @CheckOutAt, @Notes, GETUTCDATE());
                  SELECT CAST(SCOPE_IDENTITY() AS INT);",
                new
                {
                    TenantId = tenantId,
                    DriverId = driverId,
                    AttendanceDate = body.AttendanceDate.Date,
                    Status = body.Status.Trim(),
                    body.CheckInAt,
                    body.CheckOutAt,
                    body.Notes
                },
                cancellationToken: cancellationToken));
    }

    public async Task<DriverPerformanceSummaryDto?> GetPerformanceSummaryAsync(
        int tenantId, int driverId, DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        var driver = await connection.QuerySingleOrDefaultAsync<(string FullName, decimal? Rating, int? YearsExperience)>(
            new CommandDefinition(
                "SELECT FullName, Rating, YearsExperience FROM Drivers WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
                new { Id = driverId, TenantId = tenantId },
                cancellationToken: cancellationToken));

        if (driver.FullName is null)
            return null;

        var trips = await connection.QuerySingleAsync<(int Total, int Completed, decimal Revenue)>(
            new CommandDefinition(
                @"SELECT
                    COUNT(*) AS Total,
                    SUM(CASE WHEN Status = @Completed THEN 1 ELSE 0 END) AS Completed,
                    ISNULL(SUM(CASE WHEN Status = @Completed THEN TotalAmount ELSE 0 END), 0) AS Revenue
                  FROM Bookings
                  WHERE DriverId = @DriverId AND TenantId = @TenantId AND IsDeleted = 0
                    AND PickupTime >= @From AND PickupTime <= @To",
                new { DriverId = driverId, TenantId = tenantId, From = from, To = to, Completed = (int)BookingStatus.Completed },
                cancellationToken: cancellationToken));

        var violations = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                "SELECT COUNT(*) FROM DriverViolations WHERE DriverId = @DriverId AND TenantId = @TenantId AND IsDeleted = 0",
                new { DriverId = driverId, TenantId = tenantId },
                cancellationToken: cancellationToken));

        var attendancePresent = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                @"SELECT COUNT(*) FROM DriverAttendance
                  WHERE DriverId = @DriverId AND TenantId = @TenantId AND IsDeleted = 0 AND Status = N'Present'",
                new { DriverId = driverId, TenantId = tenantId },
                cancellationToken: cancellationToken));

        var completionRate = trips.Total > 0 ? Math.Round(trips.Completed * 100m / trips.Total, 1) : 0;

        return new DriverPerformanceSummaryDto(
            driverId,
            driver.FullName,
            driver.Rating,
            driver.YearsExperience,
            trips.Total,
            trips.Completed,
            trips.Revenue,
            completionRate,
            violations,
            attendancePresent);
    }

    public async Task<PagedResult<DriverViolationDto>> GetViolationsAsync(
        int tenantId, int driverId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var offset = (page - 1) * pageSize;

        var items = (await connection.QueryAsync<DriverViolationDto>(
            new CommandDefinition(
                @"SELECT Id, ViolationType, Severity, OccurredAt, Description, BookingId, Status, CreatedAt
                  FROM DriverViolations
                  WHERE DriverId = @DriverId AND TenantId = @TenantId AND IsDeleted = 0
                  ORDER BY OccurredAt DESC
                  OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY",
                new { DriverId = driverId, TenantId = tenantId, Offset = offset, PageSize = pageSize },
                cancellationToken: cancellationToken))).ToList();

        var total = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                "SELECT COUNT(*) FROM DriverViolations WHERE DriverId = @DriverId AND TenantId = @TenantId AND IsDeleted = 0",
                new { DriverId = driverId, TenantId = tenantId },
                cancellationToken: cancellationToken));

        return new PagedResult<DriverViolationDto>
        {
            Items = items, TotalCount = total, Page = page, PageSize = pageSize
        };
    }

    public async Task<IReadOnlyList<DriverAttendanceDto>> GetAttendanceAsync(
        int tenantId, int driverId, DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return (await connection.QueryAsync<DriverAttendanceDto>(
            new CommandDefinition(
                @"SELECT Id, AttendanceDate, Status, CheckInAt, CheckOutAt, Notes, CreatedAt
                  FROM DriverAttendance
                  WHERE DriverId = @DriverId AND TenantId = @TenantId AND IsDeleted = 0
                    AND AttendanceDate >= @From AND AttendanceDate <= @To
                  ORDER BY AttendanceDate DESC",
                new { DriverId = driverId, TenantId = tenantId, From = from, To = to },
                cancellationToken: cancellationToken))).ToList();
    }

    public async Task<DriverLocationDto?> GetLocationAsync(
        int tenantId, int driverId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<DriverLocationDto>(
            new CommandDefinition(
                @"SELECT TOP 1
                    gp.Latitude, gp.Longitude, gp.Speed, gp.Ignition, gp.RecordedAt AS LastSeen,
                    v.Id AS VehicleId, v.RegistrationNumber AS VehicleRegistration,
                    CAST(CASE WHEN gd.LastSeenAt >= DATEADD(minute, -15, GETUTCDATE()) THEN 1 ELSE 0 END AS BIT) AS GpsOnline
                  FROM AssignmentHistory ah
                  INNER JOIN Vehicles v ON v.Id = ah.VehicleId AND v.IsDeleted = 0
                  LEFT JOIN GpsDevices gd ON gd.Id = v.GpsDeviceId AND gd.IsDeleted = 0
                  OUTER APPLY (
                    SELECT TOP 1 p.Latitude, p.Longitude, p.Speed, p.Ignition, p.RecordedAt
                    FROM GpsPositions p
                    WHERE (p.DriverId = @DriverId OR p.VehicleId = v.Id)
                    ORDER BY p.RecordedAt DESC
                  ) gp
                  WHERE ah.DriverId = @DriverId AND ah.TenantId = @TenantId
                    AND ah.Status = N'Active' AND ah.IsDeleted = 0
                  ORDER BY ah.StartAt DESC",
                new { DriverId = driverId, TenantId = tenantId },
                cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<DriverLocationPointDto>> GetLocationHistoryAsync(
        int tenantId, int driverId, DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        var vehicleId = await connection.ExecuteScalarAsync<int?>(
            new CommandDefinition(
                @"SELECT TOP 1 VehicleId FROM AssignmentHistory
                  WHERE DriverId = @DriverId AND TenantId = @TenantId AND IsDeleted = 0
                  ORDER BY CASE WHEN Status = N'Active' THEN 0 ELSE 1 END, StartAt DESC",
                new { DriverId = driverId, TenantId = tenantId },
                cancellationToken: cancellationToken));

        if (!vehicleId.HasValue)
            return [];

        return (await connection.QueryAsync<DriverLocationPointDto>(
            new CommandDefinition(
                @"SELECT Latitude, Longitude, Speed, RecordedAt AS Timestamp
                  FROM GpsPositions
                  WHERE (DriverId = @DriverId OR VehicleId = @VehicleId)
                    AND RecordedAt >= @From AND RecordedAt <= @To
                  ORDER BY RecordedAt ASC",
                new { DriverId = driverId, VehicleId = vehicleId.Value, From = from, To = to },
                cancellationToken: cancellationToken))).ToList();
    }

    private static Task<bool> ExistsOn(IDbConnection connection, int tenantId, int id, CancellationToken cancellationToken)
        => connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT CASE WHEN EXISTS(SELECT 1 FROM Drivers WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0) THEN 1 ELSE 0 END",
                new { Id = id, TenantId = tenantId },
                cancellationToken: cancellationToken));
}
