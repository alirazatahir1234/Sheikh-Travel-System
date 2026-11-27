using System.Data;
using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.GpsTracking.Geofences;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories;

/// <summary>
/// Reads for the geofences workspace. Every statement is scoped to the caller's tenant (ITenantContext); writes are
/// in GeofenceWorkspaceRepository.Writes.cs. "Today" and the visits report's days are the company's local days.
/// </summary>
public sealed partial class GeofenceWorkspaceRepository(
    IDbConnectionFactory dbFactory,
    ITenantContext tenantContext,
    ICurrentUserService currentUser,
    ITenantTimeZoneResolver timeZoneResolver,
    ILogger<GeofenceWorkspaceRepository> logger) : IGeofenceWorkspaceRepository
{
    /// <summary>Rule alerts that count as violations (GpsAlertEvents); restricted-zone entries are counted from visits.</summary>
    private static readonly string[] ViolationEventTypes =
    [
        GeofenceRuleEvents.Dwell, GeofenceRuleEvents.Overspeed, GeofenceRuleEvents.Idle, GeofenceRuleEvents.AfterHoursExit
    ];

    /// <summary>Positions read by "test with history" at most; above it the entry count is a lower bound.</summary>
    private const int TestShapePositionCap = 50_000;

    private const string VehicleLabelSql = "COALESCE(NULLIF(LTRIM(RTRIM(v.RegistrationNumber)), N''), v.Name)";

    /// <summary>Distinct live vehicles a geofence covers (all, or vehicle / branch / department assignments).</summary>
    private const string VehiclesCoveredSql = """
        (SELECT COUNT(DISTINCT cv.Id) FROM Vehicles cv
         WHERE cv.TenantId = @TenantId AND cv.IsDeleted = 0
           AND (g.AppliesToAll = 1 OR EXISTS (
                SELECT 1 FROM GeofenceAssignments ca
                WHERE ca.GeofenceId = g.Id AND ca.IsDeleted = 0
                  AND (ca.VehicleId = cv.Id
                       OR (ca.BranchId IS NOT NULL AND ca.BranchId = cv.BranchId)
                       OR (ca.DepartmentId IS NOT NULL AND ca.DepartmentId = cv.DepartmentId)))))
        """;

    private int TenantId => tenantContext.GetRequiredTenantId();

    private async Task<TimeZoneInfo> ZoneAsync(int tenantId, CancellationToken cancellationToken)
        => await timeZoneResolver.GetTenantTimeZoneAsync(tenantId, cancellationToken);

    private static DateOnly LocalToday(TimeZoneInfo zone)
        => TenantTimeZoneHelper.UtcToLocalDate(DateTime.UtcNow, zone);

    public async Task<IReadOnlyList<GeofenceListItemDto>> GetListAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        var zone = await ZoneAsync(tenantId, cancellationToken);
        var todayStartUtc = TenantTimeZoneHelper.LocalDayStartUtc(LocalToday(zone), zone);

        using var connection = dbFactory.CreateConnection();
        var restricted = await LoadRestrictedTypeNamesAsync(connection, tenantId, cancellationToken);
        var rows = await connection.QueryAsync<GeofenceRow>(new CommandDefinition(
            $"""
            SELECT g.Id, g.Name, g.AreaType, g.CenterLat, g.CenterLng, g.RadiusMeters, g.GeoJson, g.IsActive, g.Color,
                   g.Category AS Type, g.GroupId, gg.Name AS GroupName, g.Address, g.Description, g.BufferMeters,
                   g.CorridorWidthMeters, g.AreaSquareMeters, g.PerimeterMeters, g.AppliesToAll,
                   g.DwellMinutes, g.SpeedLimitKmh, g.IdleMinutes, g.NotArrivedBy, g.EngineBlockOnExit,
                   CAST(COALESCE(ar.AlertOnEnter, 0) AS BIT) AS AlertOnEnter,
                   CAST(COALESCE(ar.AlertOnExit, 0) AS BIT) AS AlertOnExit,
                   (SELECT COUNT(1) FROM GeofenceVisits gv
                    WHERE gv.GeofenceId = g.Id AND gv.TenantId = @TenantId AND gv.ExitedUtc IS NULL) AS InsideNow,
                   (SELECT COUNT(1) FROM GeofenceVisits gv
                    WHERE gv.GeofenceId = g.Id AND gv.TenantId = @TenantId AND gv.EnteredUtc >= @TodayStartUtc) AS EntriesToday,
                   (SELECT COUNT(1) FROM GpsAlertEvents e
                    WHERE e.GeofenceId = g.Id AND e.IsDeleted = 0 AND e.Timestamp >= @TodayStartUtc
                      AND e.EventType IN @ViolationTypes) AS RuleAlertsToday,
                   {VehiclesCoveredSql} AS VehiclesCovered,
                   (SELECT COUNT(1) FROM GeofenceAssignments da
                    WHERE da.GeofenceId = g.Id AND da.IsDeleted = 0 AND da.DriverId IS NOT NULL) AS DriversAssigned
            FROM Geofences g
            LEFT JOIN GeofenceGroups gg ON gg.Id = g.GroupId AND gg.TenantId = @TenantId AND gg.IsDeleted = 0
            OUTER APPLY (SELECT TOP 1 r.AlertOnEnter, r.AlertOnExit FROM GpsAlertRules r
                         WHERE r.GeofenceId = g.Id AND r.TenantId = @TenantId AND r.VehicleId IS NULL
                           AND r.IsActive = 1 AND r.IsDeleted = 0
                         ORDER BY r.Id) ar
            WHERE g.TenantId = @TenantId AND g.IsDeleted = 0
            ORDER BY CASE WHEN gg.Id IS NULL THEN 1 ELSE 0 END, gg.SortOrder, gg.Name, g.Name
            """,
            new { TenantId = tenantId, TodayStartUtc = todayStartUtc, ViolationTypes = ViolationEventTypes },
            cancellationToken: cancellationToken));

        return rows.Select(r => r.ToListItem(IsRestricted(r.Type, restricted))).ToList();
    }

    public async Task<GeofenceDetailDto?> GetDetailAsync(int id, CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        using var connection = dbFactory.CreateConnection();
        using var multi = await connection.QueryMultipleAsync(new CommandDefinition(
            $"""
            SELECT g.Id, g.Name, g.AreaType, g.CenterLat, g.CenterLng, g.RadiusMeters, g.GeoJson, g.IsActive, g.Color,
                   g.Category AS Type, g.GroupId, gg.Name AS GroupName, g.Address, g.Description, g.BufferMeters,
                   g.CorridorWidthMeters, g.AreaSquareMeters, g.PerimeterMeters, g.AppliesToAll,
                   g.DwellMinutes, g.SpeedLimitKmh, g.IdleMinutes, g.NotArrivedBy, g.EngineBlockOnExit,
                   g.ScheduleDays, g.ScheduleStart, g.ScheduleEnd, g.AfterHoursExitCritical, g.NotifyChannels, g.CooldownMinutes,
                   CAST(COALESCE(ar.AlertOnEnter, 0) AS BIT) AS AlertOnEnter,
                   CAST(COALESCE(ar.AlertOnExit, 0) AS BIT) AS AlertOnExit,
                   {VehiclesCoveredSql} AS VehiclesCovered
            FROM Geofences g
            LEFT JOIN GeofenceGroups gg ON gg.Id = g.GroupId AND gg.TenantId = @TenantId AND gg.IsDeleted = 0
            OUTER APPLY (SELECT TOP 1 r.AlertOnEnter, r.AlertOnExit FROM GpsAlertRules r
                         WHERE r.GeofenceId = g.Id AND r.TenantId = @TenantId AND r.VehicleId IS NULL
                           AND r.IsActive = 1 AND r.IsDeleted = 0
                         ORDER BY r.Id) ar
            WHERE g.Id = @Id AND g.TenantId = @TenantId AND g.IsDeleted = 0;

            SELECT a.VehicleId, a.BranchId, a.DepartmentId, a.DriverId
            FROM GeofenceAssignments a
            INNER JOIN Geofences g ON g.Id = a.GeofenceId AND g.TenantId = @TenantId AND g.IsDeleted = 0
            WHERE a.GeofenceId = @Id AND a.IsDeleted = 0;

            SELECT RoleId, UserId FROM GeofenceRecipients WHERE GeofenceId = @Id AND TenantId = @TenantId;
            """,
            new { Id = id, TenantId = tenantId },
            cancellationToken: cancellationToken));

        var row = await multi.ReadFirstOrDefaultAsync<GeofenceRow>();
        if (row is null)
            return null;

        var assignments = (await multi.ReadAsync<(int? VehicleId, int? BranchId, int? DepartmentId, int? DriverId)>()).ToList();
        var recipients = (await multi.ReadAsync<(int? RoleId, int? UserId)>()).ToList();
        var restricted = await LoadRestrictedTypeNamesAsync(connection, tenantId, cancellationToken);

        var driverIds = assignments.Where(a => a.DriverId.HasValue).Select(a => a.DriverId!.Value).ToList();
        var branchIds = assignments.Where(a => a.BranchId.HasValue).Select(a => a.BranchId!.Value).ToList();
        var departmentIds = assignments.Where(a => a.DepartmentId.HasValue).Select(a => a.DepartmentId!.Value).ToList();
        var vehicleIds = assignments.Where(a => a.VehicleId.HasValue).Select(a => a.VehicleId!.Value).ToList();
        var appliesTo = row.AppliesToAll ? GeofenceAppliesTo.All
            : driverIds.Count > 0 ? GeofenceAppliesTo.Drivers
            : branchIds.Count + departmentIds.Count > 0 ? GeofenceAppliesTo.Groups
            : GeofenceAppliesTo.Vehicles;

        return new GeofenceDetailDto
        {
            Id = row.Id,
            Name = row.Name,
            Type = row.Type,
            IsRestricted = IsRestricted(row.Type, restricted),
            GroupId = row.GroupId,
            GroupName = row.GroupName,
            Address = row.Address,
            Description = row.Description,
            Color = row.Color ?? "#0f766e",
            IsActive = row.IsActive,
            AreaType = row.AreaType,
            CenterLat = row.CenterLat,
            CenterLng = row.CenterLng,
            RadiusMeters = row.RadiusMeters,
            GeoJson = row.GeoJson,
            CorridorWidthMeters = row.CorridorWidthMeters,
            BufferMeters = row.BufferMeters,
            AreaSquareMeters = row.AreaSquareMeters,
            PerimeterMeters = row.PerimeterMeters,
            AlertOnEnter = row.AlertOnEnter,
            AlertOnExit = row.AlertOnExit,
            DwellMinutes = row.DwellMinutes,
            SpeedLimitKmh = row.SpeedLimitKmh,
            IdleMinutes = row.IdleMinutes,
            NotArrivedBy = FormatTime(row.NotArrivedBy),
            EngineBlockOnExit = row.EngineBlockOnExit,
            ScheduleDays = row.ScheduleDays,
            ScheduleStart = FormatTime(row.ScheduleStart),
            ScheduleEnd = FormatTime(row.ScheduleEnd),
            AfterHoursExitCritical = row.AfterHoursExitCritical,
            NotifyChannels = GeofenceNotifyChannels.Parse(row.NotifyChannels),
            CooldownMinutes = row.CooldownMinutes,
            AppliesTo = appliesTo,
            VehicleIds = vehicleIds,
            BranchIds = branchIds,
            DepartmentIds = departmentIds,
            DriverIds = driverIds,
            RecipientRoleIds = recipients.Where(r => r.RoleId.HasValue).Select(r => r.RoleId!.Value).ToList(),
            RecipientUserIds = recipients.Where(r => r.UserId.HasValue).Select(r => r.UserId!.Value).ToList(),
            VehiclesCovered = row.VehiclesCovered,
            RuleCount = row.RuleCount
        };
    }

    public async Task<GeofenceSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        var zone = await ZoneAsync(tenantId, cancellationToken);
        var localToday = LocalToday(zone);
        var todayStartUtc = TenantTimeZoneHelper.LocalDayStartUtc(localToday, zone);

        using var connection = dbFactory.CreateConnection();
        var restricted = await LoadRestrictedTypeNamesAsync(connection, tenantId, cancellationToken);
        var categories = await connection.QueryAsync<(int Id, string? Category)>(new CommandDefinition(
            "SELECT Id, Category FROM Geofences WHERE TenantId = @TenantId AND IsDeleted = 0",
            new { TenantId = tenantId },
            cancellationToken: cancellationToken));
        var restrictedIds = categories.Where(c => IsRestricted(c.Category, restricted)).Select(c => c.Id).ToArray();

        var s = await connection.QuerySingleAsync<SummaryRow>(new CommandDefinition(
            """
            SELECT
              (SELECT COUNT(1) FROM Geofences WHERE TenantId = @TenantId AND IsDeleted = 0) AS Total,
              (SELECT COUNT(1) FROM Geofences WHERE TenantId = @TenantId AND IsDeleted = 0 AND IsActive = 1) AS Active,
              (SELECT COUNT(DISTINCT gv.VehicleId) FROM GeofenceVisits gv
               INNER JOIN Geofences g ON g.Id = gv.GeofenceId AND g.TenantId = @TenantId AND g.IsDeleted = 0 AND g.IsActive = 1
               WHERE gv.TenantId = @TenantId AND gv.ExitedUtc IS NULL) AS VehiclesInsideNow,
              (SELECT COUNT(DISTINCT cv.Id) FROM Vehicles cv
               WHERE cv.TenantId = @TenantId AND cv.IsDeleted = 0
                 AND EXISTS (SELECT 1 FROM Geofences g
                             WHERE g.TenantId = @TenantId AND g.IsDeleted = 0 AND g.IsActive = 1
                               AND (g.AppliesToAll = 1 OR EXISTS (
                                    SELECT 1 FROM GeofenceAssignments ca
                                    WHERE ca.GeofenceId = g.Id AND ca.IsDeleted = 0
                                      AND (ca.VehicleId = cv.Id
                                           OR (ca.BranchId IS NOT NULL AND ca.BranchId = cv.BranchId)
                                           OR (ca.DepartmentId IS NOT NULL AND ca.DepartmentId = cv.DepartmentId)))))) AS VehiclesAssigned,
              (SELECT COUNT(1) FROM GeofenceVisits WHERE TenantId = @TenantId AND EnteredUtc >= @TodayStartUtc) AS EntriesToday,
              (SELECT COUNT(1) FROM GeofenceVisits WHERE TenantId = @TenantId AND ExitedUtc >= @TodayStartUtc) AS ExitsToday,
              (SELECT COUNT(1) FROM GeofenceVisits
               WHERE TenantId = @TenantId AND EnteredUtc >= @TodayStartUtc AND GeofenceId IN @RestrictedIds) AS RestrictedEntriesToday,
              (SELECT COUNT(1) FROM GpsAlertEvents e INNER JOIN Geofences g ON g.Id = e.GeofenceId AND g.TenantId = @TenantId
               WHERE e.IsDeleted = 0 AND e.Timestamp >= @TodayStartUtc AND e.EventType = @Dwell) AS OverstaysToday,
              (SELECT COUNT(1) FROM GpsAlertEvents e INNER JOIN Geofences g ON g.Id = e.GeofenceId AND g.TenantId = @TenantId
               WHERE e.IsDeleted = 0 AND e.Timestamp >= @TodayStartUtc AND e.EventType = @Overspeed) AS OverspeedsToday,
              (SELECT COUNT(1) FROM GpsAlertEvents e INNER JOIN Geofences g ON g.Id = e.GeofenceId AND g.TenantId = @TenantId
               WHERE e.IsDeleted = 0 AND e.Timestamp >= @TodayStartUtc AND e.EventType = @Idle) AS IdleAlertsToday,
              (SELECT COUNT(1) FROM GpsAlertEvents e INNER JOIN Geofences g ON g.Id = e.GeofenceId AND g.TenantId = @TenantId
               WHERE e.IsDeleted = 0 AND e.Timestamp >= @TodayStartUtc AND e.EventType = @AfterHours) AS AfterHoursExitsToday,
              (SELECT COUNT(1) FROM GeofenceNotArrivedLog WHERE TenantId = @TenantId AND LocalDate = @LocalToday) AS NotArrivedToday,
              (SELECT AVG(CAST(DATEDIFF(SECOND, EnteredUtc, ExitedUtc) AS FLOAT)) / 60.0 FROM GeofenceVisits
               WHERE TenantId = @TenantId AND ExitedUtc >= @SevenDaysAgoUtc) AS AvgMinutesOnSiteLast7Days
            """,
            new
            {
                TenantId = tenantId,
                TodayStartUtc = todayStartUtc,
                LocalToday = localToday.ToDateTime(TimeOnly.MinValue),
                SevenDaysAgoUtc = DateTime.UtcNow.AddDays(-7),
                RestrictedIds = restrictedIds.Length == 0 ? new[] { -1 } : restrictedIds,
                Dwell = GeofenceRuleEvents.Dwell,
                Overspeed = GeofenceRuleEvents.Overspeed,
                Idle = GeofenceRuleEvents.Idle,
                AfterHours = GeofenceRuleEvents.AfterHoursExit
            },
            cancellationToken: cancellationToken));

        return new GeofenceSummaryDto
        {
            Total = s.Total,
            Active = s.Active,
            VehiclesInsideNow = s.VehiclesInsideNow,
            VehiclesAssigned = s.VehiclesAssigned,
            EntriesToday = s.EntriesToday,
            ExitsToday = s.ExitsToday,
            RestrictedEntriesToday = s.RestrictedEntriesToday,
            OverstaysToday = s.OverstaysToday,
            OverspeedsToday = s.OverspeedsToday,
            IdleAlertsToday = s.IdleAlertsToday,
            AfterHoursExitsToday = s.AfterHoursExitsToday,
            NotArrivedToday = s.NotArrivedToday,
            ViolationsToday = s.RestrictedEntriesToday + s.OverstaysToday + s.OverspeedsToday + s.IdleAlertsToday
                              + s.AfterHoursExitsToday + s.NotArrivedToday,
            AvgMinutesOnSiteLast7Days = s.AvgMinutesOnSiteLast7Days is { } avg ? Math.Round(avg, 1) : null
        };
    }

    public async Task<IReadOnlyList<GeofenceInsideVehicleDto>?> GetInsideAsync(int id, CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        using var connection = dbFactory.CreateConnection();
        var dwell = await connection.QueryFirstOrDefaultAsync<(int Id, int? DwellMinutes)>(new CommandDefinition(
            "SELECT Id, DwellMinutes FROM Geofences WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
            new { Id = id, TenantId = tenantId },
            cancellationToken: cancellationToken));
        if (dwell.Id == 0)
            return null;

        var rows = await connection.QueryAsync<(long VisitId, int VehicleId, string? VehicleLabel, string? DriverName, DateTime EnteredUtc)>(
            new CommandDefinition(
                $"""
                SELECT gv.Id, gv.VehicleId, {VehicleLabelSql} AS VehicleLabel, d.FullName AS DriverName, gv.EnteredUtc
                FROM GeofenceVisits gv
                INNER JOIN Vehicles v ON v.Id = gv.VehicleId AND v.TenantId = @TenantId
                LEFT JOIN Drivers d ON d.Id = gv.DriverId AND d.TenantId = @TenantId
                WHERE gv.GeofenceId = @Id AND gv.TenantId = @TenantId AND gv.ExitedUtc IS NULL
                ORDER BY gv.EnteredUtc
                """,
                new { Id = id, TenantId = tenantId },
                cancellationToken: cancellationToken));

        var now = DateTime.UtcNow;
        return rows.Select(r =>
        {
            var minutes = (int)Math.Max(0, (now - r.EnteredUtc).TotalMinutes);
            return new GeofenceInsideVehicleDto
            {
                VisitId = r.VisitId,
                VehicleId = r.VehicleId,
                VehicleLabel = string.IsNullOrWhiteSpace(r.VehicleLabel) ? $"Vehicle #{r.VehicleId}" : r.VehicleLabel,
                DriverName = r.DriverName,
                EnteredUtc = r.EnteredUtc,
                MinutesInside = minutes,
                OverDwell = dwell.DwellMinutes is > 0 && minutes >= dwell.DwellMinutes
            };
        }).ToList();
    }

    public async Task<IReadOnlyList<GeofenceActivityItemDto>?> GetActivityAsync(
        int id, DateOnly? day, CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        var zone = await ZoneAsync(tenantId, cancellationToken);
        var localDay = day ?? LocalToday(zone);
        var (startUtc, endUtc) = TenantTimeZoneHelper.LocalDayRangeUtc(localDay, localDay, zone);

        using var connection = dbFactory.CreateConnection();
        var exists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM Geofences WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
            new { Id = id, TenantId = tenantId },
            cancellationToken: cancellationToken));
        if (exists == 0)
            return null;

        using var multi = await connection.QueryMultipleAsync(new CommandDefinition(
            $"""
            SELECT gv.VehicleId, {VehicleLabelSql} AS VehicleLabel, d.FullName AS DriverName, gv.EnteredUtc, gv.ExitedUtc
            FROM GeofenceVisits gv
            INNER JOIN Vehicles v ON v.Id = gv.VehicleId AND v.TenantId = @TenantId
            LEFT JOIN Drivers d ON d.Id = gv.DriverId AND d.TenantId = @TenantId
            WHERE gv.GeofenceId = @Id AND gv.TenantId = @TenantId
              AND ((gv.EnteredUtc >= @StartUtc AND gv.EnteredUtc < @EndUtc)
                   OR (gv.ExitedUtc >= @StartUtc AND gv.ExitedUtc < @EndUtc));

            SELECT TOP 200 e.VehicleId, {VehicleLabelSql} AS VehicleLabel, d.FullName AS DriverName,
                   e.EventType, e.Message, e.Timestamp
            FROM GpsAlertEvents e
            INNER JOIN Geofences g ON g.Id = e.GeofenceId AND g.TenantId = @TenantId
            INNER JOIN Vehicles v ON v.Id = e.VehicleId AND v.TenantId = @TenantId
            LEFT JOIN Drivers d ON d.Id = e.DriverId AND d.TenantId = @TenantId
            WHERE e.GeofenceId = @Id AND e.IsDeleted = 0 AND e.EventType IN @RuleTypes
              AND e.Timestamp >= @StartUtc AND e.Timestamp < @EndUtc
            ORDER BY e.Timestamp DESC;

            SELECT AlertedUtc FROM GeofenceNotArrivedLog
            WHERE GeofenceId = @Id AND TenantId = @TenantId AND LocalDate = @LocalDay;
            """,
            new
            {
                Id = id,
                TenantId = tenantId,
                StartUtc = startUtc,
                EndUtc = endUtc,
                LocalDay = localDay.ToDateTime(TimeOnly.MinValue),
                RuleTypes = ViolationEventTypes
            },
            cancellationToken: cancellationToken));

        var visits = await multi.ReadAsync<(int VehicleId, string? VehicleLabel, string? DriverName, DateTime EnteredUtc, DateTime? ExitedUtc)>();
        var alerts = await multi.ReadAsync<(int VehicleId, string? VehicleLabel, string? DriverName, string EventType, string? Message, DateTime Timestamp)>();
        var notArrived = await multi.ReadAsync<DateTime>();

        var items = new List<GeofenceActivityItemDto>();
        foreach (var v in visits)
        {
            var label = string.IsNullOrWhiteSpace(v.VehicleLabel) ? $"Vehicle #{v.VehicleId}" : v.VehicleLabel;
            if (v.EnteredUtc >= startUtc && v.EnteredUtc < endUtc)
                items.Add(new GeofenceActivityItemDto
                {
                    AtUtc = v.EnteredUtc, Kind = GeofenceActivityKind.Entered, VehicleId = v.VehicleId,
                    VehicleLabel = label, DriverName = v.DriverName
                });
            if (v.ExitedUtc is { } exited && exited >= startUtc && exited < endUtc)
                items.Add(new GeofenceActivityItemDto
                {
                    AtUtc = exited, Kind = GeofenceActivityKind.Exited, VehicleId = v.VehicleId,
                    VehicleLabel = label, DriverName = v.DriverName
                });
        }

        foreach (var a in alerts)
        {
            items.Add(new GeofenceActivityItemDto
            {
                AtUtc = a.Timestamp,
                Kind = a.EventType switch
                {
                    GeofenceRuleEvents.Dwell => GeofenceActivityKind.Dwell,
                    GeofenceRuleEvents.Overspeed => GeofenceActivityKind.Overspeed,
                    GeofenceRuleEvents.Idle => GeofenceActivityKind.Idle,
                    _ => GeofenceActivityKind.AfterHoursExit
                },
                VehicleId = a.VehicleId,
                VehicleLabel = string.IsNullOrWhiteSpace(a.VehicleLabel) ? $"Vehicle #{a.VehicleId}" : a.VehicleLabel,
                DriverName = a.DriverName,
                Message = a.Message
            });
        }

        items.AddRange(notArrived.Select(at => new GeofenceActivityItemDto
        {
            AtUtc = at, Kind = GeofenceActivityKind.NotArrived, Message = "No assigned vehicle arrived by the deadline."
        }));

        return items.OrderByDescending(i => i.AtUtc).Take(300).ToList();
    }

    public async Task<GeofenceVisitsPageDto> GetVisitsAsync(
        GeofenceVisitFilter filter, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        if (filter.From is null || filter.To is null)
            return new GeofenceVisitsPageDto { Items = [], Total = 0, Page = page, PageSize = pageSize };

        var tenantId = TenantId;
        var zone = await ZoneAsync(tenantId, cancellationToken);
        var (startUtc, endUtc) = TenantTimeZoneHelper.LocalDayRangeUtc(filter.From.Value, filter.To.Value, zone);
        var args = VisitArgs(tenantId, filter, startUtc, endUtc);
        args.Add("Offset", (page - 1) * pageSize);
        args.Add("PageSize", pageSize);

        using var connection = dbFactory.CreateConnection();
        using var multi = await connection.QueryMultipleAsync(new CommandDefinition(
            $"""
            SELECT COUNT(1)
            FROM GeofenceVisits gv
            INNER JOIN Geofences g ON g.Id = gv.GeofenceId AND g.TenantId = @TenantId AND g.IsDeleted = 0
            CROSS APPLY (SELECT {VisitStatusSql} AS Status) st
            WHERE {VisitWhereSql};

            SELECT gv.Id, gv.GeofenceId, g.Name AS GeofenceName, gv.VehicleId, {VehicleLabelSql} AS VehicleLabel,
                   gv.DriverId, d.FullName AS DriverName, gv.EnteredUtc, gv.ExitedUtc,
                   DATEDIFF(MINUTE, gv.EnteredUtc, COALESCE(gv.ExitedUtc, @Now)) AS DurationMinutes,
                   gv.IdleSeconds / 60 AS IdleMinutes, gv.MaxSpeedKmh, st.Status
            FROM GeofenceVisits gv
            INNER JOIN Geofences g ON g.Id = gv.GeofenceId AND g.TenantId = @TenantId AND g.IsDeleted = 0
            INNER JOIN Vehicles v ON v.Id = gv.VehicleId AND v.TenantId = @TenantId
            LEFT JOIN Drivers d ON d.Id = gv.DriverId AND d.TenantId = @TenantId
            CROSS APPLY (SELECT {VisitStatusSql} AS Status) st
            WHERE {VisitWhereSql}
            ORDER BY gv.EnteredUtc DESC, gv.Id DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """,
            args,
            cancellationToken: cancellationToken));

        var total = await multi.ReadSingleAsync<int>();
        var items = (await multi.ReadAsync<GeofenceVisitDto>()).ToList();
        return new GeofenceVisitsPageDto { Items = items, Total = total, Page = page, PageSize = pageSize };
    }

    public async Task<GeofenceVisitsSummaryDto> GetVisitsSummaryAsync(GeofenceVisitFilter filter, CancellationToken cancellationToken = default)
    {
        var emptyHours = Enumerable.Repeat(0, 24).ToList();
        var noBreachHours = Enumerable.Repeat(false, 24).ToList();
        if (filter.From is null || filter.To is null)
            return new GeofenceVisitsSummaryDto { OccupancyToday = emptyHours, DwellBreachHoursToday = noBreachHours };

        var tenantId = TenantId;
        var zone = await ZoneAsync(tenantId, cancellationToken);
        var (startUtc, endUtc) = TenantTimeZoneHelper.LocalDayRangeUtc(filter.From.Value, filter.To.Value, zone);
        var noStatus = filter with { Status = null };

        using var connection = dbFactory.CreateConnection();
        var totals = await connection.QuerySingleAsync<VisitTotalsRow>(new CommandDefinition(
            $"""
            SELECT COUNT(1) AS Visits, COUNT(DISTINCT gv.VehicleId) AS Vehicles,
                   COALESCE(SUM(DATEDIFF(MINUTE, gv.EnteredUtc, COALESCE(gv.ExitedUtc, @Now))), 0) AS TotalMinutes,
                   AVG(CASE WHEN gv.ExitedUtc IS NOT NULL
                            THEN CAST(DATEDIFF(SECOND, gv.EnteredUtc, gv.ExitedUtc) AS FLOAT) / 60.0 END) AS AvgMinutes,
                   COALESCE(SUM(gv.IdleSeconds), 0) / 60 AS IdleMinutes,
                   COALESCE(SUM(CASE WHEN gv.DwellBreached = 1 THEN 1 ELSE 0 END), 0) AS Overstays,
                   COALESCE(SUM(CASE WHEN gv.SpeedBreached = 1 THEN 1 ELSE 0 END), 0) AS Overspeeds,
                   COALESCE(SUM(CASE WHEN gv.AfterHoursExit = 1 THEN 1 ELSE 0 END), 0) AS AfterHoursExits,
                   COALESCE(SUM(CASE WHEN gv.IdleBreached = 1 THEN 1 ELSE 0 END), 0) AS IdleBreaches
            FROM GeofenceVisits gv
            INNER JOIN Geofences g ON g.Id = gv.GeofenceId AND g.TenantId = @TenantId AND g.IsDeleted = 0
            CROSS APPLY (SELECT {VisitStatusSql} AS Status) st
            WHERE {VisitWhereSql}
            """,
            VisitArgs(tenantId, noStatus, startUtc, endUtc),
            cancellationToken: cancellationToken));

        int? dwellTarget = null;
        if (filter.GeofenceId is int geofenceId)
        {
            dwellTarget = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
                "SELECT DwellMinutes FROM Geofences WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
                new { Id = geofenceId, TenantId = tenantId },
                cancellationToken: cancellationToken));
        }

        // Today's hourly occupancy, for the same geofence / vehicle / driver filters.
        var today = LocalToday(zone);
        var (todayStartUtc, todayEndUtc) = TenantTimeZoneHelper.LocalDayRangeUtc(today, today, zone);
        var todayVisits = await connection.QueryAsync<(int VehicleId, DateTime EnteredUtc, DateTime? ExitedUtc, bool DwellBreached, int? DwellMinutes)>(
            new CommandDefinition(
                $"""
                SELECT gv.VehicleId, gv.EnteredUtc, gv.ExitedUtc, gv.DwellBreached, g.DwellMinutes
                FROM GeofenceVisits gv
                INNER JOIN Geofences g ON g.Id = gv.GeofenceId AND g.TenantId = @TenantId AND g.IsDeleted = 0
                CROSS APPLY (SELECT {VisitStatusSql} AS Status) st
                WHERE {VisitWhereSql}
                """,
                VisitArgs(tenantId, noStatus, todayStartUtc, todayEndUtc),
                cancellationToken: cancellationToken));

        var (occupancy, breachHours) = HourlyOccupancy(todayVisits.ToList(), todayStartUtc, DateTime.UtcNow);
        return new GeofenceVisitsSummaryDto
        {
            Visits = totals.Visits,
            Vehicles = totals.Vehicles,
            TotalMinutes = totals.TotalMinutes,
            AvgMinutes = totals.AvgMinutes is { } avg ? Math.Round(avg, 1) : null,
            IdleMinutes = totals.IdleMinutes,
            Overstays = totals.Overstays,
            Overspeeds = totals.Overspeeds,
            AfterHoursExits = totals.AfterHoursExits,
            IdleBreaches = totals.IdleBreaches,
            Breaches = totals.Overstays + totals.Overspeeds + totals.AfterHoursExits + totals.IdleBreaches,
            DwellTargetMinutes = dwellTarget,
            OccupancyToday = occupancy,
            DwellBreachHoursToday = breachHours
        };
    }

    /// <summary>Visits read for one grouped report at most; above it the groups cover the newest visits only.</summary>
    public const int MaxGroupedVisits = 50_000;

    public async Task<GeofenceVisitGroupsDto> GetVisitGroupsAsync(
        GeofenceVisitFilter filter, string groupBy, CancellationToken cancellationToken = default)
    {
        if (filter.From is null || filter.To is null)
            return new GeofenceVisitGroupsDto { GroupBy = groupBy };

        var tenantId = TenantId;
        var zone = await ZoneAsync(tenantId, cancellationToken);
        var (startUtc, endUtc) = TenantTimeZoneHelper.LocalDayRangeUtc(filter.From.Value, filter.To.Value, zone);
        var args = VisitArgs(tenantId, filter, startUtc, endUtc);
        args.Add("Cap", MaxGroupedVisits + 1);

        using var connection = dbFactory.CreateConnection();
        var rows = (await connection.QueryAsync<GroupableVisit>(new CommandDefinition(
            $"""
            SELECT TOP (@Cap) gv.VehicleId, {VehicleLabelSql} AS VehicleLabel, gv.DriverId, d.FullName AS DriverName,
                   gv.GeofenceId, g.Name AS GeofenceName, gv.EnteredUtc, gv.ExitedUtc, gv.IdleSeconds,
                   CAST(gv.DwellBreached AS INT) + CAST(gv.SpeedBreached AS INT)
                     + CAST(gv.AfterHoursExit AS INT) + CAST(gv.IdleBreached AS INT) AS Breaches
            FROM GeofenceVisits gv
            INNER JOIN Geofences g ON g.Id = gv.GeofenceId AND g.TenantId = @TenantId AND g.IsDeleted = 0
            INNER JOIN Vehicles v ON v.Id = gv.VehicleId AND v.TenantId = @TenantId
            LEFT JOIN Drivers d ON d.Id = gv.DriverId AND d.TenantId = @TenantId
            CROSS APPLY (SELECT {VisitStatusSql} AS Status) st
            WHERE {VisitWhereSql}
            ORDER BY gv.EnteredUtc DESC, gv.Id DESC
            """,
            args,
            cancellationToken: cancellationToken))).ToList();

        var capped = rows.Count > MaxGroupedVisits;
        if (capped)
            rows.RemoveAt(rows.Count - 1);

        return new GeofenceVisitGroupsDto
        {
            GroupBy = groupBy,
            Items = GroupVisits(rows, groupBy, zone, startUtc, DateTime.UtcNow),
            Capped = capped,
            VisitsRead = rows.Count
        };
    }

    /// <summary>One visit as the grouped report reads it.</summary>
    public sealed class GroupableVisit
    {
        public int VehicleId { get; init; }
        public string? VehicleLabel { get; init; }
        public int? DriverId { get; init; }
        public string? DriverName { get; init; }
        public int GeofenceId { get; init; }
        public string GeofenceName { get; init; } = string.Empty;
        public DateTime EnteredUtc { get; init; }
        public DateTime? ExitedUtc { get; init; }
        public int IdleSeconds { get; init; }
        public int Breaches { get; init; }
    }

    /// <summary>
    /// Groups visits by vehicle, driver, geofence or the company's local day of entry. Minutes are counted the way the
    /// report KPIs count them (SQL DATEDIFF; open visits up to <paramref name="nowUtc"/>), so the groups add up to the
    /// totals. A visit that started before the range is counted on the range's first day.
    /// </summary>
    public static IReadOnlyList<GeofenceVisitGroupDto> GroupVisits(
        IReadOnlyList<GroupableVisit> visits, string groupBy, TimeZoneInfo zone, DateTime rangeStartUtc, DateTime nowUtc)
    {
        string KeyOf(GroupableVisit v) => groupBy switch
        {
            GeofenceVisitGroupBy.Vehicle => v.VehicleId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            GeofenceVisitGroupBy.Driver => v.DriverId?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "none",
            GeofenceVisitGroupBy.Geofence => v.GeofenceId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            _ => TenantTimeZoneHelper.UtcToLocalDate(v.EnteredUtc < rangeStartUtc ? rangeStartUtc : v.EnteredUtc, zone)
                .ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)
        };

        string LabelOf(GroupableVisit v, string key) => groupBy switch
        {
            GeofenceVisitGroupBy.Vehicle => string.IsNullOrWhiteSpace(v.VehicleLabel) ? $"Vehicle #{v.VehicleId}" : v.VehicleLabel,
            GeofenceVisitGroupBy.Driver => string.IsNullOrWhiteSpace(v.DriverName) ? "No driver" : v.DriverName,
            GeofenceVisitGroupBy.Geofence => v.GeofenceName,
            _ => key
        };

        var groups = visits
            .GroupBy(KeyOf)
            .Select(g =>
            {
                var completed = g.Where(v => v.ExitedUtc is not null).ToList();
                return new GeofenceVisitGroupDto
                {
                    Key = g.Key,
                    Label = LabelOf(g.First(), g.Key),
                    Visits = g.Count(),
                    Vehicles = g.Select(v => v.VehicleId).Distinct().Count(),
                    InsideNow = g.Count(v => v.ExitedUtc is null),
                    TotalMinutes = (int)Math.Max(0, g.Sum(v => DateDiff(v.EnteredUtc, v.ExitedUtc ?? nowUtc, TimeSpan.TicksPerMinute))),
                    AvgMinutes = completed.Count == 0
                        ? null
                        : Math.Round(completed.Average(v => DateDiff(v.EnteredUtc, v.ExitedUtc!.Value, TimeSpan.TicksPerSecond) / 60.0), 1),
                    IdleMinutes = g.Sum(v => v.IdleSeconds) / 60,
                    Breaches = g.Sum(v => v.Breaches)
                };
            });

        return groupBy == GeofenceVisitGroupBy.Day
            ? groups.OrderBy(g => g.Key, StringComparer.Ordinal).ToList()
            : groups.OrderByDescending(g => g.TotalMinutes).ThenBy(g => g.Label, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>SQL Server DATEDIFF: the number of unit boundaries crossed between two instants.</summary>
    private static long DateDiff(DateTime from, DateTime to, long unitTicks)
        => to.Ticks / unitTicks - from.Ticks / unitTicks;

    /// <summary>Vehicles inside during each of the 24 local hours from <paramref name="dayStartUtc"/>, and dwell-breach hours.</summary>
    public static (IReadOnlyList<int> Occupancy, IReadOnlyList<bool> BreachHours) HourlyOccupancy(
        IReadOnlyList<(int VehicleId, DateTime EnteredUtc, DateTime? ExitedUtc, bool DwellBreached, int? DwellMinutes)> visits,
        DateTime dayStartUtc,
        DateTime nowUtc)
    {
        var occupancy = new int[24];
        var breach = new bool[24];
        for (var h = 0; h < 24; h++)
        {
            var from = dayStartUtc.AddHours(h);
            var to = from.AddHours(1);
            occupancy[h] = visits
                .Where(v => v.EnteredUtc < to && (v.ExitedUtc ?? nowUtc) > from)
                .Select(v => v.VehicleId)
                .Distinct()
                .Count();
        }

        foreach (var v in visits.Where(v => v.DwellBreached && v.DwellMinutes is > 0))
        {
            var at = v.EnteredUtc.AddMinutes(v.DwellMinutes!.Value);
            var hour = (int)Math.Floor((at - dayStartUtc).TotalHours);
            if (hour is >= 0 and < 24)
                breach[hour] = true;
        }

        return (occupancy, breach);
    }

    public async Task<TestGeofenceShapeResultDto> TestShapeAsync(TestGeofenceShapeDto shape, CancellationToken cancellationToken = default)
    {
        const int days = 7;
        var box = GpsGeoHelper.TryGetBoundingBox(
            shape.AreaType, shape.CenterLat, shape.CenterLng, shape.RadiusMeters, shape.GeoJson, shape.CorridorWidthMeters,
            marginMeters: shape.BufferMeters + 200);
        if (box is not { } bounds)
            return new TestGeofenceShapeResultDto { Days = days };

        var tenantId = TenantId;
        var vehicleFilter = shape.VehicleIds is { Length: > 0 } ? "AND p.VehicleId IN @VehicleIds" : string.Empty;
        using var connection = dbFactory.CreateConnection();
        var points = (await connection.QueryAsync<(int VehicleId, double Latitude, double Longitude, DateTime RecordedAt)>(
            new CommandDefinition(
                $"""
                SELECT TOP (@Cap) p.VehicleId, p.Latitude, p.Longitude, p.RecordedAt
                FROM GpsPositions p
                INNER JOIN Vehicles v ON v.Id = p.VehicleId AND v.TenantId = @TenantId AND v.IsDeleted = 0
                WHERE p.RecordedAt >= @SinceUtc
                  AND p.Latitude BETWEEN @MinLat AND @MaxLat AND p.Longitude BETWEEN @MinLng AND @MaxLng
                  {vehicleFilter}
                ORDER BY p.VehicleId, p.RecordedAt
                """,
                new
                {
                    Cap = TestShapePositionCap,
                    TenantId = tenantId,
                    SinceUtc = DateTime.UtcNow.AddDays(-days),
                    bounds.MinLat, bounds.MaxLat, bounds.MinLng, bounds.MaxLng,
                    shape.VehicleIds
                },
                commandTimeout: 60,
                cancellationToken: cancellationToken))).ToList();

        var (entries, vehicles) = CountEntries(points, shape);
        return new TestGeofenceShapeResultDto
        {
            Entries = entries,
            Vehicles = vehicles,
            Days = days,
            Capped = points.Count >= TestShapePositionCap
        };
    }

    /// <summary>
    /// Outside → inside transitions per vehicle. Only points near the shape are loaded, so a gap of more than
    /// 10 minutes between them means the vehicle was away (outside) in between.
    /// </summary>
    public static (int Entries, int Vehicles) CountEntries(
        IReadOnlyList<(int VehicleId, double Latitude, double Longitude, DateTime RecordedAt)> points,
        TestGeofenceShapeDto shape)
    {
        var entries = 0;
        var vehiclesWithEntry = new HashSet<int>();
        foreach (var track in points.GroupBy(p => p.VehicleId))
        {
            var wasInside = false;
            DateTime? previous = null;
            foreach (var p in track.OrderBy(p => p.RecordedAt))
            {
                if (previous is { } prev && p.RecordedAt - prev > TimeSpan.FromMinutes(10))
                    wasInside = false;

                var inside = GpsGeoHelper.IsInsideGeofence(
                    p.Latitude, p.Longitude, shape.AreaType, shape.CenterLat, shape.CenterLng, shape.RadiusMeters,
                    shape.GeoJson, shape.CorridorWidthMeters ?? 0);
                if (inside && !wasInside)
                {
                    entries++;
                    vehiclesWithEntry.Add(track.Key);
                }

                wasInside = inside;
                previous = p.RecordedAt;
            }
        }

        return (entries, vehiclesWithEntry.Count);
    }

    public async Task<IReadOnlyList<GeofenceGroupDto>> GetGroupsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return (await connection.QueryAsync<GeofenceGroupDto>(new CommandDefinition(
            """
            SELECT gg.Id, gg.Name, gg.SortOrder,
                   (SELECT COUNT(1) FROM Geofences g
                    WHERE g.GroupId = gg.Id AND g.TenantId = @TenantId AND g.IsDeleted = 0) AS GeofenceCount
            FROM GeofenceGroups gg
            WHERE gg.TenantId = @TenantId AND gg.IsDeleted = 0
            ORDER BY gg.SortOrder, gg.Name
            """,
            new { TenantId },
            cancellationToken: cancellationToken))).ToList();
    }

    public async Task<IReadOnlyList<GeofenceTypeDto>> GetTypesAsync(CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var custom = await connection.QueryAsync<GeofenceTypeDto>(new CommandDefinition(
            """
            SELECT Id, Name, Color, IsRestricted, CAST(0 AS BIT) AS IsBuiltIn
            FROM GeofenceTypes
            WHERE TenantId = @TenantId AND IsDeleted = 0
            ORDER BY Name
            """,
            new { TenantId },
            cancellationToken: cancellationToken));

        return GeofenceTypeCatalog.BuiltIn
            .Select(t => new GeofenceTypeDto { Name = t.Name, Color = t.Color, IsRestricted = t.IsRestricted, IsBuiltIn = true })
            .Concat(custom)
            .ToList();
    }

    public async Task<GeofenceRecipientOptionsDto> GetRecipientOptionsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        using var multi = await connection.QueryMultipleAsync(new CommandDefinition(
            """
            SELECT DISTINCT r.Id, r.Name
            FROM Roles r
            INNER JOIN UserRoles ur ON ur.RoleId = r.Id
            INNER JOIN Users u ON u.Id = ur.UserId AND u.TenantId = @TenantId AND u.IsActive = 1 AND u.IsDeleted = 0
            WHERE r.IsActive = 1
            ORDER BY r.Name;

            SELECT Id, FullName AS Name
            FROM Users
            WHERE TenantId = @TenantId AND IsActive = 1 AND IsDeleted = 0
            ORDER BY FullName;
            """,
            new { TenantId },
            cancellationToken: cancellationToken));

        return new GeofenceRecipientOptionsDto
        {
            Roles = (await multi.ReadAsync<GeofenceRecipientOptionDto>()).ToList(),
            Users = (await multi.ReadAsync<GeofenceRecipientOptionDto>()).ToList()
        };
    }

    // ── helpers ────────────────────────────────────────────────────────────────

    private const string VisitStatusSql = """
        CASE WHEN gv.ExitedUtc IS NULL THEN 'inside'
             WHEN gv.SpeedBreached = 1 THEN 'overspeed'
             WHEN gv.DwellBreached = 1 THEN 'overstay'
             WHEN gv.AfterHoursExit = 1 THEN 'after_hours'
             WHEN gv.IdleBreached = 1 THEN 'idle'
             ELSE 'completed' END
        """;

    /// <summary>Visits that overlap [StartUtc, EndUtc) and match the filters.</summary>
    private const string VisitWhereSql = """
        gv.TenantId = @TenantId
        AND gv.EnteredUtc < @EndUtc AND (gv.ExitedUtc IS NULL OR gv.ExitedUtc >= @StartUtc)
        AND (@GeofenceId IS NULL OR gv.GeofenceId = @GeofenceId)
        AND (@DriverId IS NULL OR gv.DriverId = @DriverId)
        AND (@AnyVehicle = 1 OR gv.VehicleId IN @VehicleIds)
        AND (@Status IS NULL OR st.Status = @Status)
        """;

    private static DynamicParameters VisitArgs(int tenantId, GeofenceVisitFilter filter, DateTime startUtc, DateTime endUtc)
    {
        int[] vehicleIds = filter.VehicleIds is { Count: > 0 } ids ? ids.ToArray() : [-1];
        var args = new DynamicParameters();
        args.Add("TenantId", tenantId);
        args.Add("StartUtc", startUtc);
        args.Add("EndUtc", endUtc);
        args.Add("Now", DateTime.UtcNow);
        args.Add("GeofenceId", filter.GeofenceId);
        args.Add("DriverId", filter.DriverId);
        args.Add("AnyVehicle", filter.VehicleIds is { Count: > 0 } ? 0 : 1);
        args.Add("VehicleIds", vehicleIds);
        args.Add("Status", filter.Status);
        return args;
    }

    private static async Task<HashSet<string>> LoadRestrictedTypeNamesAsync(
        IDbConnection connection, int tenantId, CancellationToken cancellationToken)
    {
        var names = await connection.QueryAsync<string>(new CommandDefinition(
            "SELECT Name FROM GeofenceTypes WHERE TenantId = @TenantId AND IsDeleted = 0 AND IsRestricted = 1",
            new { TenantId = tenantId },
            cancellationToken: cancellationToken));
        return new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsRestricted(string? type, HashSet<string> customRestricted)
        => GeofenceTypeCatalog.Find(type)?.IsRestricted == true
           || (!string.IsNullOrWhiteSpace(type) && customRestricted.Contains(type.Trim()));

    private static string? FormatTime(TimeSpan? value) => value?.ToString(@"hh\:mm");

    private sealed class GeofenceRow
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string AreaType { get; init; } = "circle";
        public double CenterLat { get; init; }
        public double CenterLng { get; init; }
        public double RadiusMeters { get; init; }
        public string? GeoJson { get; init; }
        public bool IsActive { get; init; }
        public string? Color { get; init; }
        public string? Type { get; init; }
        public int? GroupId { get; init; }
        public string? GroupName { get; init; }
        public string? Address { get; init; }
        public string? Description { get; init; }
        public int BufferMeters { get; init; }
        public int? CorridorWidthMeters { get; init; }
        public double? AreaSquareMeters { get; init; }
        public double? PerimeterMeters { get; init; }
        public bool AppliesToAll { get; init; }
        public int? DwellMinutes { get; init; }
        public decimal? SpeedLimitKmh { get; init; }
        public int? IdleMinutes { get; init; }
        public TimeSpan? NotArrivedBy { get; init; }
        public bool EngineBlockOnExit { get; init; }
        public byte? ScheduleDays { get; init; }
        public TimeSpan? ScheduleStart { get; init; }
        public TimeSpan? ScheduleEnd { get; init; }
        public bool AfterHoursExitCritical { get; init; }
        public string? NotifyChannels { get; init; }
        public int CooldownMinutes { get; init; } = 15;
        public bool AlertOnEnter { get; init; }
        public bool AlertOnExit { get; init; }
        public int InsideNow { get; init; }
        public int EntriesToday { get; init; }
        public int RuleAlertsToday { get; init; }
        public int VehiclesCovered { get; init; }
        public int DriversAssigned { get; init; }

        public int RuleCount =>
            (AlertOnEnter ? 1 : 0) + (AlertOnExit ? 1 : 0) + (DwellMinutes.HasValue ? 1 : 0)
            + (SpeedLimitKmh.HasValue ? 1 : 0) + (IdleMinutes.HasValue ? 1 : 0) + (NotArrivedBy.HasValue ? 1 : 0)
            + (EngineBlockOnExit ? 1 : 0);

        public GeofenceListItemDto ToListItem(bool isRestricted) => new()
        {
            Id = Id,
            Name = Name,
            AreaType = AreaType,
            CenterLat = CenterLat,
            CenterLng = CenterLng,
            RadiusMeters = RadiusMeters,
            GeoJson = GeoJson,
            IsActive = IsActive,
            Color = Color ?? "#0f766e",
            Type = Type,
            IsRestricted = isRestricted,
            GroupId = GroupId,
            GroupName = GroupName,
            Address = Address,
            Description = Description,
            BufferMeters = BufferMeters,
            CorridorWidthMeters = CorridorWidthMeters,
            AreaSquareMeters = AreaSquareMeters,
            PerimeterMeters = PerimeterMeters,
            AppliesToAll = AppliesToAll,
            VehiclesCovered = VehiclesCovered,
            DriversAssigned = DriversAssigned,
            InsideNow = InsideNow,
            EntriesToday = EntriesToday,
            ViolationsToday = RuleAlertsToday + (isRestricted ? EntriesToday : 0),
            RuleCount = RuleCount,
            AlertOnEnter = AlertOnEnter,
            AlertOnExit = AlertOnExit
        };
    }

    private sealed class SummaryRow
    {
        public int Total { get; init; }
        public int Active { get; init; }
        public int VehiclesInsideNow { get; init; }
        public int VehiclesAssigned { get; init; }
        public int EntriesToday { get; init; }
        public int ExitsToday { get; init; }
        public int RestrictedEntriesToday { get; init; }
        public int OverstaysToday { get; init; }
        public int OverspeedsToday { get; init; }
        public int IdleAlertsToday { get; init; }
        public int AfterHoursExitsToday { get; init; }
        public int NotArrivedToday { get; init; }
        public double? AvgMinutesOnSiteLast7Days { get; init; }
    }

    private sealed class VisitTotalsRow
    {
        public int Visits { get; init; }
        public int Vehicles { get; init; }
        public int TotalMinutes { get; init; }
        public double? AvgMinutes { get; init; }
        public int IdleMinutes { get; init; }
        public int Overstays { get; init; }
        public int Overspeeds { get; init; }
        public int AfterHoursExits { get; init; }
        public int IdleBreaches { get; init; }
    }
}
