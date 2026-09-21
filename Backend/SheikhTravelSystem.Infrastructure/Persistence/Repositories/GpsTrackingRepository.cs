using System.Globalization;
using System.Text;
using SheikhTravelSystem.Application.Features.Notifications;
using MediatR;
using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Infrastructure.Persistence;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.GpsTracking;
using SheikhTravelSystem.Application.Features.GpsTracking.Commands;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Queries;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;
using SheikhTravelSystem.Application.Features.GpsTracking.Traccar;
using SheikhTravelSystem.Application.Features.GpsTracking.Trackers;
using SheikhTravelSystem.Application.Features.MaintenanceModule;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories;

public sealed partial class GpsTrackingRepository(
    IDbConnectionFactory dbFactory,
    ITenantContext tenantContext,
    ICurrentUserService currentUser,
    IAppCache cache,
    IDataScopeEngine dataScopeEngine,
    IOptions<GpsSettings> gpsSettings,
    IOptions<TraccarOptions> traccarOptions,
    IGpsFleetStatusCalculator fleetStatusCalculator,
    ITripVehicleQueryHelper tripVehicleQuery,
    IGpsTraccarFleetFetcher traccarFleetFetcher,
    IGpsPositionIngestionHelper positionIngestion,
    IGpsDeviceTelemetryUpdater telemetryUpdater,
    IGpsTripPersistenceService tripPersistence,
    IGpsAlertWriter alertWriter,
    MediatR.IMediator mediator,
    ITraccarClient traccar,
    IReverseGeocodingService geocoder,
    IGpsAddressBackfillQueue addressBackfill,
    INotificationDecisionEngine decisionEngine,
    IEscalationService escalation,
    ILocationBroadcastService broadcaster,
    ILogger<GpsTrackingRepository> logger) : IGpsTrackingRepository
{
    private readonly IGpsAlertWriter _alertWriter = alertWriter;
    private readonly IOptions<TraccarOptions> _traccarOptions = traccarOptions;
    private readonly INotificationDecisionEngine _decisionEngine = decisionEngine;
    public async Task<ApiResponse<IReadOnlyList<AnalyticsReportScheduleDto>>> ListAnalyticsReportSchedulesAsync(ListAnalyticsReportSchedulesQuery request, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        using var connection = dbFactory.CreateConnection();

        var rows = await connection.QueryAsync<AnalyticsReportScheduleRow>(new CommandDefinition("""
            SELECT Id, ReportType, FiltersJson, Frequency, Recipients, NextRunAt, LastRunAt, LastRunStatus, IsActive
            FROM GpsAnalyticsReportSchedules
            WHERE TenantId = @TenantId AND IsDeleted = 0
            ORDER BY CreatedAt DESC
            """, new { TenantId = tenantId }, cancellationToken: cancellationToken));

        return ApiResponse<IReadOnlyList<AnalyticsReportScheduleDto>>.SuccessResponse(
            rows.Select(r => r.ToDto()).ToList());
    }

    public async Task<ApiResponse<int>> CreateAnalyticsReportScheduleAsync(CreateAnalyticsReportScheduleCommand request, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var body = request.Body;
        var nextRun = AnalyticsReportHelper.ComputeNextRunAt(body.Frequency);
        var filtersJson = AnalyticsReportHelper.SerializeFilters(body.Filters);
        var createdBy = currentUser.UserId?.ToString() ?? "system";

        using var connection = dbFactory.CreateConnection();
        var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            INSERT INTO GpsAnalyticsReportSchedules
                (TenantId, ReportType, FiltersJson, Frequency, Recipients, NextRunAt, LastRunStatus, CreatedBy)
            VALUES
                (@TenantId, @ReportType, @FiltersJson, @Frequency, @Recipients, @NextRunAt, N'Pending', @CreatedBy);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """, new
        {
            TenantId = tenantId,
            ReportType = AnalyticsReportHelper.NormalizeReportType(body.ReportType),
            FiltersJson = filtersJson,
            body.Frequency,
            body.Recipients,
            NextRunAt = nextRun,
            CreatedBy = createdBy
        }, cancellationToken: cancellationToken));

        logger.LogInformation("Analytics report schedule {ScheduleId} queued (email delivery stubbed)", id);
        return ApiResponse<int>.SuccessResponse(id);
        }

    public async Task<ApiResponse<bool>> UpdateAnalyticsReportScheduleAsync(UpdateAnalyticsReportScheduleCommand request, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        using var connection = dbFactory.CreateConnection();

        var exists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM GpsAnalyticsReportSchedules WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
            new { request.Id, TenantId = tenantId }, cancellationToken: cancellationToken));
        if (exists == 0) throw new NotFoundException("AnalyticsReportSchedule", request.Id);

        var body = request.Body;
        var nextRun = body.Frequency is not null
            ? AnalyticsReportHelper.ComputeNextRunAt(body.Frequency)
            : (DateTime?)null;

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE GpsAnalyticsReportSchedules SET
                Frequency = COALESCE(@Frequency, Frequency),
                Recipients = COALESCE(@Recipients, Recipients),
                FiltersJson = COALESCE(@FiltersJson, FiltersJson),
                IsActive = COALESCE(@IsActive, IsActive),
                NextRunAt = COALESCE(@NextRunAt, NextRunAt),
                UpdatedAt = GETUTCDATE(),
                UpdatedBy = @UpdatedBy
            WHERE Id = @Id AND TenantId = @TenantId
            """, new
        {
            request.Id,
            TenantId = tenantId,
            body.Frequency,
            body.Recipients,
            FiltersJson = body.Filters is null ? null : AnalyticsReportHelper.SerializeFilters(body.Filters),
            body.IsActive,
            NextRunAt = nextRun,
            UpdatedBy = currentUser.UserId?.ToString() ?? "system"
        }, cancellationToken: cancellationToken));

        return ApiResponse<bool>.SuccessResponse(true);
        }

    public async Task<ApiResponse<bool>> DeleteAnalyticsReportScheduleAsync(DeleteAnalyticsReportScheduleCommand request, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        using var connection = dbFactory.CreateConnection();

        var affected = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE GpsAnalyticsReportSchedules SET IsDeleted = 1, UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0
            """, new { request.Id, TenantId = tenantId }, cancellationToken: cancellationToken));

        if (affected == 0) throw new NotFoundException("AnalyticsReportSchedule", request.Id);
        return ApiResponse<bool>.SuccessResponse(true);
        }

    public async Task<ApiResponse<int>> CreateGeofenceAsync(CreateGeofenceCommand request, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var dto = request.Geofence;
        var areaType = GpsGeoHelper.NormalizeAreaType(dto.AreaType);

        var duplicate = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            @"SELECT COUNT(1) FROM Geofences WHERE IsDeleted = 0 AND LOWER(Name) = LOWER(@Name)",
            new { dto.Name },
            cancellationToken: cancellationToken));
        if (duplicate > 0)
            return ApiResponse<int>.FailResponse("A geofence with this name already exists.");

        var (centerLat, centerLng, radius) = GpsGeoHelper.NormalizeGeometry(areaType, dto.CenterLat, dto.CenterLng, dto.RadiusMeters, dto.GeoJson);

        var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            @"INSERT INTO Geofences (Name, AreaType, CenterLat, CenterLng, RadiusMeters, GeoJson, Color, Category, Description, IsActive, CreatedAt, CreatedBy, IsDeleted)
              OUTPUT INSERTED.Id
              VALUES (@Name, @AreaType, @CenterLat, @CenterLng, @RadiusMeters, @GeoJson, @Color, @Category, @Description, @IsActive, GETUTCDATE(), @CreatedBy, 0)",
            new
            {
                dto.Name,
                AreaType = areaType,
                CenterLat = centerLat,
                CenterLng = centerLng,
                RadiusMeters = radius,
                dto.GeoJson,
                Color = string.IsNullOrWhiteSpace(dto.Color) ? "#0f766e" : dto.Color,
                dto.Category,
                dto.Description,
                IsActive = dto.IsActive,
                CreatedBy = currentUser.UserId?.ToString()
            },
            cancellationToken: cancellationToken));

        return ApiResponse<int>.SuccessResponse(id, "Geofence created.");
        }

    public async Task<ApiResponse<bool>> UpdateGeofenceAsync(UpdateGeofenceCommand request, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var dto = request.Geofence;
        var areaType = GpsGeoHelper.NormalizeAreaType(dto.AreaType);

        var duplicate = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            @"SELECT COUNT(1) FROM Geofences WHERE IsDeleted = 0 AND LOWER(Name) = LOWER(@Name) AND Id <> @Id",
            new { dto.Name, request.Id },
            cancellationToken: cancellationToken));
        if (duplicate > 0)
            return ApiResponse<bool>.FailResponse("A geofence with this name already exists.");

        var (centerLat, centerLng, radius) = GpsGeoHelper.NormalizeGeometry(
            areaType, dto.CenterLat, dto.CenterLng, dto.RadiusMeters, dto.GeoJson);

        var rows = await connection.ExecuteAsync(new CommandDefinition(
            @"UPDATE Geofences SET Name = @Name, AreaType = @AreaType, CenterLat = @CenterLat, CenterLng = @CenterLng,
              RadiusMeters = @RadiusMeters, GeoJson = @GeoJson, Color = @Color, Category = @Category,
              Description = @Description, IsActive = @IsActive, UpdatedAt = GETUTCDATE(), UpdatedBy = @UpdatedBy
              WHERE Id = @Id AND IsDeleted = 0",
            new
            {
                request.Id,
                dto.Name,
                AreaType = areaType,
                CenterLat = centerLat,
                CenterLng = centerLng,
                RadiusMeters = radius,
                dto.GeoJson,
                Color = string.IsNullOrWhiteSpace(dto.Color) ? "#0f766e" : dto.Color,
                dto.Category,
                dto.Description,
                dto.IsActive,
                UpdatedBy = currentUser.UserId?.ToString()
            },
            cancellationToken: cancellationToken));

        return rows > 0
            ? ApiResponse<bool>.SuccessResponse(true, "Geofence updated.")
            : ApiResponse<bool>.FailResponse("Geofence not found.");
        }

    public async Task<ApiResponse<bool>> DeleteGeofenceAsync(DeleteGeofenceCommand request, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE Geofences SET IsDeleted = 1, UpdatedAt = GETUTCDATE() WHERE Id = @Id",
            new { request.Id },
            cancellationToken: cancellationToken));

        if (rows > 0)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE GeofenceAssignments SET IsDeleted = 1, UpdatedAt = GETUTCDATE() WHERE GeofenceId = @Id AND IsDeleted = 0",
                new { request.Id },
                cancellationToken: cancellationToken));
        }

        return rows > 0
            ? ApiResponse<bool>.SuccessResponse(true, "Geofence deleted.")
            : ApiResponse<bool>.FailResponse("Geofence not found.");
        }

    public async Task<ApiResponse<int>> DuplicateGeofenceAsync(DuplicateGeofenceCommand request, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var src = await connection.QueryFirstOrDefaultAsync(new CommandDefinition(
            @"SELECT Name, AreaType, CenterLat, CenterLng, RadiusMeters, GeoJson, Color, Category, Description
              FROM Geofences WHERE Id = @Id AND IsDeleted = 0",
            new { request.Id },
            cancellationToken: cancellationToken));

        if (src is null)
            return ApiResponse<int>.FailResponse("Geofence not found.");

        string baseName = src.Name;
        var newName = $"{baseName} (Copy)";
        var n = 2;
        while (await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                   @"SELECT COUNT(1) FROM Geofences WHERE IsDeleted = 0 AND LOWER(Name) = LOWER(@Name)",
                   new { Name = newName },
                   cancellationToken: cancellationToken)) > 0)
        {
            newName = $"{baseName} (Copy {n++})";
        }

        var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            @"INSERT INTO Geofences (Name, AreaType, CenterLat, CenterLng, RadiusMeters, GeoJson, Color, Category, Description, IsActive, CreatedAt, CreatedBy, IsDeleted)
              OUTPUT INSERTED.Id
              VALUES (@Name, @AreaType, @CenterLat, @CenterLng, @RadiusMeters, @GeoJson, @Color, @Category, @Description, 1, GETUTCDATE(), @CreatedBy, 0)",
            new
            {
                Name = newName,
                AreaType = (string)src.AreaType,
                CenterLat = (double)src.CenterLat,
                CenterLng = (double)src.CenterLng,
                RadiusMeters = (double)src.RadiusMeters,
                GeoJson = (string?)src.GeoJson,
                Color = (string?)src.Color ?? "#0f766e",
                Category = (string?)src.Category,
                Description = (string?)src.Description,
                CreatedBy = currentUser.UserId?.ToString()
            },
            cancellationToken: cancellationToken));

        return ApiResponse<int>.SuccessResponse(id, "Geofence duplicated.");
        }

    public async Task<ApiResponse<bool>> UpsertGeofenceAssignmentsAsync(UpsertGeofenceAssignmentsCommand request, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var exists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM Geofences WHERE Id = @Id AND IsDeleted = 0",
            new { Id = request.GeofenceId },
            cancellationToken: cancellationToken));
        if (exists == 0)
            return ApiResponse<bool>.FailResponse("Geofence not found.");

        var dto = request.Body;
        var createdBy = currentUser.UserId?.ToString();

        if (dto.ReplaceVehicles)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                @"UPDATE GeofenceAssignments SET IsDeleted = 1, UpdatedAt = GETUTCDATE()
                  WHERE GeofenceId = @GeofenceId AND VehicleId IS NOT NULL AND IsDeleted = 0",
                new { request.GeofenceId },
                cancellationToken: cancellationToken));
        }

        if (dto.VehicleIds is { Length: > 0 })
        {
            foreach (var vehicleId in dto.VehicleIds.Distinct())
            {
                var already = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                    @"SELECT COUNT(1) FROM GeofenceAssignments
                      WHERE GeofenceId = @GeofenceId AND VehicleId = @VehicleId AND IsDeleted = 0",
                    new { request.GeofenceId, VehicleId = vehicleId },
                    cancellationToken: cancellationToken));
                if (already > 0) continue;

                await connection.ExecuteAsync(new CommandDefinition(
                    @"INSERT INTO GeofenceAssignments (GeofenceId, VehicleId, CreatedAt, CreatedBy, IsDeleted)
                      VALUES (@GeofenceId, @VehicleId, GETUTCDATE(), @CreatedBy, 0)",
                    new { request.GeofenceId, VehicleId = vehicleId, CreatedBy = createdBy },
                    cancellationToken: cancellationToken));
            }
        }

        if (dto.BranchId is > 0)
        {
            var already = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                @"SELECT COUNT(1) FROM GeofenceAssignments
                  WHERE GeofenceId = @GeofenceId AND BranchId = @BranchId AND IsDeleted = 0",
                new { request.GeofenceId, dto.BranchId },
                cancellationToken: cancellationToken));
            if (already == 0)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    @"INSERT INTO GeofenceAssignments (GeofenceId, BranchId, CreatedAt, CreatedBy, IsDeleted)
                      VALUES (@GeofenceId, @BranchId, GETUTCDATE(), @CreatedBy, 0)",
                    new { request.GeofenceId, dto.BranchId, CreatedBy = createdBy },
                    cancellationToken: cancellationToken));
            }
        }

        if (dto.DepartmentId is > 0)
        {
            var already = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                @"SELECT COUNT(1) FROM GeofenceAssignments
                  WHERE GeofenceId = @GeofenceId AND DepartmentId = @DepartmentId AND IsDeleted = 0",
                new { request.GeofenceId, dto.DepartmentId },
                cancellationToken: cancellationToken));
            if (already == 0)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    @"INSERT INTO GeofenceAssignments (GeofenceId, DepartmentId, CreatedAt, CreatedBy, IsDeleted)
                      VALUES (@GeofenceId, @DepartmentId, GETUTCDATE(), @CreatedBy, 0)",
                    new { request.GeofenceId, dto.DepartmentId, CreatedBy = createdBy },
                    cancellationToken: cancellationToken));
            }
        }

        return ApiResponse<bool>.SuccessResponse(true, "Assignments saved.");
        }

    public async Task<ApiResponse<bool>> DeleteGeofenceAssignmentAsync(DeleteGeofenceAssignmentCommand request, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.ExecuteAsync(new CommandDefinition(
            @"UPDATE GeofenceAssignments SET IsDeleted = 1, UpdatedAt = GETUTCDATE()
              WHERE Id = @AssignmentId AND GeofenceId = @GeofenceId AND IsDeleted = 0",
            new { request.AssignmentId, request.GeofenceId },
            cancellationToken: cancellationToken));

        return rows > 0
            ? ApiResponse<bool>.SuccessResponse(true, "Assignment removed.")
            : ApiResponse<bool>.FailResponse("Assignment not found.");
        }

    public async Task<ApiResponse<int>> CreateGpsAlertRuleAsync(CreateGpsAlertRuleCommand request, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var dto = request.Rule;
        var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            @"INSERT INTO GpsAlertRules (VehicleId, SpeedLimitKmh, GeofenceId, AlertOnEnter, AlertOnExit, IsActive, CreatedAt, CreatedBy, IsDeleted)
              OUTPUT INSERTED.Id
              VALUES (@VehicleId, @SpeedLimitKmh, @GeofenceId, @AlertOnEnter, @AlertOnExit, 1, GETUTCDATE(), @CreatedBy, 0)",
            new
            {
                dto.VehicleId,
                dto.SpeedLimitKmh,
                dto.GeofenceId,
                dto.AlertOnEnter,
                dto.AlertOnExit,
                CreatedBy = currentUser.UserId?.ToString()
            },
            cancellationToken: cancellationToken));

        return ApiResponse<int>.SuccessResponse(id, "Alert rule created.");
        }

    public async Task<ApiResponse<bool>> AcknowledgeGpsAlertAsync(AcknowledgeGpsAlertCommand request, CancellationToken cancellationToken = default)
    {
        if (!GpsAlertAccess.CanAcknowledge(currentUser))
            return ApiResponse<bool>.FailResponse("Insufficient permission to acknowledge alerts.");

        using var connection = dbFactory.CreateConnection();
        var rows = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE e
            SET e.IsAcknowledged = 1,
                e.Status = CASE WHEN e.Status = 'active' THEN 'acknowledged' ELSE e.Status END,
                e.AcknowledgedAt = COALESCE(e.AcknowledgedAt, GETUTCDATE()),
                e.AcknowledgedBy = COALESCE(e.AcknowledgedBy, @AcknowledgedBy),
                e.ReadAt = COALESCE(e.ReadAt, GETUTCDATE()),
                e.ReadBy = COALESCE(e.ReadBy, @AcknowledgedBy)
            FROM GpsAlertEvents e
            INNER JOIN Vehicles v ON v.Id = e.VehicleId
            WHERE e.Id = @Id
              AND e.IsDeleted = 0
              AND v.TenantId = @TenantId
              AND v.IsDeleted = 0
              AND e.Status IN ('active', 'acknowledged')
            """,
            new
            {
                request.Id,
                TenantId = tenantContext.GetRequiredTenantId(),
                AcknowledgedBy = currentUser.UserId?.ToString()
            },
            cancellationToken: cancellationToken));

        return rows > 0
            ? ApiResponse<bool>.SuccessResponse(true, "Alert acknowledged.")
            : ApiResponse<bool>.FailResponse("Alert not found or already processed.");
        }

    public async Task<ApiResponse<bool>> MarkGpsAlertReadAsync(MarkGpsAlertReadCommand request, CancellationToken cancellationToken = default)
    {
        if (!GpsAlertAccess.CanView(currentUser))
            return ApiResponse<bool>.FailResponse("Insufficient permission to read alerts.");

        using var connection = dbFactory.CreateConnection();
        var rows = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE e
            SET e.ReadAt = COALESCE(e.ReadAt, GETUTCDATE()),
                e.ReadBy = COALESCE(e.ReadBy, @ReadBy)
            FROM GpsAlertEvents e
            INNER JOIN Vehicles v ON v.Id = e.VehicleId
            WHERE e.Id = @Id
              AND e.IsDeleted = 0
              AND v.TenantId = @TenantId
              AND v.IsDeleted = 0
              AND e.Status <> 'archived'
              AND e.ReadAt IS NULL
            """,
            new
            {
                request.Id,
                TenantId = tenantContext.GetRequiredTenantId(),
                ReadBy = currentUser.UserId?.ToString()
            },
            cancellationToken: cancellationToken));

        return rows > 0
            ? ApiResponse<bool>.SuccessResponse(true, "Alert marked as read.")
            : ApiResponse<bool>.FailResponse("Alert not found or already read.");
        }

    public async Task<ApiResponse<bool>> ResolveGpsAlertAsync(ResolveGpsAlertCommand request, CancellationToken cancellationToken = default)
    {
        if (!GpsAlertAccess.CanResolve(currentUser))
            return ApiResponse<bool>.FailResponse("Insufficient permission to resolve alerts.");

        using var connection = dbFactory.CreateConnection();
        var rows = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE e
            SET e.IsAcknowledged = 1,
                e.Status = 'resolved',
                e.ResolvedAt = COALESCE(e.ResolvedAt, GETUTCDATE()),
                e.ResolvedBy = COALESCE(e.ResolvedBy, @ResolvedBy),
                e.ResolutionNotes = @ResolutionNotes,
                e.AcknowledgedAt = COALESCE(e.AcknowledgedAt, GETUTCDATE()),
                e.AcknowledgedBy = COALESCE(e.AcknowledgedBy, @ResolvedBy),
                e.ReadAt = COALESCE(e.ReadAt, GETUTCDATE()),
                e.ReadBy = COALESCE(e.ReadBy, @ResolvedBy)
            FROM GpsAlertEvents e
            INNER JOIN Vehicles v ON v.Id = e.VehicleId
            WHERE e.Id = @Id
              AND e.IsDeleted = 0
              AND v.TenantId = @TenantId
              AND v.IsDeleted = 0
              AND e.Status <> 'resolved'
              AND e.Status <> 'archived'
            """,
            new
            {
                request.Id,
                TenantId = tenantContext.GetRequiredTenantId(),
                ResolvedBy = currentUser.UserId?.ToString(),
                request.Resolution.ResolutionNotes
            },
            cancellationToken: cancellationToken));

        return rows > 0
            ? ApiResponse<bool>.SuccessResponse(true, "Alert resolved.")
            : ApiResponse<bool>.FailResponse("Alert not found or already resolved.");
        }

    public async Task<ApiResponse<bool>> ArchiveGpsAlertAsync(ArchiveGpsAlertCommand request, CancellationToken cancellationToken = default)
    {
        if (!GpsAlertAccess.CanArchive(currentUser))
            return ApiResponse<bool>.FailResponse("Insufficient permission to archive alerts.");

        using var connection = dbFactory.CreateConnection();
        var rows = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE e
            SET e.Status = 'archived',
                e.ArchivedAt = COALESCE(e.ArchivedAt, GETUTCDATE()),
                e.ArchivedBy = COALESCE(e.ArchivedBy, @ArchivedBy),
                e.ReadAt = COALESCE(e.ReadAt, GETUTCDATE()),
                e.ReadBy = COALESCE(e.ReadBy, @ArchivedBy),
                e.ResolutionNotes = COALESCE(NULLIF(@ArchiveReason, ''), e.ResolutionNotes)
            FROM GpsAlertEvents e
            INNER JOIN Vehicles v ON v.Id = e.VehicleId
            WHERE e.Id = @Id
              AND e.IsDeleted = 0
              AND v.TenantId = @TenantId
              AND v.IsDeleted = 0
              AND e.Status IN ('acknowledged', 'resolved')
            """,
            new
            {
                request.Id,
                TenantId = tenantContext.GetRequiredTenantId(),
                ArchivedBy = currentUser.UserId?.ToString(),
                request.Archive.ArchiveReason
            },
            cancellationToken: cancellationToken));

        return rows > 0
            ? ApiResponse<bool>.SuccessResponse(true, "Alert archived.")
            : ApiResponse<bool>.FailResponse("Only acknowledged or resolved alerts can be archived.");
        }

    public async Task<ApiResponse<bool>> DeleteGpsAlertEventAsync(DeleteGpsAlertEventCommand request, CancellationToken cancellationToken = default)
    {
        if (!GpsAlertAccess.CanDelete(currentUser))
            return ApiResponse<bool>.FailResponse("Insufficient permission to delete alerts.");

        using var connection = dbFactory.CreateConnection();
        var rows = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE e
            SET e.IsDeleted = 1
            FROM GpsAlertEvents e
            INNER JOIN Vehicles v ON v.Id = e.VehicleId
            WHERE e.Id = @Id
              AND e.IsDeleted = 0
              AND v.TenantId = @TenantId
              AND v.IsDeleted = 0
            """,
            new { request.Id, TenantId = tenantContext.GetRequiredTenantId() },
            cancellationToken: cancellationToken));

        return rows > 0
            ? ApiResponse<bool>.SuccessResponse(true, "Alert deleted.")
            : ApiResponse<bool>.FailResponse("Alert not found.");
        }

    public async Task<ApiResponse<bool>> UpdateAlertSettingsAsync(UpdateAlertSettingsCommand request, CancellationToken cancellationToken = default)
    {
        var userId = currentUser.UserId;
        if (userId is null)
            return ApiResponse<bool>.FailResponse("No authenticated user.");

        using var connection = dbFactory.CreateConnection();
        foreach (var s in request.Settings.Settings)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                MERGE AlertSettings AS target
                USING (SELECT @UserId AS UserId, @AlertType AS AlertType) AS source
                ON target.UserId = source.UserId AND target.AlertType = source.AlertType
                WHEN MATCHED THEN
                    UPDATE SET InAppEnabled = @InAppEnabled, EmailEnabled = @EmailEnabled,
                               PushEnabled = @PushEnabled, SmsEnabled = @SmsEnabled, UpdatedAt = GETUTCDATE()
                WHEN NOT MATCHED THEN
                    INSERT (UserId, AlertType, InAppEnabled, EmailEnabled, PushEnabled, SmsEnabled, CreatedAt)
                    VALUES (@UserId, @AlertType, @InAppEnabled, @EmailEnabled, @PushEnabled, @SmsEnabled, GETUTCDATE());
                """,
                new
                {
                    UserId = userId.Value,
                    s.AlertType,
                    s.InAppEnabled,
                    s.EmailEnabled,
                    s.PushEnabled,
                    s.SmsEnabled
                },
                cancellationToken: cancellationToken));
        }

        return ApiResponse<bool>.SuccessResponse(true, "Alert settings saved.");
        }

    public async Task<ApiResponse<FleetUtilizationDto>> GetFleetUtilizationAsync(GetFleetUtilizationQuery request, CancellationToken cancellationToken = default)
    {
        var fromDate = request.FromDate ?? DateTime.UtcNow.AddDays(-7);
        var toDate = request.ToDate ?? DateTime.UtcNow;

        if (fromDate > toDate)
            return ApiResponse<FleetUtilizationDto>.FailResponse("'from' must be before 'to'.");

        if (toDate - fromDate > FleetUtilizationMaxRange)
            return ApiResponse<FleetUtilizationDto>.FailResponse("Date range cannot exceed 400 days.");

        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        var snapshots = (await connection.QueryAsync<(DateTime SnapshotAt, int TotalVehicles, int Moving, int Idle, int Parked, int Offline)>(
            new CommandDefinition(
                """
                SELECT SnapshotAt, TotalVehicles, Moving, Idle, Parked, Offline
                FROM GpsFleetStatusSnapshots
                WHERE TenantId = @TenantId AND SnapshotAt BETWEEN @FromDate AND @ToDate
                ORDER BY SnapshotAt ASC
                """,
                new { TenantId = tenantId, FromDate = fromDate, ToDate = toDate },
                cancellationToken: cancellationToken))).ToList();

        if (snapshots.Count == 0)
            return ApiResponse<FleetUtilizationDto>.SuccessResponse(new FleetUtilizationDto(0, 0, 0, 0, 0, "No data", []));

        var runningHours = snapshots.Sum(s => s.Moving);
        var idleHours = snapshots.Sum(s => s.Idle);
        var parkingHours = snapshots.Sum(s => s.Parked);
        var offlineHours = snapshots.Sum(s => s.Offline);
        var totalAvailableHours = snapshots.Sum(s => s.TotalVehicles);

        var utilizationPercent = totalAvailableHours > 0
            ? Math.Round(runningHours * 100m / totalAvailableHours, 1)
            : 0;

        var daily = snapshots
            .GroupBy(s => s.SnapshotAt.Date)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var totalForDay = g.Sum(s => s.TotalVehicles);
                var pct = totalForDay > 0 ? Math.Round(g.Sum(s => s.Moving) * 100m / totalForDay, 1) : 0;
                return new DailyUtilizationDto(g.Key, pct);
            })
            .ToList();

        var dto = new FleetUtilizationDto(
            runningHours, idleHours, parkingHours, offlineHours, utilizationPercent, LabelFor(utilizationPercent), daily);

        return ApiResponse<FleetUtilizationDto>.SuccessResponse(dto);
        }

    public async Task<ApiResponse<TrendsDto>> GetAnalyticsTrendsAsync(GetAnalyticsTrendsQuery request, CancellationToken cancellationToken = default)
    {
        var fromDate = (request.FromDate ?? DateTime.UtcNow.AddMonths(-3)).Date;
        var toDate = (request.ToDate ?? DateTime.UtcNow).Date;

        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        var rows = (await connection.QueryAsync<(DateTime StatDate, decimal DistanceKm, int TripCount, int? OverspeedCount)>(
            new CommandDefinition(
                """
                SELECT StatDate, DistanceKm, TripCount, OverspeedCount
                FROM GpsVehicleDailyStats
                WHERE TenantId = @TenantId AND StatDate BETWEEN @FromDate AND @ToDate
                """,
                new { TenantId = tenantId, FromDate = fromDate, ToDate = toDate },
                cancellationToken: cancellationToken))).ToList();

        DateTime Bucket(DateTime d) => request.Granularity.ToLowerInvariant() switch
        {
            "monthly" => new DateTime(d.Year, d.Month, 1),
            "weekly" => d.AddDays(-(int)d.DayOfWeek),
            _ => d.Date
        };

        var points = rows
            .GroupBy(r => Bucket(r.StatDate))
            .OrderBy(g => g.Key)
            .Select(g => new TrendPointDto(g.Key, g.Sum(r => r.DistanceKm), g.Sum(r => r.TripCount), g.Sum(r => r.OverspeedCount ?? 0)))
            .ToList();

        return ApiResponse<TrendsDto>.SuccessResponse(new TrendsDto(points));
        }

    public async Task<ApiResponse<GeofenceAnalyticsDto>> GetGeofenceAnalyticsAsync(GetGeofenceAnalyticsQuery request, CancellationToken cancellationToken = default)
    {
        var fromDate = request.FromDate ?? DateTime.UtcNow.AddDays(-7);
        var toDate = request.ToDate ?? DateTime.UtcNow;

        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        var events = (await connection.QueryAsync<(int GeofenceId, string GeofenceName, int VehicleId, string EventType, DateTime Timestamp)>(
            new CommandDefinition(
                """
                SELECT e.GeofenceId, g.Name AS GeofenceName, e.VehicleId, e.EventType, e.Timestamp
                FROM GpsAlertEvents e
                INNER JOIN Vehicles v ON v.Id = e.VehicleId AND v.TenantId = @TenantId
                INNER JOIN Geofences g ON g.Id = e.GeofenceId
                WHERE e.EventType IN ('geofence_enter', 'geofence_exit') AND e.IsDeleted = 0
                  AND e.GeofenceId IS NOT NULL AND e.Timestamp BETWEEN @FromDate AND @ToDate
                ORDER BY e.VehicleId, e.GeofenceId, e.Timestamp
                """,
                new { TenantId = tenantId, FromDate = fromDate, ToDate = toDate },
                cancellationToken: cancellationToken))).ToList();

        var dwellMinutesByGeofence = new Dictionary<int, List<decimal>>();
        foreach (var group in events.GroupBy(e => (e.VehicleId, e.GeofenceId)))
        {
            var ordered = group.OrderBy(e => e.Timestamp).ToList();
            for (var i = 0; i < ordered.Count - 1; i++)
            {
                if (ordered[i].EventType != "geofence_enter" || ordered[i + 1].EventType != "geofence_exit")
                    continue;

                var minutes = (decimal)(ordered[i + 1].Timestamp - ordered[i].Timestamp).TotalMinutes;
                if (minutes <= 0) continue;

                if (!dwellMinutesByGeofence.TryGetValue(group.Key.GeofenceId, out var list))
                {
                    list = [];
                    dwellMinutesByGeofence[group.Key.GeofenceId] = list;
                }
                list.Add(minutes);
            }
        }

        var byGeofence = events
            .GroupBy(e => new { e.GeofenceId, e.GeofenceName })
            .Select(g => new GeofenceVisitDto(
                g.Key.GeofenceId,
                g.Key.GeofenceName,
                g.Count(e => e.EventType == "geofence_enter"),
                g.Count(e => e.EventType == "geofence_exit"),
                dwellMinutesByGeofence.TryGetValue(g.Key.GeofenceId, out var dwell) && dwell.Count > 0
                    ? Math.Round(dwell.Average(), 1)
                    : null))
            .OrderByDescending(g => g.EntryCount + g.ExitCount)
            .ToList();

        var dto = new GeofenceAnalyticsDto(
            byGeofence.Take(10).ToList(),
            byGeofence.AsEnumerable().Reverse().Take(10).ToList(),
            events.Count(e => e.EventType == "geofence_enter"),
            events.Count(e => e.EventType == "geofence_exit"));

        return ApiResponse<GeofenceAnalyticsDto>.SuccessResponse(dto);
        }

    public async Task<ApiResponse<AlertEventStatsDto>> GetAlertEventStatsAsync(GetAlertEventStatsQuery request, CancellationToken cancellationToken = default)
    {
        var fromDate = request.FromDate ?? DateTime.UtcNow.AddDays(-7);
        var toDate = request.ToDate ?? DateTime.UtcNow;

        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        var sql = """
            SELECT e.EventType, e.Severity, e.Timestamp
            FROM GpsAlertEvents e
            INNER JOIN Vehicles v ON v.Id = e.VehicleId AND v.TenantId = @TenantId
            WHERE e.IsDeleted = 0 AND e.Timestamp BETWEEN @FromDate AND @ToDate
            """;

        var parameters = new DynamicParameters();
        parameters.Add("TenantId", tenantId);
        parameters.Add("FromDate", fromDate);
        parameters.Add("ToDate", toDate);

        if (request.VehicleId.HasValue)
        {
            sql += " AND e.VehicleId = @VehicleId";
            parameters.Add("VehicleId", request.VehicleId.Value);
        }

        var events = (await connection.QueryAsync<(string EventType, string Severity, DateTime Timestamp)>(
            new CommandDefinition(sql, parameters, cancellationToken: cancellationToken))).ToList();

        var byType = events
            .GroupBy(e => GpsEventTypeNormalizer.Normalize(e.EventType))
            .Select(g => new EventTypeCountDto(g.Key, g.Count()))
            .OrderByDescending(x => x.Count)
            .ToList();

        var bySeverity = events
            .GroupBy(e => e.Severity)
            .Select(g => new EventTypeCountDto(g.Key, g.Count()))
            .OrderByDescending(x => x.Count)
            .ToList();

        var daily = events
            .GroupBy(e => e.Timestamp.Date)
            .OrderBy(g => g.Key)
            .Select(g => new DailyEventCountDto(g.Key, g.Count()))
            .ToList();

        var dto = new AlertEventStatsDto(byType, bySeverity, daily, events.Count);
        return ApiResponse<AlertEventStatsDto>.SuccessResponse(dto);
        }

    public async Task<ApiResponse<List<HeatmapPointDto>>> GetPositionHeatmapAsync(GetPositionHeatmapQuery request, CancellationToken cancellationToken = default)
    {
        if (request.VehicleIds.Length == 0)
            return ApiResponse<List<HeatmapPointDto>>.FailResponse("Select at least one vehicle.");

        if (request.VehicleIds.Length > MaxVehicles)
            return ApiResponse<List<HeatmapPointDto>>.FailResponse($"Select at most {MaxVehicles} vehicles for the heatmap.");

        var fromDate = request.FromDate ?? DateTime.UtcNow.AddDays(-7);
        var toDate = request.ToDate ?? DateTime.UtcNow;

        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        var ownedVehicleIds = (await connection.QueryAsync<int>(new CommandDefinition(
            "SELECT Id FROM Vehicles WHERE TenantId = @TenantId AND IsDeleted = 0 AND Id IN @VehicleIds",
            new { TenantId = tenantId, VehicleIds = request.VehicleIds },
            cancellationToken: cancellationToken))).ToList();

        if (ownedVehicleIds.Count == 0)
            return ApiResponse<List<HeatmapPointDto>>.SuccessResponse([]);

        var positions = await connection.QueryAsync<(double Latitude, double Longitude)>(new CommandDefinition(
            """
            SELECT Latitude, Longitude FROM GpsPositions
            WHERE VehicleId IN @VehicleIds AND RecordedAt BETWEEN @FromDate AND @ToDate
            """,
            new { VehicleIds = ownedVehicleIds, FromDate = fromDate, ToDate = toDate },
            cancellationToken: cancellationToken));

        var binned = positions
            .GroupBy(p => (Lat: Math.Round(p.Latitude / GridSize) * GridSize, Lng: Math.Round(p.Longitude / GridSize) * GridSize))
            .Select(g => new HeatmapPointDto(g.Key.Lat, g.Key.Lng, g.Count()))
            .OrderByDescending(p => p.Count)
            .Take(5000)
            .ToList();

        return ApiResponse<List<HeatmapPointDto>>.SuccessResponse(binned);
        }

    public async Task<ApiResponse<List<GpsVehicleHealthDto>>> GetVehicleHealthScoreAsync(GetVehicleHealthScoreQuery request, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        var sql = """
            SELECT v.Id AS VehicleId, v.Name AS VehicleName, v.InsuranceExpiryDate, v.CurrentMileage,
                   d.LastBatteryLevel, d.LastRssi, d.WarrantyEnd AS TrackerWarrantyEnd
            FROM Vehicles v
            LEFT JOIN GpsDevices d ON d.Id = v.GpsDeviceId AND d.IsDeleted = 0
            WHERE v.TenantId = @TenantId AND v.IsDeleted = 0
            """;

        var parameters = new DynamicParameters();
        parameters.Add("TenantId", tenantId);

        if (request.BranchId.HasValue)
        {
            sql += " AND v.BranchId = @BranchId";
            parameters.Add("BranchId", request.BranchId.Value);
        }

        if (request.DepartmentId.HasValue)
        {
            sql += " AND v.DepartmentId = @DepartmentId";
            parameters.Add("DepartmentId", request.DepartmentId.Value);
        }

        var vehicles = (await connection.QueryAsync<(int VehicleId, string VehicleName, DateTime? InsuranceExpiryDate, decimal CurrentMileage, decimal? LastBatteryLevel, int? LastRssi, DateTime? TrackerWarrantyEnd)>(
            new CommandDefinition(sql, parameters, cancellationToken: cancellationToken))).ToList();

        if (vehicles.Count == 0)
            return ApiResponse<List<GpsVehicleHealthDto>>.SuccessResponse([]);

        var vehicleIds = vehicles.Select(v => v.VehicleId).ToList();
        var schedules = (await connection.QueryAsync<(int VehicleId, string IntervalType, DateTime? NextDueDate, decimal? NextDueMileage)>(
            new CommandDefinition(
                """
                SELECT VehicleId, IntervalType, NextDueDate, NextDueMileage
                FROM VehicleMaintenanceSchedules
                WHERE TenantId = @TenantId AND IsDeleted = 0 AND IsActive = 1 AND VehicleId IN @VehicleIds
                """,
                new { TenantId = tenantId, VehicleIds = vehicleIds },
                cancellationToken: cancellationToken))).ToList();

        var result = vehicles.Select(v =>
        {
            var vehicleSchedules = schedules.Where(s => s.VehicleId == v.VehicleId).ToList();
            var maintenanceStatus = vehicleSchedules.Count == 0
                ? "None"
                : vehicleSchedules
                    .Select(s => MaintenanceScheduleHelper.ComputeStatus(s.IntervalType, s.NextDueDate, s.NextDueMileage, null, v.CurrentMileage, null))
                    .OrderByDescending(SeverityRank)
                    .First();

            return new GpsVehicleHealthDto(
                v.VehicleId,
                v.VehicleName,
                v.LastBatteryLevel,
                v.LastRssi,
                maintenanceStatus,
                v.InsuranceExpiryDate,
                ExpiryStatusFor(v.InsuranceExpiryDate),
                v.TrackerWarrantyEnd,
                ExpiryStatusFor(v.TrackerWarrantyEnd));
        }).ToList();

        return ApiResponse<List<GpsVehicleHealthDto>>.SuccessResponse(result);
        }

    public async Task<ApiResponse<List<GeofenceDto>>> GetGeofencesAsync(GetGeofencesQuery request, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var sql = """
            SELECT g.Id, g.Name, g.AreaType, g.CenterLat, g.CenterLng, g.RadiusMeters, g.GeoJson, g.IsActive,
                   g.Color, g.Category, g.Description,
                   (SELECT COUNT(1) FROM GeofenceAssignments a
                    WHERE a.GeofenceId = g.Id AND a.IsDeleted = 0 AND a.VehicleId IS NOT NULL) AS AssignedVehicleCount
            FROM Geofences g
            WHERE g.IsDeleted = 0
            """;

        if (!string.IsNullOrWhiteSpace(request.Search))
            sql += " AND (g.Name LIKE @Search OR ISNULL(g.Category,'') LIKE @Search OR ISNULL(g.Description,'') LIKE @Search)";
        if (!string.IsNullOrWhiteSpace(request.AreaType))
            sql += " AND LOWER(g.AreaType) = LOWER(@AreaType)";
        if (request.IsActive.HasValue)
            sql += " AND g.IsActive = @IsActive";
        if (request.VehicleId.HasValue)
        {
            sql += """
                 AND EXISTS (
                   SELECT 1 FROM GeofenceAssignments a
                   WHERE a.GeofenceId = g.Id AND a.IsDeleted = 0 AND a.VehicleId = @VehicleId
                 )
                """;
        }

        sql += " ORDER BY g.Name";

        var rows = await connection.QueryAsync<GeofenceDto>(new CommandDefinition(
            sql,
            new
            {
                Search = $"%{request.Search?.Trim()}%",
                request.AreaType,
                request.IsActive,
                request.VehicleId
            },
            cancellationToken: cancellationToken));

        return ApiResponse<List<GeofenceDto>>.SuccessResponse(rows.ToList());
        }

    public async Task<ApiResponse<List<GeofenceAssignmentDto>>> GetGeofenceAssignmentsAsync(GetGeofenceAssignmentsQuery request, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.QueryAsync<GeofenceAssignmentDto>(new CommandDefinition(
            """
            SELECT a.Id, a.GeofenceId, a.VehicleId, v.Name AS VehicleName, v.RegistrationNumber,
                   a.BranchId, b.Name AS BranchName, a.DepartmentId, d.Name AS DepartmentName
            FROM GeofenceAssignments a
            LEFT JOIN Vehicles v ON v.Id = a.VehicleId
            LEFT JOIN Branches b ON b.Id = a.BranchId
            LEFT JOIN Departments d ON d.Id = a.DepartmentId
            WHERE a.GeofenceId = @GeofenceId AND a.IsDeleted = 0
            ORDER BY v.Name, b.Name, d.Name
            """,
            new { request.GeofenceId },
            cancellationToken: cancellationToken));

        return ApiResponse<List<GeofenceAssignmentDto>>.SuccessResponse(rows.ToList());
        }

    public async Task<ApiResponse<GeofenceStatsDto>> GetGeofenceStatsAsync(GetGeofenceStatsQuery request, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        // Geofences itself has no TenantId column (geofences were never made tenant-scoped at the
        // schema level) — Total/Active are intentionally left as-is here, a separate pre-existing
        // gap this fix doesn't attempt to close. The counts below are tenant-scoped via the
        // GpsAlertEvents -> Vehicles join, since alert events and vehicles are tenant-owned.
        var total = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM Geofences WHERE IsDeleted = 0",
            cancellationToken: cancellationToken));
        var active = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM Geofences WHERE IsDeleted = 0 AND IsActive = 1",
            cancellationToken: cancellationToken));
        var todayEntries = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            @"SELECT COUNT(1) FROM GpsAlertEvents e
              INNER JOIN Vehicles v ON v.Id = e.VehicleId AND v.TenantId = @TenantId
              WHERE e.EventType = 'geofence_enter' AND e.IsDeleted = 0
              AND e.Timestamp >= CAST(GETUTCDATE() AS DATE)",
            new { TenantId = tenantId },
            cancellationToken: cancellationToken));
        var todayExits = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            @"SELECT COUNT(1) FROM GpsAlertEvents e
              INNER JOIN Vehicles v ON v.Id = e.VehicleId AND v.TenantId = @TenantId
              WHERE e.EventType = 'geofence_exit' AND e.IsDeleted = 0
              AND e.Timestamp >= CAST(GETUTCDATE() AS DATE)",
            new { TenantId = tenantId },
            cancellationToken: cancellationToken));
        var unacked = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            @"SELECT COUNT(*) FROM GpsAlertEvents e
              INNER JOIN Vehicles v ON v.Id = e.VehicleId AND v.TenantId = @TenantId
              WHERE e.EventType IN ('geofence_enter', 'geofence_exit') AND e.IsAcknowledged = 0 AND e.IsDeleted = 0
              AND e.Timestamp > DATEADD(HOUR, -24, GETUTCDATE())",
            new { TenantId = tenantId },
            cancellationToken: cancellationToken));

        var vehiclesInside = await CountVehiclesInsideAsync(connection, tenantId, cancellationToken);

        return ApiResponse<GeofenceStatsDto>.SuccessResponse(new GeofenceStatsDto(
            total, active, vehiclesInside, todayEntries, todayExits, unacked));
        }

    public async Task<ApiResponse<List<GpsAlertEventDto>>> GetGeofenceEventsAsync(GetGeofenceEventsQuery request, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var from = request.From ?? DateTime.UtcNow.Date.AddDays(-7);
        var to = request.To ?? DateTime.UtcNow.AddDays(1);

        var rows = await connection.QueryAsync<GpsAlertEventDto>(new CommandDefinition(
            """
            SELECT TOP 200 e.Id, e.RuleId, e.VehicleId, v.Name AS VehicleName, e.EventType,
                   e.Latitude, e.Longitude, e.Speed, e.Message, e.Timestamp, e.IsAcknowledged,
                   COALESCE(e.Severity, 'medium') AS Severity,
                   COALESCE(e.Status, 'active') AS Status,
                   e.GeofenceId, g.Name AS GeofenceName
            FROM GpsAlertEvents e
            LEFT JOIN Vehicles v ON v.Id = e.VehicleId
            LEFT JOIN Geofences g ON g.Id = e.GeofenceId
            WHERE e.IsDeleted = 0 AND e.GeofenceId = @GeofenceId
              AND e.EventType IN ('geofence_enter', 'geofence_exit')
              AND e.Timestamp >= @From AND e.Timestamp < @To
            ORDER BY e.Timestamp DESC
            """,
            new { request.GeofenceId, From = from, To = to },
            cancellationToken: cancellationToken));

        return ApiResponse<List<GpsAlertEventDto>>.SuccessResponse(rows.ToList());
        }

    public async Task<ApiResponse<List<GpsLiveFleetVehicleDto>>> GetGpsLiveFleetAsync(GetGpsLiveFleetQuery request, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        var clauses = new List<string>
        {
            "v.TenantId = @TenantId",
            "v.IsDeleted = 0",
            "v.Status <> 5"
        };
        var parameters = new DynamicParameters(new { TenantId = tenantId, MaxRows = MaxFleetSize });

        if (currentUser.UserId is int userId)
        {
            var scope = await dataScopeEngine.ResolveAsync(userId, tenantId, cancellationToken);
            DataScopeSqlBuilder.ApplyVehicleScope(parameters, scope, "v", clauses);
        }

        var whereClause = string.Join(" AND ", clauses);

        var rows = (await connection.QueryAsync<GpsLiveFleetVehicleDto>(new CommandDefinition(
            $"""
            SELECT TOP (@MaxRows)
              v.Id AS VehicleId,
              ISNULL(NULLIF(LTRIM(RTRIM(v.Name)), ''), CONCAT(N'Vehicle #', v.Id)) AS VehicleName,
              ISNULL(v.RegistrationNumber, N'') AS RegistrationNumber,
              v.VehicleType,
              CAST(v.Status AS INT) AS VehicleStatus,
              COALESCE(assignDrv.DriverId, vcl.DriverId) AS DriverId,
              dr.FullName AS DriverName,
              dr.Phone AS DriverPhone,
              CASE WHEN v.GpsDeviceId IS NOT NULL THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END AS HasGpsDevice,
              vcl.Latitude,
              vcl.Longitude,
              vcl.LastUpdate AS LastUpdated,
              ISNULL(vcl.Speed, 0) AS Speed,
              COALESCE(vcl.Ignition, gd.LastIgnition) AS Ignition,
              vcl.Heading,
              vcl.FuelLevel,
              vcl.BatteryLevel,
              vcl.GsmSignal,
              vcl.TotalDistanceKm,
              vcl.Address,
              vcl.AlarmType,
              vcl.Temperature,
              vcl.BookingId,
              gd.UniqueId AS Imei,
              gd.Name AS TrackerName,
              gd.RelayOutput
            FROM Vehicles v
            LEFT JOIN GpsDevices gd ON gd.Id = v.GpsDeviceId AND gd.IsDeleted = 0
            LEFT JOIN VehicleCurrentLocation vcl ON vcl.VehicleId = v.Id
            OUTER APPLY (
                SELECT TOP 1 a.DriverId
                FROM AssignmentHistory a
                WHERE a.VehicleId = v.Id AND a.IsDeleted = 0
                  AND a.Status IN (N'Active', N'Scheduled') AND a.DriverId IS NOT NULL
                ORDER BY CASE WHEN a.Status = N'Active' THEN 0 ELSE 1 END, a.StartAt DESC
            ) assignDrv
            LEFT JOIN Drivers dr ON dr.Id = COALESCE(assignDrv.DriverId, vcl.DriverId) AND dr.IsDeleted = 0
            WHERE {whereClause}
            ORDER BY v.Name, v.Id
            """,
            parameters,
            cancellationToken: cancellationToken))).ToList();

        for (var i = 0; i < rows.Count; i++)
        {
            if (rows[i].LastUpdated is DateTime ts)
                rows[i] = rows[i] with { LastUpdated = GpsUtcDateTime.AsUtc(ts) };
        }

        return ApiResponse<List<GpsLiveFleetVehicleDto>>.SuccessResponse(rows);
        }

    public async Task<ApiResponse<List<GpsAlertRuleDto>>> GetGpsAlertRulesAsync(GetGpsAlertRulesQuery request, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.QueryAsync<GpsAlertRuleDto>(new CommandDefinition(
            @"SELECT r.Id, r.VehicleId, v.Name AS VehicleName, r.SpeedLimitKmh, r.GeofenceId,
                     g.Name AS GeofenceName, r.AlertOnEnter, r.AlertOnExit, r.IsActive
              FROM GpsAlertRules r
              LEFT JOIN Vehicles v ON v.Id = r.VehicleId
              LEFT JOIN Geofences g ON g.Id = r.GeofenceId
              WHERE r.IsDeleted = 0 ORDER BY r.Id DESC",
            cancellationToken: cancellationToken));

        return ApiResponse<List<GpsAlertRuleDto>>.SuccessResponse(rows.ToList());
        }

    public async Task<ApiResponse<List<GpsAlertEventDto>>> GetGpsAlertEventsAsync(GetGpsAlertEventsQuery request, CancellationToken cancellationToken = default)
    {
        if (!GpsAlertAccess.CanView(currentUser))
            return ApiResponse<List<GpsAlertEventDto>>.FailResponse("Insufficient permission to view alerts.");

        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();
        var allowedEventTypes = GpsAlertAccess.AllowedEventTypesForRoles(currentUser.Roles);
        var (from, to) = ResolveDateRange(request.DatePreset, request.From, request.To);
        var sql = $"""
            SELECT {AlertSelectColumns}
            FROM GpsAlertEvents e
            INNER JOIN Vehicles v ON v.Id = e.VehicleId AND v.TenantId = @TenantId
            LEFT JOIN Geofences g ON g.Id = e.GeofenceId
            LEFT JOIN Drivers d ON d.Id = e.DriverId
            WHERE e.IsDeleted = 0
            """;

        if (request.VehicleId.HasValue)
        {
            sql += " AND e.VehicleId = @VehicleId";
        }

        if (request.UnacknowledgedOnly == true)
        {
            sql += " AND e.IsAcknowledged = 0";
        }

        if (from.HasValue)
        {
            sql += " AND e.Timestamp >= @From";
        }

        if (to.HasValue)
        {
            sql += " AND e.Timestamp <= @To";
        }

        if (request.DriverId.HasValue)
        {
            sql += " AND e.DriverId = @DriverId";
        }

        if (!string.IsNullOrWhiteSpace(request.EventType))
        {
            sql += " AND e.EventType = @EventType";
        }

        if (request.GeofenceId.HasValue)
        {
            sql += " AND e.GeofenceId = @GeofenceId";
        }

        if (!string.IsNullOrWhiteSpace(request.Severity))
        {
            sql += " AND e.Severity = @Severity";
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var normalizedStatus = request.Status.Trim().ToLowerInvariant();
            sql += normalizedStatus switch
            {
                "unread" => " AND e.Status = 'active' AND e.ReadAt IS NULL",
                "read" => " AND e.Status = 'active' AND e.ReadAt IS NOT NULL",
                _ => " AND e.Status = @Status"
            };
        }

        if (!string.IsNullOrWhiteSpace(request.ReadState))
        {
            sql += request.ReadState.Trim().ToLowerInvariant() switch
            {
                "unread" => " AND e.ReadAt IS NULL AND e.Status <> 'archived'",
                "read" => " AND e.ReadAt IS NOT NULL",
                _ => string.Empty
            };
        }

        if (allowedEventTypes is not null)
        {
            sql += " AND LOWER(e.EventType) IN @AllowedEventTypes";
        }

        sql += " ORDER BY e.Timestamp DESC";

        var rows = await connection.QueryAsync<GpsAlertEventDto>(new CommandDefinition(
            sql,
            new
            {
                TenantId = tenantId,
                request.VehicleId,
                From = from,
                To = to,
                request.DriverId,
                request.EventType,
                request.Severity,
                Status = request.Status?.Trim().ToLowerInvariant(),
                request.GeofenceId,
                AllowedEventTypes = allowedEventTypes
            },
            cancellationToken: cancellationToken));

        return ApiResponse<List<GpsAlertEventDto>>.SuccessResponse(GpsAlertQueryProjection.Decorate(rows, currentUser).ToList());
        }

    public async Task<ApiResponse<GpsAlertEventDto>> GetGpsAlertEventByIdAsync(GetGpsAlertEventByIdQuery request, CancellationToken cancellationToken = default)
    {
        if (!GpsAlertAccess.CanView(currentUser))
            return ApiResponse<GpsAlertEventDto>.FailResponse("Insufficient permission to view alerts.");

        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();
        var allowedEventTypes = GpsAlertAccess.AllowedEventTypesForRoles(currentUser.Roles);

        var sql = """
            SELECT e.Id, e.RuleId, e.VehicleId, v.Name AS VehicleName, e.EventType,
                   ISNULL(e.Latitude, 0) AS Latitude, ISNULL(e.Longitude, 0) AS Longitude,
                   ISNULL(e.Speed, 0) AS Speed, ISNULL(e.Message, '') AS Message,
                   e.Timestamp, e.IsAcknowledged,
                   COALESCE(e.Severity, 'medium') AS Severity,
                   COALESCE(e.Status, 'active') AS Status,
                   e.GeofenceId, g.Name AS GeofenceName,
                   e.DriverId, d.FullName AS DriverName,
                   e.ReadAt, e.ReadBy, e.AcknowledgedAt, e.AcknowledgedBy,
                   e.ResolvedAt, e.ResolvedBy, e.ResolutionNotes, e.ArchivedAt, e.ArchivedBy
            FROM GpsAlertEvents e
            INNER JOIN Vehicles v ON v.Id = e.VehicleId AND v.TenantId = @TenantId
            LEFT JOIN Geofences g ON g.Id = e.GeofenceId
            LEFT JOIN Drivers d ON d.Id = e.DriverId
            WHERE e.Id = @Id AND e.IsDeleted = 0
            """;
        if (allowedEventTypes is not null)
        {
            sql += " AND LOWER(e.EventType) IN @AllowedEventTypes";
        }

        var row = await connection.QueryFirstOrDefaultAsync<GpsAlertEventDto>(new CommandDefinition(
            sql,
            new { request.Id, TenantId = tenantId, AllowedEventTypes = allowedEventTypes },
            cancellationToken: cancellationToken));

        return row is not null
            ? ApiResponse<GpsAlertEventDto>.SuccessResponse(GpsAlertQueryProjection.Decorate(row, currentUser))
            : ApiResponse<GpsAlertEventDto>.FailResponse("Alert event not found.");
        }

    public async Task<ApiResponse<GpsAlertStatsDto>> GetGpsAlertStatsAsync(GetGpsAlertStatsQuery request, CancellationToken cancellationToken = default)
    {
        if (!GpsAlertAccess.CanView(currentUser))
            return ApiResponse<GpsAlertStatsDto>.FailResponse("Insufficient permission to view alerts.");

        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();
        var allowedEventTypes = GpsAlertAccess.AllowedEventTypes(currentUser.Role);

        var sql = """
            SELECT
                COUNT(*) AS Total,
                SUM(CASE WHEN CAST(e.Timestamp AS DATE) = CAST(GETUTCDATE() AS DATE) THEN 1 ELSE 0 END) AS Today,
                SUM(CASE WHEN e.ReadAt IS NULL AND e.Status <> 'archived' THEN 1 ELSE 0 END) AS Unread,
                SUM(CASE WHEN e.Status IN ('active', 'acknowledged') THEN 1 ELSE 0 END) AS Active,
                SUM(CASE WHEN e.Status = 'resolved' THEN 1 ELSE 0 END) AS Resolved,
                SUM(CASE WHEN e.Severity = 'critical' AND e.Status IN ('active', 'acknowledged') THEN 1 ELSE 0 END) AS Critical,
                SUM(CASE WHEN e.Status = 'archived' THEN 1 ELSE 0 END) AS Archived
            FROM GpsAlertEvents e
            INNER JOIN Vehicles v ON v.Id = e.VehicleId AND v.TenantId = @TenantId AND v.IsDeleted = 0
            WHERE e.IsDeleted = 0
            """;
        if (allowedEventTypes is not null)
        {
            sql += " AND LOWER(e.EventType) IN @AllowedEventTypes";
        }

        var stats = await connection.QueryFirstAsync<GpsAlertStatsDto>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, AllowedEventTypes = allowedEventTypes },
            cancellationToken: cancellationToken));

        return ApiResponse<GpsAlertStatsDto>.SuccessResponse(stats);
        }

    public async Task<ApiResponse<List<AlertSettingDto>>> GetAlertSettingsAsync(GetAlertSettingsQuery request, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var saved = (await connection.QueryAsync<AlertSettingDto>(new CommandDefinition(
            """
            SELECT AlertType, InAppEnabled, EmailEnabled, PushEnabled, SmsEnabled
            FROM AlertSettings
            WHERE UserId = @UserId
            """,
            new { UserId = currentUser.UserId },
            cancellationToken: cancellationToken)))
            .ToDictionary(s => s.AlertType, StringComparer.OrdinalIgnoreCase);

        var settings = AlertTypeCatalog.Types
            .Select(t => saved.TryGetValue(t, out var s) ? s : new AlertSettingDto(t, true, false, false, false))
            .ToList();

        return ApiResponse<List<AlertSettingDto>>.SuccessResponse(settings);
        }

    public async Task<ApiResponse<GpsEtaDto>> GetGpsEtaAsync(GetGpsEtaQuery request, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var booking = await connection.QueryFirstOrDefaultAsync<(int Id, int? VehicleId, string? RouteSource, double? PickupLat, double? PickupLng)>(
            new CommandDefinition(
                @"SELECT b.Id, b.VehicleId, r.Source AS RouteSource, b.PickupLat, b.PickupLng
                  FROM Bookings b
                  LEFT JOIN Routes r ON r.Id = b.RouteId
                  WHERE b.Id = @Id AND b.IsDeleted = 0",
                new { Id = request.BookingId },
                cancellationToken: cancellationToken));

        if (booking.Id == 0 || !booking.VehicleId.HasValue)
        {
            return ApiResponse<GpsEtaDto>.FailResponse("Booking not found or no vehicle assigned.");
        }

        var position = await connection.QueryFirstOrDefaultAsync<(double Lat, double Lng)>(
            new CommandDefinition(
                @"SELECT Latitude, Longitude FROM VehicleCurrentLocation WHERE VehicleId = @VehicleId",
                new { VehicleId = booking.VehicleId.Value },
                cancellationToken: cancellationToken));

        if (position.Lat == 0 && position.Lng == 0)
        {
            return ApiResponse<GpsEtaDto>.FailResponse("No GPS position available for assigned vehicle.");
        }

        var (pickupLat, pickupLng) = booking.PickupLat.HasValue && booking.PickupLng.HasValue
            ? (booking.PickupLat.Value, booking.PickupLng.Value)
            : ResolvePickupCoordinates(booking.RouteSource);

        var distanceKm = GpsGeoHelper.HaversineKm(position.Lat, position.Lng, pickupLat, pickupLng);
        var etaMinutes = distanceKm > 0 ? (int)Math.Ceiling(distanceKm / 40.0 * 60) : 0;

        var vehicleName = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT Name FROM Vehicles WHERE Id = @Id",
            new { Id = booking.VehicleId.Value },
            cancellationToken: cancellationToken));

        return ApiResponse<GpsEtaDto>.SuccessResponse(new GpsEtaDto(
            request.BookingId,
            booking.VehicleId.Value,
            vehicleName,
            Math.Round(distanceKm, 2),
            etaMinutes,
            position.Lat,
            position.Lng,
            pickupLat,
            pickupLng));
        }

    public async Task<ApiResponse<int>> GetGeofenceBreachCountAsync(GetGeofenceBreachCountQuery request, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var count = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            @"SELECT COUNT(*) FROM GpsAlertEvents
              WHERE EventType IN ('geofence_enter', 'geofence_exit') AND IsAcknowledged = 0 AND IsDeleted = 0
              AND Timestamp > DATEADD(HOUR, -24, GETUTCDATE())",
            cancellationToken: cancellationToken));

        return ApiResponse<int>.SuccessResponse(count);
        }

    public async Task<ApiResponse<TripDeviceContextDto>> GetTripContextAsync(GetTripContextQuery request, CancellationToken cancellationToken = default)
    {
        var context = await tripVehicleQuery.BuildContextAsync(
            request.VehicleId, cancellationToken);
        if (context is null)
        {
            return ApiResponse<TripDeviceContextDto>.FailResponse("Vehicle not found.");
        }

        return ApiResponse<TripDeviceContextDto>.SuccessResponse(context);
        }

    public async Task<ApiResponse<GpsFleetStatusLocalDto>> GetGpsFleetStatusLocalAsync(GetGpsFleetStatusLocalQuery request, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();
        DataScopeResult? scope = null;
        if (currentUser.UserId is int userId)
            scope = await dataScopeEngine.ResolveAsync(userId, tenantId, cancellationToken);

        var result = await fleetStatusCalculator.ComputeAsync(
            connection,
            tenantId,
            gpsSettings,
            traccarOptions,
            cancellationToken,
            scope);
        return ApiResponse<GpsFleetStatusLocalDto>.SuccessResponse(result);
        }

    public async Task<ApiResponse<GpsOperatorDashboardDto>> GetGpsOperatorDashboardAsync(GetGpsOperatorDashboardQuery request, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();
        var offlineStaleMinutes = gpsSettings.Value.OfflineStaleMinutes <= 0
            ? 10
            : gpsSettings.Value.OfflineStaleMinutes;
        var movingThresholdKmh = traccarOptions.Value.MovingSpeedKmh > 0
            ? traccarOptions.Value.MovingSpeedKmh
            : gpsSettings.Value.FleetMovingSpeedKmh;
        var fleet = await fleetStatusCalculator.ComputeAsync(
            connection,
            tenantId,
            gpsSettings,
            traccarOptions,
            cancellationToken);
        var todayStart = DateTime.UtcNow.Date;

        var health = await connection.QuerySingleAsync<(int Healthy, int Offline, int WeakGsm, int LowBattery, int NoGps, int IgnitionOn)>(
            new CommandDefinition(
                """
                SELECT
                  ISNULL(SUM(CASE
                    WHEN v.GpsDeviceId IS NOT NULL
                     AND vcl.VehicleId IS NOT NULL
                     AND DATEDIFF(MINUTE, vcl.LastUpdate, GETUTCDATE()) <= @OfflineStaleMinutes
                     AND ISNULL(vcl.BatteryLevel, 100) >= @LowBatteryThreshold
                     AND ISNULL(vcl.GsmSignal, 99) >= @WeakGsmThreshold
                     AND vcl.Latitude IS NOT NULL AND vcl.Longitude IS NOT NULL
                    THEN 1 ELSE 0 END), 0) AS Healthy,
                  ISNULL(SUM(CASE
                    WHEN v.GpsDeviceId IS NOT NULL
                     AND (vcl.VehicleId IS NULL
                          OR DATEDIFF(MINUTE, vcl.LastUpdate, GETUTCDATE()) > @OfflineStaleMinutes)
                    THEN 1 ELSE 0 END), 0) AS Offline,
                  ISNULL(SUM(CASE
                    WHEN vcl.VehicleId IS NOT NULL
                     AND DATEDIFF(MINUTE, vcl.LastUpdate, GETUTCDATE()) <= @OfflineStaleMinutes
                     AND ISNULL(vcl.GsmSignal, 0) < @WeakGsmThreshold
                    THEN 1 ELSE 0 END), 0) AS WeakGsm,
                  ISNULL(SUM(CASE
                    WHEN vcl.VehicleId IS NOT NULL
                     AND vcl.BatteryLevel IS NOT NULL
                     AND vcl.BatteryLevel < @LowBatteryThreshold
                    THEN 1 ELSE 0 END), 0) AS LowBattery,
                  ISNULL(SUM(CASE
                    WHEN v.GpsDeviceId IS NOT NULL
                     AND (vcl.VehicleId IS NULL OR vcl.Latitude IS NULL OR vcl.Longitude IS NULL)
                    THEN 1 ELSE 0 END), 0) AS NoGps,
                  ISNULL(SUM(CASE
                    WHEN vcl.Ignition = 1
                     AND DATEDIFF(MINUTE, vcl.LastUpdate, GETUTCDATE()) <= @OfflineStaleMinutes
                    THEN 1 ELSE 0 END), 0) AS IgnitionOn
                FROM Vehicles v
                LEFT JOIN VehicleCurrentLocation vcl ON vcl.VehicleId = v.Id
                WHERE v.TenantId = @TenantId AND v.IsDeleted = 0
                """,
                new
                {
                    TenantId = tenantId,
                    OfflineStaleMinutes = offlineStaleMinutes,
                    MovingThresholdKmh = movingThresholdKmh,
                    WeakGsmThreshold,
                    LowBatteryThreshold,
                },
                cancellationToken: cancellationToken));

        var alertCounts = await connection.QuerySingleAsync<(int Overspeed, int Sos, int Geofence, int Offline, int PowerCut)>(
            new CommandDefinition(
                """
                SELECT
                  ISNULL(SUM(CASE WHEN LOWER(e.EventType) IN ('overspeed','speed_exceeded') THEN 1 ELSE 0 END), 0),
                  ISNULL(SUM(CASE WHEN LOWER(e.EventType) IN ('sos','panic') THEN 1 ELSE 0 END), 0),
                  ISNULL(SUM(CASE WHEN LOWER(e.EventType) IN ('geofence_enter','geofence_exit') THEN 1 ELSE 0 END), 0),
                  ISNULL(SUM(CASE WHEN LOWER(e.EventType) IN ('gps_offline','vehicle_offline','offline') THEN 1 ELSE 0 END), 0),
                  ISNULL(SUM(CASE WHEN LOWER(e.EventType) IN ('power_cut','power_off') THEN 1 ELSE 0 END), 0)
                FROM GpsAlertEvents e
                INNER JOIN Vehicles v ON v.Id = e.VehicleId AND v.TenantId = @TenantId
                WHERE e.IsDeleted = 0 AND e.Timestamp >= @TodayStart
                """,
                new { TenantId = tenantId, TodayStart = todayStart },
                cancellationToken: cancellationToken));

        var tripsToday = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            SELECT COUNT(*) FROM GpsTrips t
            INNER JOIN Vehicles v ON v.Id = t.VehicleId AND v.TenantId = @TenantId
            WHERE t.StartTime >= @TodayStart
            """,
            new { TenantId = tenantId, TodayStart = todayStart },
            cancellationToken: cancellationToken));

        var todayDistance = await connection.ExecuteScalarAsync<decimal?>(new CommandDefinition(
            """
            SELECT SUM(ISNULL(t.DistanceKm, 0))
            FROM GpsTrips t
            INNER JOIN Vehicles v ON v.Id = t.VehicleId AND v.TenantId = @TenantId
            WHERE t.StartTime >= @TodayStart
            """,
            new { TenantId = tenantId, TodayStart = todayStart },
            cancellationToken: cancellationToken));

        return ApiResponse<GpsOperatorDashboardDto>.SuccessResponse(new GpsOperatorDashboardDto(
            fleet,
            health.Healthy,
            health.Offline,
            health.WeakGsm,
            health.LowBattery,
            health.NoGps,
            health.IgnitionOn,
            alertCounts.Overspeed,
            alertCounts.Sos,
            alertCounts.Geofence,
            alertCounts.Offline,
            alertCounts.PowerCut,
            tripsToday,
            todayDistance.HasValue ? (double?)Math.Round(todayDistance.Value, 1) : null));
        }

    public async Task<ApiResponse<GpsOperatorInsightDto>> PostGpsOperatorInsightsAsync(PostGpsOperatorInsightsCommand request, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();
        var key = (request.QueryKey ?? string.Empty).Trim().ToLowerInvariant();

        if (key.Contains("offline") || key.Contains("15"))
        {
            var rows = await connection.QueryAsync<(string Name, string Plate, int Minutes)>(
                new CommandDefinition(
                    """
                    SELECT v.Name, v.RegistrationNumber AS Plate,
                           DATEDIFF(MINUTE, vcl.LastUpdate, GETUTCDATE()) AS Minutes
                    FROM Vehicles v
                    INNER JOIN VehicleCurrentLocation vcl ON vcl.VehicleId = v.Id
                    WHERE v.TenantId = @TenantId AND v.IsDeleted = 0
                      AND DATEDIFF(MINUTE, vcl.LastUpdate, GETUTCDATE()) >= @OfflineStaleMinutes
                    ORDER BY Minutes DESC
                    """,
                    new { TenantId = tenantId, OfflineStaleMinutes },
                    cancellationToken: cancellationToken));
            var bullets = rows.Take(20)
                .Select(r => $"{r.Name} ({r.Plate}) — offline {r.Minutes} min")
                .ToList();
            return ApiResponse<GpsOperatorInsightDto>.SuccessResponse(
                new GpsOperatorInsightDto(
                    "Vehicles offline > 15 minutes",
                    bullets.Count == 0
                        ? "All tracked vehicles have communicated within 15 minutes."
                        : $"{bullets.Count} vehicle(s) exceed 15 minutes without updates.",
                    bullets));
        }

        if (key.Contains("overspeed") || key.Contains("speed"))
        {
            var rows = await connection.QueryAsync<(string Name, string Plate, decimal Speed)>(
                new CommandDefinition(
                    """
                    SELECT v.Name, v.RegistrationNumber AS Plate, vcl.Speed
                    FROM Vehicles v
                    INNER JOIN VehicleCurrentLocation vcl ON vcl.VehicleId = v.Id
                    WHERE v.TenantId = @TenantId AND v.IsDeleted = 0
                      AND vcl.Speed >= 90
                    ORDER BY vcl.Speed DESC
                    """,
                    new { TenantId = tenantId },
                    cancellationToken: cancellationToken));
            var bullets = rows.Take(20)
                .Select(r => $"{r.Name} ({r.Plate}) — {r.Speed:0} km/h")
                .ToList();
            return ApiResponse<GpsOperatorInsightDto>.SuccessResponse(
                new GpsOperatorInsightDto(
                    "Overspeed now (≥ 90 km/h)",
                    bullets.Count == 0 ? "No vehicles above threshold right now." : $"{bullets.Count} vehicle(s) overspeeding.",
                    bullets));
        }

        if (key.Contains("battery"))
        {
            var rows = await connection.QueryAsync<(string Name, decimal Batt)>(
                new CommandDefinition(
                    """
                    SELECT v.Name, vcl.BatteryLevel AS Batt
                    FROM Vehicles v
                    INNER JOIN VehicleCurrentLocation vcl ON vcl.VehicleId = v.Id
                    WHERE v.TenantId = @TenantId AND v.IsDeleted = 0
                      AND vcl.BatteryLevel IS NOT NULL AND vcl.BatteryLevel < 25
                    ORDER BY vcl.BatteryLevel
                    """,
                    new { TenantId = tenantId },
                    cancellationToken: cancellationToken));
            var bullets = rows.Take(20)
                .Select(r => $"{r.Name} — {r.Batt:0}% battery")
                .ToList();
            return ApiResponse<GpsOperatorInsightDto>.SuccessResponse(
                new GpsOperatorInsightDto(
                    "Low battery trackers",
                    bullets.Count == 0 ? "No low-battery trackers." : $"{bullets.Count} tracker(s) below 25%.",
                    bullets));
        }

        if (key.Contains("alert") || key.Contains("summarize"))
        {
            var todayStart = DateTime.UtcNow.Date;
            var stats = await connection.QuerySingleAsync<(int Total, int Critical, int Unread)>(
                new CommandDefinition(
                    """
                    SELECT COUNT(*),
                           ISNULL(SUM(CASE WHEN LOWER(Severity) IN ('critical','high') THEN 1 ELSE 0 END), 0),
                           ISNULL(SUM(CASE WHEN e.ReadAt IS NULL AND e.Status <> 'archived' THEN 1 ELSE 0 END), 0)
                    FROM GpsAlertEvents e
                    INNER JOIN Vehicles v ON v.Id = e.VehicleId AND v.TenantId = @TenantId
                    WHERE e.IsDeleted = 0 AND e.Timestamp >= @TodayStart
                    """,
                    new { TenantId = tenantId, TodayStart = todayStart },
                    cancellationToken: cancellationToken));
            var bullets = new List<string>
            {
                $"Total today: {stats.Total}",
                $"Critical/high: {stats.Critical}",
                $"Unread: {stats.Unread}",
            };
            return ApiResponse<GpsOperatorInsightDto>.SuccessResponse(
                new GpsOperatorInsightDto("Today's GPS alerts", "Summary for today (UTC).", bullets));
        }

        if (key.Contains("geofence"))
        {
            var todayStart = DateTime.UtcNow.Date;
            var count = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                """
                SELECT COUNT(*) FROM GpsAlertEvents e
                INNER JOIN Vehicles v ON v.Id = e.VehicleId AND v.TenantId = @TenantId
                WHERE e.IsDeleted = 0 AND e.Timestamp >= @TodayStart
                  AND LOWER(e.EventType) IN ('geofence_enter','geofence_exit')
                """,
                new { TenantId = tenantId, TodayStart = todayStart },
                cancellationToken: cancellationToken));
            return ApiResponse<GpsOperatorInsightDto>.SuccessResponse(
                new GpsOperatorInsightDto(
                    "Geofence violations today",
                    count == 0 ? "No geofence enter/exit alerts today." : $"{count} geofence alert(s) today.",
                    count == 0 ? new List<string>() : new List<string> { $"Count: {count}" }));
        }

        return ApiResponse<GpsOperatorInsightDto>.SuccessResponse(
            new GpsOperatorInsightDto(
                "Fleet insight",
                "Try a canned query from the list (offline, overspeed, alerts, battery, geofence).",
                new List<string>()));
        }

    public async Task<ApiResponse<List<GpsFleetStatusSnapshotDto>>> GetGpsFleetStatusHistoryAsync(GetGpsFleetStatusHistoryQuery request, CancellationToken cancellationToken = default)
    {
        var fromDate = request.FromDate ?? DateTime.UtcNow.AddDays(-7);
        var toDate = request.ToDate ?? DateTime.UtcNow;

        if (fromDate > toDate)
        {
            return ApiResponse<List<GpsFleetStatusSnapshotDto>>.FailResponse("'from' must be before 'to'.");
        }

        if (toDate - fromDate > FleetHistoryMaxRange)
        {
            return ApiResponse<List<GpsFleetStatusSnapshotDto>>.FailResponse("Date range cannot exceed 90 days.");
        }

        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        var rows = await connection.QueryAsync<GpsFleetStatusSnapshotDto>(new CommandDefinition(
            """
            SELECT SnapshotAt, TotalVehicles, Online, Offline, Moving, Idle, Parked, NeverSeen, Sos, AlertsToday
            FROM GpsFleetStatusSnapshots
            WHERE TenantId = @TenantId AND SnapshotAt BETWEEN @FromDate AND @ToDate
            ORDER BY SnapshotAt ASC
            """,
            new { TenantId = tenantId, FromDate = fromDate, ToDate = toDate },
            cancellationToken: cancellationToken));

        return ApiResponse<List<GpsFleetStatusSnapshotDto>>.SuccessResponse(rows.ToList());
        }


    public async Task<ApiResponse<bool>> IngestPositionAsync(IngestPositionCommand request, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var dto = request.Position;

        if (currentUser.Role == "Driver")
        {
            var driverId = currentUser.DriverId;
            if (!driverId.HasValue)
                return ApiResponse<bool>.FailResponse("Driver identity required.");

            var allowed = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
                @"SELECT CASE WHEN EXISTS(
                    SELECT 1 FROM Bookings
                    WHERE DriverId = @DriverId AND VehicleId = @VehicleId AND Status = @Started AND IsDeleted = 0) THEN 1 ELSE 0 END",
                new { DriverId = driverId.Value, dto.VehicleId, Started = (int)BookingStatus.Started },
                cancellationToken: cancellationToken));

            if (!allowed)
                return ApiResponse<bool>.FailResponse("GPS ingest only allowed during your active started trip for this vehicle.");

            dto = dto with { DriverId = driverId.Value };
        }

        var recordedAt = DateTime.UtcNow;

        // Read the previous state before ingestion overwrites the row — both previousSpeed (for
        // trip-persistence detection below) and previousIgnition (for the ignition-transition
        // alert) depend on this happening first; a future reordering of these calls would
        // silently break both.
        var previous = await connection.QueryFirstOrDefaultAsync<(
            decimal? Speed, bool? Ignition, double? Latitude, double? Longitude, string? Address)>(new CommandDefinition(
            "SELECT Speed, Ignition, Latitude, Longitude, Address FROM VehicleCurrentLocation WHERE VehicleId = @VehicleId",
            new { dto.VehicleId },
            cancellationToken: cancellationToken));
        var previousSpeed = previous.Speed;
        var previousIgnition = previous.Ignition;

        await positionIngestion.IngestAsync(connection, dto, recordedAt, cancellationToken);

        // Traccar often supplies city-only strings (e.g. "Pasrur, Punjab, PK") — upgrade via queue.
        var addressNeedsUpgrade = string.IsNullOrWhiteSpace(dto.Address)
            || TripReplayAddressEnricher.IsCoarseAddress(dto.Address);
        var previousNeedsUpgrade = string.IsNullOrWhiteSpace(previous.Address)
            || TripReplayAddressEnricher.IsCoarseAddress(previous.Address);
        var needsGeocode = addressNeedsUpgrade
            && (previousNeedsUpgrade
                || previous.Latitude is null
                || previous.Longitude is null
                || DistanceMeters(previous.Latitude.Value, previous.Longitude.Value, dto.Latitude, dto.Longitude) >= 150);

        if (needsGeocode)
        {
            addressBackfill.Enqueue(dto.VehicleId, dto.Latitude, dto.Longitude);
        }

        var bookingId = await positionIngestion.ResolveActiveBookingIdAsync(
            connection, dto.VehicleId, dto.BookingId, cancellationToken);

        if (dto.GpsDeviceId.HasValue)
        {
            await telemetryUpdater.UpdateAsync(
                connection,
                dto.GpsDeviceId.Value,
                recordedAt,
                dto.Ignition,
                dto.Speed,
                cancellationToken: cancellationToken);
        }

        var ingestDto = dto with { BookingId = bookingId };
        await EvaluateAlertsAsync(connection, ingestDto, recordedAt, cancellationToken);
        await EvaluateSosAsync(connection, ingestDto, recordedAt, cancellationToken);
        await EvaluateIgnitionTransitionAsync(connection, ingestDto, previousIgnition, recordedAt, cancellationToken);
        await EvaluateLowFuelAsync(connection, ingestDto, recordedAt, cancellationToken);
        await EvaluateLowBatteryAsync(connection, ingestDto, recordedAt, cancellationToken);
        await EvaluatePowerCutAsync(connection, ingestDto, recordedAt, cancellationToken);
        await EvaluateGpsLostAsync(connection, ingestDto, recordedAt, cancellationToken);

        // A position just arrived for this vehicle, so it's no longer offline — clear any
        // outstanding offline alert the background detector raised while it was unreachable, and
        // fire a one-time "online" event (rows affected > 0 means it actually was flagged offline).
        var clearedOffline = await connection.ExecuteAsync(new CommandDefinition(
            @"UPDATE GpsAlertEvents
              SET IsAcknowledged = 1,
                  Status = 'resolved',
                  AcknowledgedAt = GETUTCDATE(),
                  AcknowledgedBy = 'system',
                  ResolvedAt = GETUTCDATE(),
                  ResolvedBy = 'system',
                  ResolutionNotes = N'Vehicle back online'
              WHERE VehicleId = @VehicleId AND EventType = 'vehicle_offline' AND IsAcknowledged = 0 AND IsDeleted = 0",
            new { dto.VehicleId },
            cancellationToken: cancellationToken));

        if (clearedOffline > 0)
        {
            await InsertAlertAsync(connection, null, ingestDto, null, "online",
                "Vehicle has reconnected to the tracking server.", recordedAt, cancellationToken);
        }

        if (GpsPositionIngestionRules.ShouldAttemptTripPersistence(dto.Speed, dto.Ignition, previousSpeed))
        {
            await tripPersistence.TryPersistRecentTripsAsync(connection, dto.VehicleId, cancellationToken);
        }

        await broadcaster.BroadcastLocationUpdateAsync(
            dto.VehicleId,
            bookingId,
            dto.Latitude,
            dto.Longitude,
            dto.Speed,
            dto.Ignition,
            recordedAt,
            dto.Heading,
            dto.FuelLevel,
            dto.BatteryLevel,
            dto.GsmSignal,
            dto.TotalDistanceKm,
            dto.Address ?? previous.Address,
            dto.AlarmType,
            dto.Temperature,
            cancellationToken);

        return ApiResponse<bool>.SuccessResponse(true, "Position recorded.");
        }

    public async Task<ApiResponse<FuelAnalyticsDto>> GetFuelAnalyticsAsync(GetFuelAnalyticsQuery request, CancellationToken cancellationToken = default)
    {
        var fromDate = request.FromDate ?? DateTime.UtcNow.AddDays(-7);
        var toDate = request.ToDate ?? DateTime.UtcNow;

        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        var sql = """
            SELECT f.VehicleId, v.Name AS VehicleName, f.Liters, f.TotalCost AS Cost, f.FuelDate
            FROM FuelLogs f
            INNER JOIN Vehicles v ON v.Id = f.VehicleId AND v.TenantId = @TenantId AND v.IsDeleted = 0
            WHERE f.TenantId = @TenantId AND f.IsDeleted = 0 AND f.FuelDate BETWEEN @FromDate AND @ToDate
            """;

        var parameters = new DynamicParameters();
        parameters.Add("TenantId", tenantId);
        parameters.Add("FromDate", fromDate);
        parameters.Add("ToDate", toDate);

        if (request.BranchId.HasValue)
        {
            sql += " AND v.BranchId = @BranchId";
            parameters.Add("BranchId", request.BranchId.Value);
        }

        if (request.DepartmentId.HasValue)
        {
            sql += " AND v.DepartmentId = @DepartmentId";
            parameters.Add("DepartmentId", request.DepartmentId.Value);
        }

        if (request.DriverId.HasValue)
        {
            sql += " AND f.DriverId = @DriverId";
            parameters.Add("DriverId", request.DriverId.Value);
        }

        var logs = (await connection.QueryAsync<(int VehicleId, string? VehicleName, decimal Liters, decimal Cost, DateTime FuelDate)>(
            new CommandDefinition(sql, parameters, cancellationToken: cancellationToken))).ToList();

        var daily = logs
            .GroupBy(l => l.FuelDate.Date)
            .OrderBy(g => g.Key)
            .Select(g => new DailyFuelDto(g.Key, g.Sum(l => l.Liters), g.Sum(l => l.Cost)))
            .ToList();

        var tripsResponse = await mediator.Send(
            new GetGpsTripsQuery(null, fromDate, toDate, request.BranchId, request.DepartmentId, request.DriverId, Unpaged: true),
            cancellationToken);

        var distanceByVehicle = tripsResponse is { Success: true, Data: not null }
            ? tripsResponse.Data.Items.GroupBy(t => t.VehicleId).ToDictionary(g => g.Key, g => (decimal)g.Sum(t => t.DistanceKm))
            : [];

        var byVehicle = logs
            .GroupBy(l => l.VehicleId)
            .Select(g =>
            {
                var distance = distanceByVehicle.GetValueOrDefault(g.Key);
                var liters = g.Sum(l => l.Liters);
                var litersPer100Km = distance > 0 ? Math.Round(liters / distance * 100, 2) : (decimal?)null;
                return new VehicleFuelDto(g.Key, g.First().VehicleName, liters, g.Sum(l => l.Cost), distance > 0 ? distance : null, litersPer100Km);
            })
            .OrderByDescending(v => v.Liters)
            .ToList();

        var totalLiters = logs.Sum(l => l.Liters);
        var totalDistance = distanceByVehicle.Values.Sum();
        var fleetLitersPer100Km = totalDistance > 0 ? Math.Round(totalLiters / totalDistance * 100, 2) : (decimal?)null;

        var dto = new FuelAnalyticsDto(totalLiters, logs.Sum(l => l.Cost), fleetLitersPer100Km, daily, byVehicle);
        return ApiResponse<FuelAnalyticsDto>.SuccessResponse(dto);
        }

    public async Task<ApiResponse<CostAnalyticsDto>> GetCostAnalyticsAsync(GetCostAnalyticsQuery request, CancellationToken cancellationToken = default)
    {
        var fromDate = request.FromDate ?? DateTime.UtcNow.AddDays(-7);
        var toDate = request.ToDate ?? DateTime.UtcNow;

        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();
        var branchClause = request.BranchId.HasValue ? " AND v.BranchId = @BranchId" : "";
        var departmentClause = request.DepartmentId.HasValue ? " AND v.DepartmentId = @DepartmentId" : "";

        var fuelCost = await connection.ExecuteScalarAsync<decimal>(new CommandDefinition(
            $"""
            SELECT ISNULL(SUM(f.TotalCost), 0)
            FROM FuelLogs f
            INNER JOIN Vehicles v ON v.Id = f.VehicleId AND v.TenantId = @TenantId AND v.IsDeleted = 0
            WHERE f.IsDeleted = 0 AND f.FuelDate >= @FromDate AND f.FuelDate < @ToDate {branchClause} {departmentClause}
            """,
            new { TenantId = tenantId, FromDate = fromDate, ToDate = toDate, request.BranchId, request.DepartmentId },
            cancellationToken: cancellationToken));

        var maintenanceCost = await connection.ExecuteScalarAsync<decimal>(new CommandDefinition(
            $"""
            SELECT ISNULL(SUM(m.Cost + ISNULL(m.LaborCost,0) + ISNULL(m.PartsCost,0)), 0)
            FROM Maintenance m
            INNER JOIN Vehicles v ON v.Id = m.VehicleId AND v.TenantId = @TenantId AND v.IsDeleted = 0
            WHERE m.IsDeleted = 0 AND m.MaintenanceDate >= @FromDate AND m.MaintenanceDate < @ToDate {branchClause} {departmentClause}
            """,
            new { TenantId = tenantId, FromDate = fromDate, ToDate = toDate, request.BranchId, request.DepartmentId },
            cancellationToken: cancellationToken));

        var tripsResponse = await mediator.Send(
            new GetGpsTripsQuery(null, fromDate, toDate, request.BranchId, request.DepartmentId, null, Unpaged: true),
            cancellationToken);

        var totalDistance = tripsResponse is { Success: true, Data: not null }
            ? (decimal)tripsResponse.Data.Items.Sum(t => t.DistanceKm)
            : 0;

        var totalCost = fuelCost + maintenanceCost;
        var costPerKm = totalDistance > 0 ? Math.Round(totalCost / totalDistance, 2) : (decimal?)null;

        var dto = new CostAnalyticsDto(fuelCost, maintenanceCost, totalCost, Math.Round(totalDistance, 2), costPerKm, BasisNote);
        return ApiResponse<CostAnalyticsDto>.SuccessResponse(dto);
        }

    public async Task<ApiResponse<List<DriverScoreDto>>> GetDriverScoreRankingAsync(GetDriverScoreRankingQuery request, CancellationToken cancellationToken = default)
    {
        var fromDate = request.FromDate ?? DateTime.UtcNow.AddDays(-7);
        var toDate = request.ToDate ?? DateTime.UtcNow;

        var tripsResponse = await mediator.Send(
            new GetGpsTripsQuery(null, fromDate, toDate, request.BranchId, request.DepartmentId, null, Unpaged: true),
            cancellationToken);

        if (!tripsResponse.Success || tripsResponse.Data is null)
            return ApiResponse<List<DriverScoreDto>>.FailResponse(tripsResponse.Message ?? "Failed to load trips.");

        var trips = tripsResponse.Data.Items;
        if (trips.Count == 0)
            return ApiResponse<List<DriverScoreDto>>.SuccessResponse([]);

        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();
        var vehicleIds = trips.Select(t => t.VehicleId).Distinct().ToList();

        // AssignmentHistory windows overlapping the range for these vehicles — the join a trip (or
        // a Traccar stop/event, keyed by the same VehicleId+timestamp) is attributed to a driver through.
        var assignments = (await connection.QueryAsync<(int VehicleId, int? DriverId, DateTime StartAt, DateTime? EndAt)>(
            new CommandDefinition(
                """
                SELECT VehicleId, DriverId, StartAt, EndAt
                FROM AssignmentHistory
                WHERE TenantId = @TenantId AND IsDeleted = 0 AND DriverId IS NOT NULL
                  AND VehicleId IN @VehicleIds
                  AND StartAt <= @ToDate AND (EndAt IS NULL OR EndAt >= @FromDate)
                """,
                new { TenantId = tenantId, VehicleIds = vehicleIds, FromDate = fromDate, ToDate = toDate },
                cancellationToken: cancellationToken))).ToList();

        int? ResolveDriver(int vehicleId, DateTime at) =>
            assignments.FirstOrDefault(a => a.VehicleId == vehicleId && a.StartAt <= at && (a.EndAt == null || a.EndAt >= at)).DriverId;

        var attributedTrips = trips
            .Select(t => (Trip: t, DriverId: ResolveDriver(t.VehicleId, t.StartTime)))
            .Where(x => x.DriverId.HasValue)
            .ToList();

        if (attributedTrips.Count == 0)
            return ApiResponse<List<DriverScoreDto>>.SuccessResponse([]);

        var driverNames = (await connection.QueryAsync<(int Id, string FullName)>(new CommandDefinition(
            "SELECT Id, FullName FROM Drivers WHERE TenantId = @TenantId AND IsDeleted = 0",
            new { TenantId = tenantId },
            cancellationToken: cancellationToken))).ToDictionary(d => d.Id, d => d.FullName);

        var overspeedByDriver = (await connection.QueryAsync<(int DriverId, int Count)>(new CommandDefinition(
            """
            SELECT e.DriverId, COUNT(*) AS Count
            FROM GpsAlertEvents e
            INNER JOIN Vehicles v ON v.Id = e.VehicleId AND v.TenantId = @TenantId
            WHERE e.EventType IN ('overspeed', 'speed_exceeded') AND e.IsDeleted = 0
              AND e.DriverId IS NOT NULL AND e.Timestamp BETWEEN @FromDate AND @ToDate
            GROUP BY e.DriverId
            """,
            new { TenantId = tenantId, FromDate = fromDate, ToDate = toDate },
            cancellationToken: cancellationToken))).ToDictionary(r => r.DriverId, r => r.Count);

        var fuelByDriver = (await connection.QueryAsync<(int DriverId, decimal Liters)>(new CommandDefinition(
            """
            SELECT DriverId, SUM(Liters) AS Liters
            FROM FuelLogs
            WHERE TenantId = @TenantId AND IsDeleted = 0 AND DriverId IS NOT NULL
              AND FuelDate BETWEEN @FromDate AND @ToDate
            GROUP BY DriverId
            """,
            new { TenantId = tenantId, FromDate = fromDate, ToDate = toDate },
            cancellationToken: cancellationToken))).ToDictionary(r => r.DriverId, r => r.Liters);

        var idleMinutesByDriver = new Dictionary<int, int>();
        var harshCountByDriver = new Dictionary<int, int>();
        var traccarGap = false;

        var opts = traccarOptions.Value;
        if (opts.IsConfigured && opts.Enabled)
        {
            var deviceMap = await traccarFleetFetcher.ResolveVehicleToDeviceMapAsync(tenantId, vehicleIds, cancellationToken);
            traccarGap = await traccarFleetFetcher.HasNonTraccarVehicleAsync(tenantId, vehicleIds, cancellationToken);

            if (deviceMap.Count > 0)
            {
                var deviceToVehicle = deviceMap.ToDictionary(kv => kv.Value, kv => kv.Key);
                var stopsTasks = deviceMap.Values.Select(id => traccar.GetStopsAsync(id, fromDate, toDate, cancellationToken));
                var eventsTasks = deviceMap.Values.Select(id => traccar.GetEventsAsync(id, fromDate, toDate, ct: cancellationToken));

                var allStops = (await Task.WhenAll(stopsTasks)).SelectMany(s => s).ToList();
                var allEvents = (await Task.WhenAll(eventsTasks)).SelectMany(e => e).ToList();

                foreach (var stop in allStops)
                {
                    if (!deviceToVehicle.TryGetValue(stop.DeviceId, out var vehicleId)) continue;
                    var driverId = ResolveDriver(vehicleId, stop.StartTime);
                    if (!driverId.HasValue) continue;

                    var minutes = stop.Duration >= 100_000 ? stop.Duration / 60_000 : Math.Max(1, stop.Duration / 60);
                    idleMinutesByDriver[driverId.Value] = idleMinutesByDriver.GetValueOrDefault(driverId.Value) + minutes;
                }

                foreach (var evt in allEvents)
                {
                    if (!IsHarshEvent(evt.Type)) continue;
                    if (!deviceToVehicle.TryGetValue(evt.DeviceId, out var vehicleId)) continue;
                    var driverId = ResolveDriver(vehicleId, evt.EventTime);
                    if (!driverId.HasValue) continue;

                    harshCountByDriver[driverId.Value] = harshCountByDriver.GetValueOrDefault(driverId.Value) + 1;
                }
            }
        }
        else
        {
            traccarGap = true;
        }

        var results = new List<DriverScoreDto>();
        foreach (var group in attributedTrips.GroupBy(x => x.DriverId!.Value))
        {
            var driverId = group.Key;
            var driverTrips = group.Select(x => x.Trip).ToList();
            var distanceKm = (decimal)driverTrips.Sum(t => t.DistanceKm);
            var nightTrips = driverTrips.Count(t => t.StartTime.Hour >= NightStartHour || t.StartTime.Hour < NightEndHour);
            var nightPercent = driverTrips.Count > 0 ? Math.Round(nightTrips * 100m / driverTrips.Count, 1) : 0;
            var idleMinutes = idleMinutesByDriver.GetValueOrDefault(driverId);
            var drivingMinutes = driverTrips.Sum(t => t.DurationMinutes);
            var idlePercent = (drivingMinutes + idleMinutes) > 0
                ? Math.Round(idleMinutes * 100m / (drivingMinutes + idleMinutes), 1)
                : 0;

            var factors = new DriverScoreFactorsDto(
                driverTrips.Count,
                Math.Round(distanceKm, 2),
                overspeedByDriver.GetValueOrDefault(driverId),
                idleMinutes,
                harshCountByDriver.GetValueOrDefault(driverId),
                nightPercent,
                fuelByDriver.TryGetValue(driverId, out var liters) ? liters : null);

            var score = DriverScoreCalculator.ComputeScore(factors, idlePercent);

            results.Add(new DriverScoreDto(
                driverId,
                driverNames.GetValueOrDefault(driverId, $"Driver #{driverId}"),
                score,
                DriverScoreCalculator.RatingFor(score),
                factors,
                traccarGap));
        }

        return ApiResponse<List<DriverScoreDto>>.SuccessResponse(results.OrderByDescending(r => r.Score).ToList());
        }

    public async Task<ApiResponse<IdleAnalyticsDto>> GetIdleAnalyticsAsync(GetIdleAnalyticsQuery request, CancellationToken cancellationToken = default)
    {
        var fromDate = request.FromDate ?? DateTime.UtcNow.AddDays(-7);
        var toDate = request.ToDate ?? DateTime.UtcNow;

        var tripsResponse = await mediator.Send(
            new GetGpsTripsQuery(null, fromDate, toDate, request.BranchId, request.DepartmentId, request.DriverId, Unpaged: true),
            cancellationToken);

        if (!tripsResponse.Success || tripsResponse.Data is null)
            return ApiResponse<IdleAnalyticsDto>.FailResponse(tripsResponse.Message ?? "Failed to load trips.");

        var trips = tripsResponse.Data.Items;
        var vehicleNames = trips.GroupBy(t => t.VehicleId).ToDictionary(g => g.Key, g => g.First().VehicleName);

        var opts = traccarOptions.Value;
        if (!opts.IsConfigured || !opts.Enabled || trips.Count == 0)
        {
            return ApiResponse<IdleAnalyticsDto>.SuccessResponse(new IdleAnalyticsDto(0, 0, [], trips.Count > 0));
        }

        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();
        var vehicleIds = trips.Select(t => t.VehicleId).Distinct().ToList();

        var deviceMap = await traccarFleetFetcher.ResolveVehicleToDeviceMapAsync(tenantId, vehicleIds, cancellationToken);
        var isPartial = await traccarFleetFetcher.HasNonTraccarVehicleAsync(tenantId, vehicleIds, cancellationToken);

        if (deviceMap.Count == 0)
        {
            return ApiResponse<IdleAnalyticsDto>.SuccessResponse(new IdleAnalyticsDto(0, 0, [], true));
        }

        var deviceToVehicle = deviceMap.ToDictionary(kv => kv.Value, kv => kv.Key);
        var stopsTasks = deviceMap.Values.Select(id => traccar.GetStopsAsync(id, fromDate, toDate, cancellationToken));
        var allStops = (await Task.WhenAll(stopsTasks)).SelectMany(s => s).ToList();

        var perVehicleMinutes = new Dictionary<int, int>();
        var longest = 0;
        foreach (var stop in allStops)
        {
            var minutes = stop.Duration >= 100_000 ? stop.Duration / 60_000 : Math.Max(1, stop.Duration / 60);
            longest = Math.Max(longest, minutes);
            if (deviceToVehicle.TryGetValue(stop.DeviceId, out var vehicleId))
            {
                perVehicleMinutes[vehicleId] = perVehicleMinutes.GetValueOrDefault(vehicleId) + minutes;
            }
        }

        var topIdle = perVehicleMinutes
            .OrderByDescending(kv => kv.Value)
            .Take(10)
            .Select(kv => new VehicleIdleDto(kv.Key, vehicleNames.GetValueOrDefault(kv.Key), kv.Value))
            .ToList();

        var dto = new IdleAnalyticsDto(perVehicleMinutes.Values.Sum(), longest, topIdle, isPartial);
        return ApiResponse<IdleAnalyticsDto>.SuccessResponse(dto);
        }

    public async Task<ApiResponse<StopAnalyticsDto>> GetStopAnalyticsAsync(GetStopAnalyticsQuery request, CancellationToken cancellationToken = default)
    {
        var fromDate = request.FromDate ?? DateTime.UtcNow.AddDays(-7);
        var toDate = request.ToDate ?? DateTime.UtcNow;

        var tripsResponse = await mediator.Send(
            new GetGpsTripsQuery(null, fromDate, toDate, request.BranchId, request.DepartmentId, request.DriverId, Unpaged: true),
            cancellationToken);

        if (!tripsResponse.Success || tripsResponse.Data is null)
            return ApiResponse<StopAnalyticsDto>.FailResponse(tripsResponse.Message ?? "Failed to load trips.");

        var trips = tripsResponse.Data.Items;
        var opts = traccarOptions.Value;
        if (!opts.IsConfigured || !opts.Enabled || trips.Count == 0)
        {
            return ApiResponse<StopAnalyticsDto>.SuccessResponse(new StopAnalyticsDto(0, 0, 0, trips.Count > 0));
        }

        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();
        var vehicleIds = trips.Select(t => t.VehicleId).Distinct().ToList();

        var deviceMap = await traccarFleetFetcher.ResolveVehicleToDeviceMapAsync(tenantId, vehicleIds, cancellationToken);
        var isPartial = await traccarFleetFetcher.HasNonTraccarVehicleAsync(tenantId, vehicleIds, cancellationToken);

        if (deviceMap.Count == 0)
        {
            return ApiResponse<StopAnalyticsDto>.SuccessResponse(new StopAnalyticsDto(0, 0, 0, true));
        }

        var stopsTasks = deviceMap.Values.Select(id => traccar.GetStopsAsync(id, fromDate, toDate, cancellationToken));
        var allStops = (await Task.WhenAll(stopsTasks)).SelectMany(s => s).ToList();

        if (allStops.Count == 0)
        {
            return ApiResponse<StopAnalyticsDto>.SuccessResponse(new StopAnalyticsDto(0, 0, 0, isPartial));
        }

        var durations = allStops
            .Select(s => s.Duration >= 100_000 ? s.Duration / 60_000 : Math.Max(1, s.Duration / 60))
            .ToList();

        var dto = new StopAnalyticsDto(
            allStops.Count,
            Math.Round((decimal)durations.Average(), 1),
            durations.Max(),
            isPartial);

        return ApiResponse<StopAnalyticsDto>.SuccessResponse(dto);
        }

    public async Task<ApiResponse<AnalyticsOverviewDto>> GetAnalyticsOverviewAsync(GetAnalyticsOverviewQuery request, CancellationToken cancellationToken = default)
    {
        var todayStart = DateTime.UtcNow.Date;
        var now = DateTime.UtcNow;

        var fleetStatusTask = mediator.Send(new GetGpsFleetStatusLocalQuery(), cancellationToken);
        var rangeSummaryTask = mediator.Send(
            new GetFleetTripSummaryQuery(request.FromDate, request.ToDate, request.BranchId, request.DepartmentId, request.DriverId),
            cancellationToken);
        var todaySummaryTask = mediator.Send(
            new GetFleetTripSummaryQuery(todayStart, now, request.BranchId, request.DepartmentId, request.DriverId),
            cancellationToken);
        var geofenceStatsTask = mediator.Send(new GetGeofenceStatsQuery(), cancellationToken);
        var overspeedTodayTask = CountOverspeedTodayAsync(cancellationToken);

        await Task.WhenAll(fleetStatusTask, rangeSummaryTask, todaySummaryTask, geofenceStatsTask, overspeedTodayTask);

        var fleetStatus = await fleetStatusTask;
        var rangeSummary = await rangeSummaryTask;

        if (!fleetStatus.Success || fleetStatus.Data is null)
            return ApiResponse<AnalyticsOverviewDto>.FailResponse(fleetStatus.Message ?? "Failed to load fleet status.");

        if (!rangeSummary.Success || rangeSummary.Data is null)
            return ApiResponse<AnalyticsOverviewDto>.FailResponse(rangeSummary.Message ?? "Failed to load trip summary.");

        var status = fleetStatus.Data;
        var range = rangeSummary.Data;

        var todaySummary = await todaySummaryTask;
        var geofenceStats = await geofenceStatsTask;
        var overspeedToday = await overspeedTodayTask;

        var tripsToday = todaySummary is { Success: true, Data: not null } ? todaySummary.Data.TripCount : 0;
        var stopsToday = todaySummary is { Success: true, Data: not null } ? todaySummary.Data.StopCount : 0;
        var geofenceEntriesToday = geofenceStats is { Success: true, Data: not null } ? geofenceStats.Data.TodayEntries : 0;

        var utilizationPercent = status.TotalVehicles > 0
            ? Math.Round((status.Moving + status.Idle) * 100m / status.TotalVehicles, 1)
            : (decimal?)null;

        var dto = new AnalyticsOverviewDto(
            status.TotalVehicles, status.Online, status.Offline, status.Moving, status.Idle, status.Parked,
            (decimal)range.DistanceKm, range.DrivingMinutes, range.IdleMinutes, range.AvgSpeedKmh, range.MaxSpeedKmh,
            range.FuelLiters, range.EngineHours,
            tripsToday, stopsToday, geofenceEntriesToday, overspeedToday,
            utilizationPercent);

        return ApiResponse<AnalyticsOverviewDto>.SuccessResponse(dto);
        }

    public async Task<ApiResponse<List<VehicleRankingDto>>> GetVehicleRankingAsync(GetVehicleRankingQuery request, CancellationToken cancellationToken = default)
    {
        var fromDate = request.FromDate ?? DateTime.UtcNow.AddDays(-7);
        var toDate = request.ToDate ?? DateTime.UtcNow;

        var tripsResponse = await mediator.Send(
            new GetGpsTripsQuery(null, fromDate, toDate, request.BranchId, request.DepartmentId, null, Unpaged: true),
            cancellationToken);

        if (!tripsResponse.Success || tripsResponse.Data is null)
            return ApiResponse<List<VehicleRankingDto>>.FailResponse(tripsResponse.Message ?? "Failed to load trips.");

        var trips = tripsResponse.Data.Items;

        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();
        var branchClause = request.BranchId.HasValue ? " AND v.BranchId = @BranchId" : "";
        var departmentClause = request.DepartmentId.HasValue ? " AND v.DepartmentId = @DepartmentId" : "";

        var costs = (await connection.QueryAsync<(int VehicleId, decimal FuelCost, decimal MaintenanceCost)>(new CommandDefinition(
            $"""
            SELECT v.Id AS VehicleId,
                ISNULL((SELECT SUM(f.TotalCost) FROM FuelLogs f WHERE f.VehicleId = v.Id AND f.IsDeleted = 0
                    AND f.FuelDate >= @FromDate AND f.FuelDate < @ToDate), 0) AS FuelCost,
                ISNULL((SELECT SUM(m.Cost + ISNULL(m.LaborCost,0) + ISNULL(m.PartsCost,0)) FROM Maintenance m
                    WHERE m.VehicleId = v.Id AND m.IsDeleted = 0 AND m.MaintenanceDate >= @FromDate AND m.MaintenanceDate < @ToDate), 0) AS MaintenanceCost
            FROM Vehicles v
            WHERE v.IsDeleted = 0 AND v.TenantId = @TenantId {branchClause} {departmentClause}
            """,
            new { TenantId = tenantId, FromDate = fromDate, ToDate = toDate, request.BranchId, request.DepartmentId },
            cancellationToken: cancellationToken))).ToDictionary(c => c.VehicleId);

        var ranking = trips
            .GroupBy(t => new { t.VehicleId, t.VehicleName })
            .Select(g =>
            {
                var distance = (decimal)g.Sum(t => t.DistanceKm);
                var costEntry = costs.GetValueOrDefault(g.Key.VehicleId);
                var totalCost = costEntry.FuelCost + costEntry.MaintenanceCost;
                return new VehicleRankingDto(
                    g.Key.VehicleId,
                    g.Key.VehicleName ?? $"Vehicle #{g.Key.VehicleId}",
                    Math.Round(distance, 2),
                    g.Count(),
                    Math.Round(g.Average(t => t.AvgSpeedKmh), 1),
                    costEntry.FuelCost,
                    costEntry.MaintenanceCost,
                    distance > 0 ? Math.Round(totalCost / distance, 2) : null);
            })
            .OrderByDescending(v => v.DistanceKm)
            .ToList();

        return ApiResponse<List<VehicleRankingDto>>.SuccessResponse(ranking);
        }

    public async Task<ApiResponse<PagedResult<PositionDto>>> GetLivePositionsAsync(GetLivePositionsQuery request, CancellationToken cancellationToken = default)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? 500 : Math.Min(request.PageSize, MaxPageSize);

        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        const string whereClause = """
              FROM VehicleCurrentLocation vcl
              INNER JOIN Vehicles v ON v.Id = vcl.VehicleId AND v.TenantId = @TenantId AND v.IsDeleted = 0
              OUTER APPLY (
                  -- AssignmentHistory is the current ownership source. VehicleCurrentLocation is
                  -- telemetry and can retain the driver from the previous position report.
                  SELECT TOP 1 a.DriverId
                  FROM AssignmentHistory a
                  WHERE a.VehicleId = vcl.VehicleId AND a.IsDeleted = 0
                    AND a.Status IN (N'Active', N'Scheduled') AND a.DriverId IS NOT NULL
                  ORDER BY CASE WHEN a.Status = N'Active' THEN 0 ELSE 1 END, a.StartAt DESC
              ) assignDrv
              LEFT JOIN Drivers dr ON dr.Id = COALESCE(assignDrv.DriverId, vcl.DriverId) AND dr.IsDeleted = 0
              WHERE vcl.Latitude IS NOT NULL
                AND vcl.Longitude IS NOT NULL
                AND NOT (vcl.Latitude = 0 AND vcl.Longitude = 0)
            """;

        var totalCount = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            $"SELECT COUNT(*) {whereClause}",
            new { TenantId = tenantId },
            cancellationToken: cancellationToken));

        var rows = await connection.QueryAsync<PositionDto>(new CommandDefinition(
            $"""
              SELECT CAST(vcl.VehicleId AS BIGINT) AS Id,
                     vcl.VehicleId,
                     COALESCE(assignDrv.DriverId, vcl.DriverId) AS DriverId,
                     vcl.BookingId,
                     vcl.GpsDeviceId,
                     vcl.Latitude,
                     vcl.Longitude,
                     ISNULL(vcl.Speed, 0) AS Speed,
                     vcl.Heading,
                     CAST(NULL AS FLOAT) AS Altitude,
                     vcl.Ignition,
                     vcl.LastUpdate AS Timestamp,
                     vcl.FuelLevel,
                     vcl.BatteryLevel,
                     vcl.GsmSignal,
                     vcl.TotalDistanceKm,
                     vcl.Address,
                     vcl.AlarmType,
                     dr.Phone AS DriverPhone,
                     vcl.Temperature
              {whereClause}
              ORDER BY vcl.VehicleId
              OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
            """,
            new { TenantId = tenantId, Offset = (page - 1) * pageSize, PageSize = pageSize },
            cancellationToken: cancellationToken));

        var items = rows
            .Select(r => r with { Timestamp = GpsUtcDateTime.AsUtc(r.Timestamp) })
            .ToList();

        items = await EnrichLiveAddressesAsync(connection, items, cancellationToken);

        return ApiResponse<PagedResult<PositionDto>>.SuccessResponse(new PagedResult<PositionDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        });
        }

    public async Task<ApiResponse<List<PositionDto>>> GetPositionHistoryAsync(GetPositionHistoryQuery request, CancellationToken cancellationToken = default)
    {
        var fromDate = request.FromDate ?? DateTime.UtcNow.AddDays(-1);
        var toDate = request.ToDate ?? DateTime.UtcNow;

        if (fromDate.Kind == DateTimeKind.Unspecified)
            fromDate = DateTime.SpecifyKind(fromDate, DateTimeKind.Utc);
        if (toDate.Kind == DateTimeKind.Unspecified)
            toDate = DateTime.SpecifyKind(toDate, DateTimeKind.Utc);

        if (fromDate > toDate)
        {
            return ApiResponse<List<PositionDto>>.FailResponse("'from' must be before 'to'.");
        }

        if (toDate - fromDate > TripVehicleQueryValidation.MaxHistoryRange)
        {
            return ApiResponse<List<PositionDto>>.FailResponse("Date range cannot exceed 366 days.");
        }

        using var connection = dbFactory.CreateConnection();
        var toExclusive = toDate.AddTicks(1);
        var localRows = await connection.QueryAsync<GpsPositionHistoryRow>(new CommandDefinition(
            @"SELECT Id, VehicleId, DriverId, BookingId, GpsDeviceId, Latitude, Longitude, Speed,
                     Heading, Altitude, Ignition, RecordedAt AS Timestamp, Address
              FROM GpsPositions
              WHERE VehicleId = @VehicleId
                AND RecordedAt >= @FromDate
                AND RecordedAt < @ToExclusive
              ORDER BY RecordedAt ASC",
            new { request.VehicleId, FromDate = fromDate, ToExclusive = toExclusive },
            cancellationToken: cancellationToken));

        var local = localRows.Select(GpsPositionHistoryMapper.ToPositionDto).ToList();

        // Prefer Traccar when the device is linked — local GpsPositions is only as complete as
        // our ingest window and often under-reports weekly mileage vs Traccar's full archive.
        if (traccarOptions.Value.Enabled)
        {
            var link = await connection.QuerySingleOrDefaultAsync<(int? GpsDeviceId, int? TraccarDeviceId)>(
                new CommandDefinition(
                    """
                    SELECT TOP 1 d.Id AS GpsDeviceId, d.TraccarDeviceId
                    FROM GpsDevices d
                    WHERE d.VehicleId = @VehicleId AND d.IsDeleted = 0 AND d.TraccarDeviceId IS NOT NULL
                    ORDER BY d.Id DESC
                    """,
                    new { request.VehicleId },
                    cancellationToken: cancellationToken));

            if (link.TraccarDeviceId is int traccarDeviceId)
            {
                var route = await traccar.GetRouteAsync(traccarDeviceId, fromDate, toDate, cancellationToken);
                if (route.Count < SparseRemoteThreshold)
                {
                    var remotePositions = await traccar.GetPositionsByDeviceAsync(
                        traccarDeviceId, fromDate, toDate, cancellationToken);
                    if (remotePositions.Count > route.Count)
                        route = remotePositions;
                }

                if (route.Count > 0)
                {
                    var remote = route
                        .Select(p => GpsPositionHistoryMapper.FromTraccar(p, request.VehicleId, link.GpsDeviceId))
                        .OrderBy(p => p.Timestamp)
                        .ToList();

                    return ApiResponse<List<PositionDto>>.SuccessResponse(
                        GpsPositionHistoryMapper.DownsampleForPlayback(remote));
                }
            }
        }

        return ApiResponse<List<PositionDto>>.SuccessResponse(
            GpsPositionHistoryMapper.DownsampleForPlayback(local));
        }

    public async Task<ApiResponse<PagedResult<GpsTripDto>>> GetGpsTripsAsync(GetGpsTripsQuery request, CancellationToken cancellationToken = default)
    {
        var fromDate = request.FromDate ?? DateTime.UtcNow.AddDays(-7);
        var toDate = request.ToDate ?? DateTime.UtcNow;

        if (fromDate > toDate)
        {
            return ApiResponse<PagedResult<GpsTripDto>>.FailResponse("'from' must be before 'to'.");
        }

        if (toDate - fromDate > TripVehicleQueryValidation.MaxRange)
        {
            return ApiResponse<PagedResult<GpsTripDto>>.FailResponse("Date range cannot exceed 30 days.");
        }

        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        List<GpsTripDto> allTrips;
        if (request.VehicleId.HasValue)
        {
            var single = await HandleSingleVehicleAsync(connection, tenantId, request.VehicleId.Value, fromDate, toDate, cancellationToken);
            if (!single.Success || single.Data is null)
            {
                return ApiResponse<PagedResult<GpsTripDto>>.FailResponse(single.Message ?? "Failed to load trips.");
            }

            allTrips = single.Data;
        }
        else
        {
            var fleet = await HandleFleetWideAsync(connection, tenantId, request, fromDate, toDate, cancellationToken);
            if (!fleet.Success || fleet.Data is null)
            {
                return ApiResponse<PagedResult<GpsTripDto>>.FailResponse(fleet.Message ?? "Failed to load trips.");
            }

            allTrips = fleet.Data;
        }

        allTrips = ApplyFilters(allTrips, request);
        allTrips = ApplySort(allTrips, request.SortBy, request.SortDir);
        allTrips = await EnrichTripAddressesAsync(allTrips, cancellationToken);
        return ApiResponse<PagedResult<GpsTripDto>>.SuccessResponse(ToPage(allTrips, request));
        }

    public async Task<ApiResponse<HistoryReplayBundleDto>> GetHistoryReplayAsync(GetHistoryReplayQuery request, CancellationToken cancellationToken = default)
    {
        var fromDate = request.FromDate ?? DateTime.UtcNow.AddDays(-7);
        var toDate = request.ToDate ?? DateTime.UtcNow;
        fromDate = GpsUtcDateTime.AsUtc(fromDate);
        toDate = GpsUtcDateTime.AsUtc(toDate);

        var validation = TripVehicleQueryValidation.ValidateHistoryRequest<HistoryReplayBundleDto>(
            request.VehicleId, fromDate, toDate, request.DeviceId);
        if (validation is not null) return validation;

        VehicleTripSource? source;
        if (request.VehicleId.HasValue)
        {
            source = await tripVehicleQuery.ResolveVehicleAsync(request.VehicleId.Value, cancellationToken);
        }
        else
        {
            source = await tripVehicleQuery.ResolveVehicleByTraccarDeviceIdAsync(request.DeviceId!.Value, cancellationToken);
        }

        if (source is null)
            return ApiResponse<HistoryReplayBundleDto>.FailResponse(
                request.DeviceId.HasValue ? "GPS device not found." : "Vehicle not found.");

        var vehicleId = source.VehicleId;

        var vehicleContext = await tripVehicleQuery.BuildContextAsync(vehicleId, cancellationToken);
        if (vehicleContext is null)
            return ApiResponse<HistoryReplayBundleDto>.FailResponse("Vehicle context unavailable.");
        var tenantId = tenantContext.GetRequiredTenantId();
        var routeMaxPoints = request.RouteMaxPoints;
        var playbackMaxPoints = request.PlaybackMaxPoints;
        var includeRaw = request.IncludeRaw;

        var cacheKey = $"gps:history-replay:{tenantId}:{vehicleId}:{fromDate:O}:{toDate:O}:{routeMaxPoints?.ToString() ?? "default"}:{playbackMaxPoints?.ToString() ?? "default"}:{includeRaw}";

        return await cache.GetOrCreateAsync(
            cacheKey,
            ReplayCacheTtl,
            async ct => await BuildReplayResponseAsync(
                source,
                vehicleContext!,
                vehicleId,
                fromDate,
                toDate,
                routeMaxPoints,
                playbackMaxPoints,
                includeRaw,
                ct),
            cancellationToken);
        }

    public async Task<ApiResponse<HistoryExportFileDto>> GetHistoryExportAsync(GetHistoryExportQuery request, CancellationToken cancellationToken = default)
    {
        var fromDate = request.FromDate ?? DateTime.UtcNow.AddDays(-1);
        var toDate = request.ToDate ?? DateTime.UtcNow;
        fromDate = GpsUtcDateTime.AsUtc(fromDate);
        toDate = GpsUtcDateTime.AsUtc(toDate);

        if (fromDate > toDate)
            return ApiResponse<HistoryExportFileDto>.FailResponse("'from' must be before 'to'.");
        if (toDate - fromDate > TripVehicleQueryValidation.MaxHistoryRange)
            return ApiResponse<HistoryExportFileDto>.FailResponse("Date range cannot exceed 366 days.");

        var format = (request.Format ?? "csv").Trim().ToLowerInvariant();
        if (format is not ("csv" or "gpx" or "geojson" or "kml"))
            return ApiResponse<HistoryExportFileDto>.FailResponse("Format must be csv, gpx, geojson, or kml.");

        var positions = await LoadFullPositionsAsync(
            dbFactory, traccar, traccarOptions.Value, request.VehicleId, fromDate, toDate, cancellationToken);

        if (positions.Count == 0)
            return ApiResponse<HistoryExportFileDto>.FailResponse("No tracking points in this period.");

        if (positions.Count > MaxExportRows)
            positions = positions.Take(MaxExportRows).ToList();

        var stamp = fromDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var stampTo = toDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var baseName = $"gps-history-{request.VehicleId}-{stamp}-{stampTo}";

        return format switch
        {
            "gpx" => ApiResponse<HistoryExportFileDto>.SuccessResponse(
                new HistoryExportFileDto(
                    Encoding.UTF8.GetBytes(HistoryExportFormatter.ToGpx(positions, request.VehicleId)),
                    "application/gpx+xml",
                    $"{baseName}.gpx")),
            "kml" => ApiResponse<HistoryExportFileDto>.SuccessResponse(
                new HistoryExportFileDto(
                    Encoding.UTF8.GetBytes(HistoryExportFormatter.ToKml(positions, request.VehicleId)),
                    "application/vnd.google-earth.kml+xml",
                    $"{baseName}.kml")),
            "geojson" => ApiResponse<HistoryExportFileDto>.SuccessResponse(
                new HistoryExportFileDto(
                    Encoding.UTF8.GetBytes(HistoryExportFormatter.ToGeoJson(positions)),
                    "application/geo+json",
                    $"{baseName}.geojson")),
            _ => ApiResponse<HistoryExportFileDto>.SuccessResponse(
                new HistoryExportFileDto(
                    Encoding.UTF8.GetBytes(HistoryExportFormatter.ToCsv(positions)),
                    "text/csv",
                    $"{baseName}.csv"))
        };
        }

    public async Task<ApiResponse<TripAnalyticsBundleDto>> GetTripAnalyticsAsync(GetTripAnalyticsQuery request, CancellationToken cancellationToken = default)
    {
        var fromDate = request.FromDate ?? DateTime.UtcNow.AddDays(-7);
        var toDate = request.ToDate ?? DateTime.UtcNow;
        var validation = TripVehicleQueryValidation.ValidateTripRequest<TripAnalyticsBundleDto>(request.VehicleId, fromDate, toDate);
        if (validation is not null) return validation;

        var vehicleId = request.VehicleId!.Value;
        var source = await tripVehicleQuery.ResolveVehicleAsync(vehicleId, cancellationToken);
        if (source is null) return ApiResponse<TripAnalyticsBundleDto>.FailResponse("Vehicle not found.");

        var tripsTask = mediator.Send(new GetGpsTripsQuery(vehicleId, fromDate, toDate, Unpaged: true), cancellationToken);
        var summaries = Array.Empty<TraccarSummary>();
        var stops = Array.Empty<TraccarStop>();
        var events = Array.Empty<TraccarEvent>();

        var opts = traccarOptions.Value;
        if (opts.IsConfigured && opts.Enabled && source.TraccarDeviceId.HasValue)
        {
            var deviceId = source.TraccarDeviceId.Value;
            var summaryTask = traccar.GetSummaryAsync(deviceId, fromDate, toDate, cancellationToken);
            var stopsTask = traccar.GetStopsAsync(deviceId, fromDate, toDate, cancellationToken);
            var eventsTask = traccar.GetEventsAsync(deviceId, fromDate, toDate, ct: cancellationToken);
            await Task.WhenAll(tripsTask, summaryTask, stopsTask, eventsTask);
            summaries = (await summaryTask).ToArray();
            stops = (await stopsTask).ToArray();
            events = (await eventsTask).ToArray();
        }
        else
        {
            await tripsTask;
        }

        var tripsResponse = await tripsTask;
        if (!tripsResponse.Success || tripsResponse.Data is null)
        {
            return ApiResponse<TripAnalyticsBundleDto>.FailResponse(tripsResponse.Message ?? "Failed to load trips.");
        }

        var trips = tripsResponse.Data.Items;
        var summary = TripAnalyticsMapper.BuildSummary(trips, summaries, stops, events);
        var eventDtos = events.Select(e => TripAnalyticsMapper.ToEventDto(e)).OrderByDescending(e => e.Time).ToList();
        var stopDtos = stops.Select(TripAnalyticsMapper.ToStopDto).OrderByDescending(s => s.EndTime).ToList();

        return ApiResponse<TripAnalyticsBundleDto>.SuccessResponse(
            new TripAnalyticsBundleDto(summary, eventDtos, stopDtos));
        }

    public async Task<ApiResponse<TripReplayBundleDto>> GetTripReplayAsync(GetTripReplayQuery request, CancellationToken cancellationToken = default)
    {
        var fromDate = request.FromDate ?? DateTime.UtcNow.AddDays(-1);
        var toDate = request.ToDate ?? DateTime.UtcNow;
        var validation = TripVehicleQueryValidation.ValidateTripRequest<TripReplayBundleDto>(request.VehicleId, fromDate, toDate);
        if (validation is not null) return validation;

        var vehicleId = request.VehicleId!.Value;
        var source = await tripVehicleQuery.ResolveVehicleAsync(vehicleId, cancellationToken);
        if (source is null) return ApiResponse<TripReplayBundleDto>.FailResponse("Vehicle not found.");

        var opts = traccarOptions.Value;
        if (opts.IsConfigured && opts.Enabled && source.TraccarDeviceId.HasValue)
        {
            var bundle = await TripReplayLoader.LoadFromTraccarAsync(
                traccar,
                source.TraccarDeviceId.Value,
                fromDate,
                toDate,
                cancellationToken,
                routeMaxPoints: request.RouteMaxPoints,
                playbackMaxPoints: request.PlaybackMaxPoints,
                includeRaw: request.IncludeRaw);
            bundle = await TripReplayAddressEnricher.EnrichAsync(
                bundle,
                geocoder,
                cancellationToken);
            return ApiResponse<TripReplayBundleDto>.SuccessResponse(bundle);
        }

        var localBundle = await TripReplayLoader.LoadFromLocalHistoryAsync(
            mediator, vehicleId, fromDate, toDate, cancellationToken,
            routeMaxPoints: request.RouteMaxPoints,
            playbackMaxPoints: request.PlaybackMaxPoints,
            includeRaw: request.IncludeRaw);
        if (localBundle.Route.Count == 0 && localBundle.Playback.Count == 0)
        {
            return ApiResponse<TripReplayBundleDto>.FailResponse("Failed to load replay positions.");
        }

        localBundle = await TripReplayAddressEnricher.EnrichAsync(
            localBundle,
            geocoder,
            cancellationToken);

        return ApiResponse<TripReplayBundleDto>.SuccessResponse(localBundle);
        }

    public async Task<ApiResponse<TripAnalyticsSummaryDto>> GetFleetTripSummaryAsync(GetFleetTripSummaryQuery request, CancellationToken cancellationToken = default)
    {
        var fromDate = request.FromDate ?? DateTime.UtcNow.AddDays(-7);
        var toDate = request.ToDate ?? DateTime.UtcNow;

        var tripsResponse = await mediator.Send(
            new GetGpsTripsQuery(null, fromDate, toDate, request.BranchId, request.DepartmentId, request.DriverId, Unpaged: true),
            cancellationToken);

        if (!tripsResponse.Success || tripsResponse.Data is null)
        {
            return ApiResponse<TripAnalyticsSummaryDto>.FailResponse(tripsResponse.Message ?? "Failed to load trips.");
        }

        var trips = tripsResponse.Data.Items;
        var summaries = Array.Empty<TraccarSummary>();
        var stops = Array.Empty<TraccarStop>();
        var events = Array.Empty<TraccarEvent>();

        var opts = traccarOptions.Value;
        if (opts.IsConfigured && opts.Enabled && trips.Count > 0)
        {
            using var connection = dbFactory.CreateConnection();
            var tenantId = tenantContext.GetRequiredTenantId();

            var deviceIds = (await connection.QueryAsync<int>(new CommandDefinition(
                """
                SELECT DISTINCT d.TraccarDeviceId
                FROM Vehicles v
                INNER JOIN GpsDevices d ON d.Id = v.GpsDeviceId AND d.IsDeleted = 0
                WHERE v.TenantId = @TenantId AND v.IsDeleted = 0 AND d.TraccarDeviceId IS NOT NULL
                  AND v.Id IN @VehicleIds
                """,
                new { TenantId = tenantId, VehicleIds = trips.Select(t => t.VehicleId).Distinct().ToList() },
                cancellationToken: cancellationToken))).ToList();

            if (deviceIds.Count > 0)
            {
                var summaryTasks = deviceIds.Select(id => traccar.GetSummaryAsync(id, fromDate, toDate, cancellationToken));
                var stopsTasks = deviceIds.Select(id => traccar.GetStopsAsync(id, fromDate, toDate, cancellationToken));
                var eventsTasks = deviceIds.Select(id => traccar.GetEventsAsync(id, fromDate, toDate, ct: cancellationToken));

                var allSummaries = await Task.WhenAll(summaryTasks);
                var allStops = await Task.WhenAll(stopsTasks);
                var allEvents = await Task.WhenAll(eventsTasks);

                summaries = allSummaries.SelectMany(s => s).ToArray();
                stops = allStops.SelectMany(s => s).ToArray();
                events = allEvents.SelectMany(e => e).ToArray();
            }
        }

        var summary = TripAnalyticsMapper.BuildSummary(trips, summaries, stops, events);

        return ApiResponse<TripAnalyticsSummaryDto>.SuccessResponse(summary);
        }

}
