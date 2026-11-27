using System.Data;
using System.Globalization;
using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.GpsTracking.Geofences;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories;

/// <summary>Writes for the geofences workspace. Ids from the request are only used after a tenant check.</summary>
public sealed partial class GeofenceWorkspaceRepository
{
    public async Task<ApiResponse<int>> SaveAsync(int? id, SaveGeofenceDto dto, CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        using var connection = dbFactory.CreateConnection();
        if (connection.State != ConnectionState.Open)
            connection.Open();
        using var tx = connection.BeginTransaction();

        var result = await SaveCoreAsync(connection, tx, tenantId, id, dto, cancellationToken);
        if (result.Success)
            tx.Commit();
        else
            tx.Rollback();
        return result;
    }

    private async Task<ApiResponse<int>> SaveCoreAsync(
        IDbConnection connection,
        IDbTransaction tx,
        int tenantId,
        int? id,
        SaveGeofenceDto dto,
        CancellationToken cancellationToken)
    {
        var name = dto.Name.Trim();
        if (id is int existingId)
        {
            var exists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT COUNT(1) FROM Geofences WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
                new { Id = existingId, TenantId = tenantId }, tx, cancellationToken: cancellationToken));
            if (exists == 0)
                return ApiResponse<int>.FailResponse("Geofence not found.");
        }

        var duplicate = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            SELECT COUNT(1) FROM Geofences
            WHERE TenantId = @TenantId AND IsDeleted = 0 AND LOWER(Name) = LOWER(@Name) AND (@Id IS NULL OR Id <> @Id)
            """,
            new { TenantId = tenantId, Name = name, Id = id }, tx, cancellationToken: cancellationToken));
        if (duplicate > 0)
            return ApiResponse<int>.FailResponse("A geofence with this name already exists.");

        // Type, colour and the red-is-restricted rule.
        var type = string.IsNullOrWhiteSpace(dto.Type) ? null : dto.Type.Trim();
        var builtIn = GeofenceTypeCatalog.Find(type);
        var custom = type is null || builtIn is not null
            ? default
            : await connection.QueryFirstOrDefaultAsync<(string? Color, bool IsRestricted)>(new CommandDefinition(
                "SELECT Color, IsRestricted FROM GeofenceTypes WHERE TenantId = @TenantId AND IsDeleted = 0 AND Name = @Name",
                new { TenantId = tenantId, Name = type }, tx, cancellationToken: cancellationToken));
        var isRestricted = builtIn?.IsRestricted ?? custom.IsRestricted;
        var color = !string.IsNullOrWhiteSpace(dto.Color) ? dto.Color! : builtIn?.Color ?? custom.Color ?? "#0f766e";
        if (string.Equals(color, GeofenceTypeCatalog.RestrictedColor, StringComparison.OrdinalIgnoreCase) && !isRestricted)
            return ApiResponse<int>.FailResponse("Red is reserved for restricted zones. Pick another colour or a restricted type.");

        if (dto.GroupId is int groupId)
        {
            var groupOwned = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT COUNT(1) FROM GeofenceGroups WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
                new { Id = groupId, TenantId = tenantId }, tx, cancellationToken: cancellationToken));
            if (groupOwned == 0)
                return ApiResponse<int>.FailResponse("Group not found.");
        }

        var areaType = GpsGeoHelper.NormalizeAreaType(dto.AreaType);
        var (centerLat, centerLng, radius) = GpsGeoHelper.NormalizeGeometry(
            areaType, dto.CenterLat, dto.CenterLng, dto.RadiusMeters, dto.GeoJson);
        int? corridorWidth = areaType == "corridor" ? dto.CorridorWidthMeters : null;
        var (area, perimeter) = GpsGeoHelper.ComputeAreaAndPerimeter(areaType, radius, dto.GeoJson, corridorWidth);
        var appliesToAll = dto.AppliesTo == GeofenceAppliesTo.All;
        var by = currentUser.UserId?.ToString();

        var row = new
        {
            Id = id,
            TenantId = tenantId,
            Name = name,
            AreaType = areaType,
            CenterLat = centerLat,
            CenterLng = centerLng,
            RadiusMeters = radius,
            dto.GeoJson,
            Color = color,
            Category = type,
            Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
            dto.IsActive,
            dto.GroupId,
            Address = string.IsNullOrWhiteSpace(dto.Address) ? null : dto.Address.Trim(),
            dto.BufferMeters,
            CorridorWidthMeters = corridorWidth,
            AreaSquareMeters = area,
            PerimeterMeters = perimeter,
            AppliesToAll = appliesToAll,
            dto.DwellMinutes,
            dto.SpeedLimitKmh,
            dto.IdleMinutes,
            NotArrivedBy = ParseTime(dto.NotArrivedBy),
            dto.EngineBlockOnExit,
            ScheduleDays = (byte?)dto.ScheduleDays,
            ScheduleStart = dto.ScheduleDays.HasValue ? ParseTime(dto.ScheduleStart) : null,
            ScheduleEnd = dto.ScheduleDays.HasValue ? ParseTime(dto.ScheduleEnd) : null,
            AfterHoursExitCritical = dto.AfterHoursExitCritical && dto.ScheduleDays.HasValue,
            NotifyChannels = GeofenceNotifyChannels.Format(dto.NotifyChannels),
            dto.CooldownMinutes,
            By = by
        };

        int geofenceId;
        if (id is null)
        {
            geofenceId = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                """
                INSERT INTO Geofences
                    (TenantId, Name, AreaType, CenterLat, CenterLng, RadiusMeters, GeoJson, Color, Category, Description,
                     IsActive, GroupId, Address, BufferMeters, CorridorWidthMeters, AreaSquareMeters, PerimeterMeters,
                     AppliesToAll, DwellMinutes, SpeedLimitKmh, IdleMinutes, NotArrivedBy, EngineBlockOnExit,
                     ScheduleDays, ScheduleStart, ScheduleEnd, AfterHoursExitCritical, NotifyChannels, CooldownMinutes,
                     CreatedAt, CreatedBy, IsDeleted)
                OUTPUT INSERTED.Id
                VALUES
                    (@TenantId, @Name, @AreaType, @CenterLat, @CenterLng, @RadiusMeters, @GeoJson, @Color, @Category, @Description,
                     @IsActive, @GroupId, @Address, @BufferMeters, @CorridorWidthMeters, @AreaSquareMeters, @PerimeterMeters,
                     @AppliesToAll, @DwellMinutes, @SpeedLimitKmh, @IdleMinutes, @NotArrivedBy, @EngineBlockOnExit,
                     @ScheduleDays, @ScheduleStart, @ScheduleEnd, @AfterHoursExitCritical, @NotifyChannels, @CooldownMinutes,
                     GETUTCDATE(), @By, 0)
                """,
                row, tx, cancellationToken: cancellationToken));
        }
        else
        {
            geofenceId = id.Value;
            await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE Geofences SET
                    Name = @Name, AreaType = @AreaType, CenterLat = @CenterLat, CenterLng = @CenterLng,
                    RadiusMeters = @RadiusMeters, GeoJson = @GeoJson, Color = @Color, Category = @Category,
                    Description = @Description, IsActive = @IsActive, GroupId = @GroupId, Address = @Address,
                    BufferMeters = @BufferMeters, CorridorWidthMeters = @CorridorWidthMeters,
                    AreaSquareMeters = @AreaSquareMeters, PerimeterMeters = @PerimeterMeters, AppliesToAll = @AppliesToAll,
                    DwellMinutes = @DwellMinutes, SpeedLimitKmh = @SpeedLimitKmh, IdleMinutes = @IdleMinutes,
                    NotArrivedBy = @NotArrivedBy, EngineBlockOnExit = @EngineBlockOnExit, ScheduleDays = @ScheduleDays,
                    ScheduleStart = @ScheduleStart, ScheduleEnd = @ScheduleEnd, AfterHoursExitCritical = @AfterHoursExitCritical,
                    NotifyChannels = @NotifyChannels, CooldownMinutes = @CooldownMinutes,
                    UpdatedAt = GETUTCDATE(), UpdatedBy = @By
                WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0
                """,
                row, tx, cancellationToken: cancellationToken));
        }

        await ReplaceAssignmentsAsync(connection, tx, tenantId, geofenceId, dto, by, cancellationToken);
        await ReplaceRecipientsAsync(connection, tx, tenantId, geofenceId, dto, cancellationToken);
        await UpsertEntryExitRuleAsync(connection, tx, tenantId, geofenceId, dto.AlertOnEnter, dto.AlertOnExit, by, cancellationToken);

        logger.LogInformation("Geofence {GeofenceId} saved for tenant {TenantId} ({Shape})", geofenceId, tenantId, areaType);
        return ApiResponse<int>.SuccessResponse(geofenceId, id is null ? "Geofence created." : "Geofence updated.");
    }

    /// <summary>Only the chosen "applies to" scope is kept; ids outside the tenant are dropped by the SELECTs.</summary>
    private static async Task ReplaceAssignmentsAsync(
        IDbConnection connection, IDbTransaction tx, int tenantId, int geofenceId, SaveGeofenceDto dto, string? by,
        CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE a SET IsDeleted = 1, UpdatedAt = GETUTCDATE(), UpdatedBy = @By
            FROM GeofenceAssignments a
            INNER JOIN Geofences g ON g.Id = a.GeofenceId AND g.TenantId = @TenantId
            WHERE a.GeofenceId = @GeofenceId AND a.IsDeleted = 0
            """,
            new { TenantId = tenantId, GeofenceId = geofenceId, By = by }, tx, cancellationToken: cancellationToken));

        var args = new { TenantId = tenantId, GeofenceId = geofenceId, By = by };
        switch (dto.AppliesTo)
        {
            case GeofenceAppliesTo.Vehicles when dto.VehicleIds is { Length: > 0 }:
                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    INSERT INTO GeofenceAssignments (GeofenceId, VehicleId, CreatedAt, CreatedBy, IsDeleted)
                    SELECT @GeofenceId, v.Id, GETUTCDATE(), @By, 0
                    FROM Vehicles v WHERE v.Id IN @Ids AND v.TenantId = @TenantId AND v.IsDeleted = 0
                    """,
                    new { args.TenantId, args.GeofenceId, args.By, Ids = dto.VehicleIds.Distinct().ToArray() },
                    tx, cancellationToken: cancellationToken));
                break;

            case GeofenceAppliesTo.Groups:
                if (dto.BranchIds is { Length: > 0 })
                {
                    await connection.ExecuteAsync(new CommandDefinition(
                        """
                        INSERT INTO GeofenceAssignments (GeofenceId, BranchId, CreatedAt, CreatedBy, IsDeleted)
                        SELECT @GeofenceId, b.Id, GETUTCDATE(), @By, 0
                        FROM Branches b WHERE b.Id IN @Ids AND b.TenantId = @TenantId
                        """,
                        new { args.TenantId, args.GeofenceId, args.By, Ids = dto.BranchIds.Distinct().ToArray() },
                        tx, cancellationToken: cancellationToken));
                }

                if (dto.DepartmentIds is { Length: > 0 })
                {
                    await connection.ExecuteAsync(new CommandDefinition(
                        """
                        INSERT INTO GeofenceAssignments (GeofenceId, DepartmentId, CreatedAt, CreatedBy, IsDeleted)
                        SELECT @GeofenceId, d.Id, GETUTCDATE(), @By, 0
                        FROM Departments d WHERE d.Id IN @Ids AND d.TenantId = @TenantId
                        """,
                        new { args.TenantId, args.GeofenceId, args.By, Ids = dto.DepartmentIds.Distinct().ToArray() },
                        tx, cancellationToken: cancellationToken));
                }

                break;

            case GeofenceAppliesTo.Drivers when dto.DriverIds is { Length: > 0 }:
                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    INSERT INTO GeofenceAssignments (GeofenceId, DriverId, CreatedAt, CreatedBy, IsDeleted)
                    SELECT @GeofenceId, d.Id, GETUTCDATE(), @By, 0
                    FROM Drivers d WHERE d.Id IN @Ids AND d.TenantId = @TenantId AND d.IsDeleted = 0
                    """,
                    new { args.TenantId, args.GeofenceId, args.By, Ids = dto.DriverIds.Distinct().ToArray() },
                    tx, cancellationToken: cancellationToken));
                break;
        }
    }

    private static async Task ReplaceRecipientsAsync(
        IDbConnection connection, IDbTransaction tx, int tenantId, int geofenceId, SaveGeofenceDto dto,
        CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM GeofenceRecipients WHERE GeofenceId = @GeofenceId AND TenantId = @TenantId",
            new { TenantId = tenantId, GeofenceId = geofenceId }, tx, cancellationToken: cancellationToken));

        if (dto.RecipientRoleIds is { Length: > 0 })
        {
            // A role counts when it is the tenant's own or a shared role held by one of the tenant's users.
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO GeofenceRecipients (TenantId, GeofenceId, RoleId)
                SELECT @TenantId, @GeofenceId, r.Id
                FROM Roles r
                WHERE r.Id IN @Ids AND r.IsActive = 1
                  AND (r.TenantId = @TenantId OR EXISTS (
                        SELECT 1 FROM UserRoles ur
                        INNER JOIN Users u ON u.Id = ur.UserId AND u.TenantId = @TenantId AND u.IsDeleted = 0
                        WHERE ur.RoleId = r.Id))
                """,
                new { TenantId = tenantId, GeofenceId = geofenceId, Ids = dto.RecipientRoleIds.Distinct().ToArray() },
                tx, cancellationToken: cancellationToken));
        }

        if (dto.RecipientUserIds is { Length: > 0 })
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO GeofenceRecipients (TenantId, GeofenceId, UserId)
                SELECT @TenantId, @GeofenceId, u.Id
                FROM Users u WHERE u.Id IN @Ids AND u.TenantId = @TenantId AND u.IsDeleted = 0
                """,
                new { TenantId = tenantId, GeofenceId = geofenceId, Ids = dto.RecipientUserIds.Distinct().ToArray() },
                tx, cancellationToken: cancellationToken));
        }
    }

    /// <summary>One geofence-level Entry/Exit rule in GpsAlertRules, as the Alerts page and the ingest expect.</summary>
    private static async Task UpsertEntryExitRuleAsync(
        IDbConnection connection, IDbTransaction tx, int tenantId, int geofenceId, bool onEnter, bool onExit, string? by,
        CancellationToken cancellationToken)
    {
        var active = onEnter || onExit;
        var existing = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            """
            SELECT TOP 1 Id FROM GpsAlertRules
            WHERE TenantId = @TenantId AND GeofenceId = @GeofenceId AND VehicleId IS NULL AND IsDeleted = 0
            ORDER BY Id
            """,
            new { TenantId = tenantId, GeofenceId = geofenceId }, tx, cancellationToken: cancellationToken));

        if (existing is int ruleId)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE GpsAlertRules
                SET AlertOnEnter = @OnEnter, AlertOnExit = @OnExit, IsActive = @Active, UpdatedAt = GETUTCDATE(), UpdatedBy = @By
                WHERE Id = @Id AND TenantId = @TenantId
                """,
                new { Id = ruleId, TenantId = tenantId, OnEnter = onEnter, OnExit = onExit, Active = active, By = by },
                tx, cancellationToken: cancellationToken));
            return;
        }

        if (!active)
            return;

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO GpsAlertRules (TenantId, VehicleId, SpeedLimitKmh, GeofenceId, AlertOnEnter, AlertOnExit, IsActive, CreatedAt, CreatedBy, IsDeleted)
            VALUES (@TenantId, NULL, NULL, @GeofenceId, @OnEnter, @OnExit, 1, GETUTCDATE(), @By, 0)
            """,
            new { TenantId = tenantId, GeofenceId = geofenceId, OnEnter = onEnter, OnExit = onExit, By = by },
            tx, cancellationToken: cancellationToken));
    }

    public async Task<ApiResponse<int>> SaveGroupAsync(int? id, SaveGeofenceGroupDto group, CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        var name = group.Name.Trim();
        using var connection = dbFactory.CreateConnection();

        var duplicate = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            SELECT COUNT(1) FROM GeofenceGroups
            WHERE TenantId = @TenantId AND IsDeleted = 0 AND LOWER(Name) = LOWER(@Name) AND (@Id IS NULL OR Id <> @Id)
            """,
            new { TenantId = tenantId, Name = name, Id = id },
            cancellationToken: cancellationToken));
        if (duplicate > 0)
            return ApiResponse<int>.FailResponse("A group with this name already exists.");

        if (id is null)
        {
            var newId = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                """
                INSERT INTO GeofenceGroups (TenantId, Name, SortOrder, CreatedBy)
                OUTPUT INSERTED.Id
                VALUES (@TenantId, @Name, @SortOrder, @By)
                """,
                new { TenantId = tenantId, Name = name, group.SortOrder, By = currentUser.UserId?.ToString() },
                cancellationToken: cancellationToken));
            return ApiResponse<int>.SuccessResponse(newId, "Group created.");
        }

        var rows = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE GeofenceGroups SET Name = @Name, SortOrder = @SortOrder, UpdatedAt = GETUTCDATE(), UpdatedBy = @By
            WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0
            """,
            new { Id = id, TenantId = tenantId, Name = name, group.SortOrder, By = currentUser.UserId?.ToString() },
            cancellationToken: cancellationToken));
        return rows > 0
            ? ApiResponse<int>.SuccessResponse(id.Value, "Group updated.")
            : ApiResponse<int>.FailResponse("Group not found.");
    }

    public async Task<ApiResponse<bool>> DeleteGroupAsync(int id, CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE GeofenceGroups SET IsDeleted = 1, UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0;
            UPDATE Geofences SET GroupId = NULL WHERE GroupId = @Id AND TenantId = @TenantId;
            """,
            new { Id = id, TenantId = tenantId },
            cancellationToken: cancellationToken));
        return rows > 0
            ? ApiResponse<bool>.SuccessResponse(true, "Group deleted. Its geofences are now ungrouped.")
            : ApiResponse<bool>.FailResponse("Group not found.");
    }

    public async Task<ApiResponse<int>> CreateTypeAsync(CreateGeofenceTypeDto type, CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        var name = type.Name.Trim();
        using var connection = dbFactory.CreateConnection();
        var duplicate = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM GeofenceTypes WHERE TenantId = @TenantId AND IsDeleted = 0 AND LOWER(Name) = LOWER(@Name)",
            new { TenantId = tenantId, Name = name },
            cancellationToken: cancellationToken));
        if (duplicate > 0)
            return ApiResponse<int>.FailResponse("That type already exists.");

        var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            INSERT INTO GeofenceTypes (TenantId, Name, Color, IsRestricted, CreatedBy)
            OUTPUT INSERTED.Id
            VALUES (@TenantId, @Name, @Color, @IsRestricted, @By)
            """,
            new { TenantId = tenantId, Name = name, type.Color, type.IsRestricted, By = currentUser.UserId?.ToString() },
            cancellationToken: cancellationToken));
        return ApiResponse<int>.SuccessResponse(id, "Type added.");
    }

    public async Task<ApiResponse<BulkGeofenceResultDto>> BulkAsync(BulkGeofenceActionDto action, CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        var by = currentUser.UserId?.ToString();
        using var connection = dbFactory.CreateConnection();
        if (connection.State != ConnectionState.Open)
            connection.Open();
        using var tx = connection.BeginTransaction();

        var ids = (await connection.QueryAsync<int>(new CommandDefinition(
            "SELECT Id FROM Geofences WHERE Id IN @Ids AND TenantId = @TenantId AND IsDeleted = 0",
            new { Ids = action.Ids.Distinct().ToArray(), TenantId = tenantId }, tx, cancellationToken: cancellationToken))).ToArray();
        if (ids.Length == 0)
            return ApiResponse<BulkGeofenceResultDto>.FailResponse("No matching geofences.");

        var args = new { Ids = ids, TenantId = tenantId, By = by };
        int affected;
        switch (action.Action)
        {
            case GeofenceBulkAction.Assign:
                affected = await connection.ExecuteAsync(new CommandDefinition(
                    """
                    INSERT INTO GeofenceAssignments (GeofenceId, VehicleId, CreatedAt, CreatedBy, IsDeleted)
                    SELECT g.Id, v.Id, GETUTCDATE(), @By, 0
                    FROM Geofences g
                    CROSS JOIN Vehicles v
                    WHERE g.Id IN @Ids AND g.TenantId = @TenantId AND g.IsDeleted = 0
                      AND v.Id IN @VehicleIds AND v.TenantId = @TenantId AND v.IsDeleted = 0
                      AND NOT EXISTS (SELECT 1 FROM GeofenceAssignments a
                                      WHERE a.GeofenceId = g.Id AND a.VehicleId = v.Id AND a.IsDeleted = 0)
                    """,
                    new { args.Ids, args.TenantId, args.By, VehicleIds = (action.VehicleIds ?? []).Distinct().ToArray() },
                    tx, cancellationToken: cancellationToken));
                break;

            case GeofenceBulkAction.Group:
                if (action.GroupId is int groupId)
                {
                    var owned = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                        "SELECT COUNT(1) FROM GeofenceGroups WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
                        new { Id = groupId, TenantId = tenantId }, tx, cancellationToken: cancellationToken));
                    if (owned == 0)
                        return ApiResponse<BulkGeofenceResultDto>.FailResponse("Group not found.");
                }

                affected = await connection.ExecuteAsync(new CommandDefinition(
                    "UPDATE Geofences SET GroupId = @GroupId, UpdatedAt = GETUTCDATE(), UpdatedBy = @By WHERE Id IN @Ids AND TenantId = @TenantId",
                    new { args.Ids, args.TenantId, args.By, action.GroupId }, tx, cancellationToken: cancellationToken));
                break;

            case GeofenceBulkAction.Enable:
            case GeofenceBulkAction.Disable:
                affected = await connection.ExecuteAsync(new CommandDefinition(
                    "UPDATE Geofences SET IsActive = @Active, UpdatedAt = GETUTCDATE(), UpdatedBy = @By WHERE Id IN @Ids AND TenantId = @TenantId",
                    new { args.Ids, args.TenantId, args.By, Active = action.Action == GeofenceBulkAction.Enable },
                    tx, cancellationToken: cancellationToken));
                break;

            case GeofenceBulkAction.Delete:
                affected = await connection.ExecuteAsync(new CommandDefinition(
                    "UPDATE Geofences SET IsDeleted = 1, UpdatedAt = GETUTCDATE(), UpdatedBy = @By WHERE Id IN @Ids AND TenantId = @TenantId",
                    args, tx, cancellationToken: cancellationToken));
                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    UPDATE a SET IsDeleted = 1, UpdatedAt = GETUTCDATE()
                    FROM GeofenceAssignments a
                    INNER JOIN Geofences g ON g.Id = a.GeofenceId AND g.TenantId = @TenantId
                    WHERE a.GeofenceId IN @Ids AND a.IsDeleted = 0;
                    UPDATE GpsAlertRules SET IsDeleted = 1, IsActive = 0, UpdatedAt = GETUTCDATE()
                    WHERE GeofenceId IN @Ids AND TenantId = @TenantId AND IsDeleted = 0;
                    """,
                    args, tx, cancellationToken: cancellationToken));
                break;

            default:
                return ApiResponse<BulkGeofenceResultDto>.FailResponse("Unknown action.");
        }

        tx.Commit();
        return ApiResponse<BulkGeofenceResultDto>.SuccessResponse(new BulkGeofenceResultDto { Affected = affected }, "Done.");
    }

    public async Task<ApiResponse<ImportGeofencesResultDto>> ImportAsync(
        IReadOnlyList<SaveGeofenceDto> items, CancellationToken cancellationToken = default)
    {
        var created = 0;
        var skipped = new List<ImportSkippedDto>();
        foreach (var item in items)
        {
            // Each row in its own transaction: one bad row never undoes the others.
            var result = await SaveAsync(null, item, cancellationToken);
            if (result.Success)
                created++;
            else
                skipped.Add(new ImportSkippedDto { Name = item.Name, Reason = result.Message });
        }

        return ApiResponse<ImportGeofencesResultDto>.SuccessResponse(
            new ImportGeofencesResultDto { Created = created, Skipped = skipped },
            $"{created} imported, {skipped.Count} skipped.");
    }

    private static TimeSpan? ParseTime(string? value)
        => TimeSpan.TryParseExact(value, @"hh\:mm", CultureInfo.InvariantCulture, out var time) ? time : null;
}
