using Dapper;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.AccidentRisk;
using SheikhTravelSystem.Application.Features.AccidentRisk.Services;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories;

public sealed class AccidentRiskRepository(
    IDbConnectionFactory dbFactory,
    ITenantContext tenantContext) : IAccidentRiskRepository
{
    private static readonly TimeSpan DefaultLookback = TimeSpan.FromDays(90);

    public async Task<ApiResponse<AccidentRiskDashboardDto>> GetDashboardAsync(
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var (from, to) = NormalizeRange(fromUtc, toUtc);

        var drivers = await LoadDriverScoresAsync(tenantId, from, to, cancellationToken);
        var routes = await LoadRouteScoresAsync(tenantId, from, to, cancellationToken);

        var dto = new AccidentRiskDashboardDto(
            from,
            to,
            drivers.Count(d => d.IsHighRisk),
            routes.Count(r => r.IsHighRisk),
            drivers.OrderByDescending(d => d.RiskScore).ToList(),
            routes.OrderByDescending(r => r.RiskScore).ToList());

        return ApiResponse<AccidentRiskDashboardDto>.SuccessResponse(dto);
    }

    public async Task<IReadOnlyList<DriverAccidentRiskDto>> GetHighRiskDriversAsync(
        int tenantId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default)
    {
        var all = await LoadDriverScoresAsync(tenantId, fromUtc, toUtc, cancellationToken);
        return all.Where(d => d.IsHighRisk).ToList();
    }

    public async Task<IReadOnlyList<RouteAccidentRiskDto>> GetHighRiskRoutesAsync(
        int tenantId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default)
    {
        var all = await LoadRouteScoresAsync(tenantId, fromUtc, toUtc, cancellationToken);
        return all.Where(r => r.IsHighRisk).ToList();
    }

    private static (DateTime From, DateTime To) NormalizeRange(DateTime? fromUtc, DateTime? toUtc)
    {
        var to = toUtc ?? DateTime.UtcNow;
        var from = fromUtc ?? to.Subtract(DefaultLookback);
        if (from > to)
            (from, to) = (to, from);
        return (from, to);
    }

    private async Task<IReadOnlyList<DriverAccidentRiskDto>> LoadDriverScoresAsync(
        int tenantId,
        DateTime from,
        DateTime to,
        CancellationToken ct)
    {
        using var connection = dbFactory.CreateConnection();

        var drivers = (await connection.QueryAsync<(int Id, string FullName)>(new CommandDefinition(
            """
            SELECT Id, FullName FROM Drivers
            WHERE TenantId = @TenantId AND IsDeleted = 0
            """,
            new { TenantId = tenantId },
            cancellationToken: ct))).ToList();

        if (drivers.Count == 0)
            return [];

        var accidents = await CountByDriverAsync(connection, tenantId, from, to, """
            SELECT DriverId, COUNT(*) AS Cnt FROM DriverViolations
            WHERE TenantId = @TenantId AND IsDeleted = 0
              AND OccurredAt BETWEEN @From AND @To
              AND (ViolationType LIKE N'%Accident%' OR ViolationType = N'Accident')
            GROUP BY DriverId
            """, ct);

        var speedGps = await CountByDriverAsync(connection, tenantId, from, to, """
            SELECT e.DriverId, COUNT(*) AS Cnt
            FROM GpsAlertEvents e
            INNER JOIN Vehicles v ON v.Id = e.VehicleId AND v.TenantId = @TenantId
            WHERE e.IsDeleted = 0 AND e.DriverId IS NOT NULL
              AND e.EventType IN (N'overspeed', N'speed_exceeded')
              AND e.Timestamp BETWEEN @From AND @To
            GROUP BY e.DriverId
            """, ct);

        var speedManual = await CountByDriverAsync(connection, tenantId, from, to, """
            SELECT DriverId, COUNT(*) AS Cnt FROM DriverViolations
            WHERE TenantId = @TenantId AND IsDeleted = 0
              AND OccurredAt BETWEEN @From AND @To
              AND ViolationType IN (N'Overspeed', N'overspeed', N'Speed')
            GROUP BY DriverId
            """, ct);

        var fatigue = await TryCountByDriverAsync(connection, tenantId, from, to, """
            SELECT DriverId, COUNT(*) AS Cnt FROM DriverFatigueEvents
            WHERE TenantId = @TenantId AND IsDeleted = 0
              AND CreatedAt BETWEEN @From AND @To
            GROUP BY DriverId
            """, ct);

        return drivers.Select(d =>
        {
            var speed = speedGps.GetValueOrDefault(d.Id) + speedManual.GetValueOrDefault(d.Id);
            return AccidentRiskCalculator.BuildDriver(
                d.Id,
                d.FullName,
                accidents.GetValueOrDefault(d.Id),
                speed,
                fatigue.GetValueOrDefault(d.Id));
        }).ToList();
    }

    private async Task<IReadOnlyList<RouteAccidentRiskDto>> LoadRouteScoresAsync(
        int tenantId,
        DateTime from,
        DateTime to,
        CancellationToken ct)
    {
        using var connection = dbFactory.CreateConnection();

        var routes = (await connection.QueryAsync<RouteRiskMetricRow>(new CommandDefinition(
            """
            SELECT r.Id AS RouteId, r.Name AS RouteName, r.Source, r.Destination,
                   r.Distance AS DistanceKm,
                   (SELECT COUNT(*) FROM Bookings b
                    WHERE b.RouteId = r.Id AND b.TenantId = @TenantId AND b.IsDeleted = 0
                      AND b.PickupTime BETWEEN @From AND @To) AS TripCount,
                   0 AS AccidentCount,
                   0 AS SpeedViolationCount
            FROM Routes r
            WHERE r.TenantId = @TenantId AND r.IsDeleted = 0 AND r.IsActive = 1
            """,
            new { TenantId = tenantId, From = from, To = to },
            cancellationToken: ct))).ToList();

        if (routes.Count == 0)
            return [];

        var accidents = await CountByRouteAsync(connection, tenantId, from, to, """
            SELECT b.RouteId, COUNT(*) AS Cnt
            FROM DriverViolations dv
            INNER JOIN Bookings b ON b.Id = dv.BookingId AND b.TenantId = @TenantId
            WHERE dv.TenantId = @TenantId AND dv.IsDeleted = 0
              AND dv.OccurredAt BETWEEN @From AND @To
              AND b.RouteId IS NOT NULL
              AND (dv.ViolationType LIKE N'%Accident%' OR dv.ViolationType = N'Accident')
            GROUP BY b.RouteId
            """, ct);

        var speed = await CountByRouteAsync(connection, tenantId, from, to, """
            SELECT b.RouteId, COUNT(*) AS Cnt
            FROM GpsAlertEvents e
            INNER JOIN Bookings b ON b.DriverId = e.DriverId AND b.TenantId = @TenantId
            WHERE e.IsDeleted = 0
              AND b.RouteId IS NOT NULL
              AND b.IsDeleted = 0
              AND e.EventType IN (N'overspeed', N'speed_exceeded')
              AND e.Timestamp BETWEEN @From AND @To
              AND e.Timestamp >= b.PickupTime
              AND (b.DropoffTime IS NULL OR e.Timestamp <= b.DropoffTime)
            GROUP BY b.RouteId
            """, ct);

        return routes.Select(r =>
        {
            var label = !string.IsNullOrWhiteSpace(r.RouteName)
                ? r.RouteName!
                : $"{r.Source} → {r.Destination}";
            return AccidentRiskCalculator.BuildRoute(
                r.RouteId,
                label,
                r.DistanceKm,
                r.TripCount,
                accidents.GetValueOrDefault(r.RouteId),
                speed.GetValueOrDefault(r.RouteId));
        }).ToList();
    }

    private static async Task<Dictionary<int, int>> CountByDriverAsync(
        System.Data.IDbConnection connection,
        int tenantId,
        DateTime from,
        DateTime to,
        string sql,
        CancellationToken ct)
    {
        var rows = await connection.QueryAsync<(int DriverId, int Cnt)>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, From = from, To = to },
            cancellationToken: ct));
        return rows.ToDictionary(r => r.DriverId, r => r.Cnt);
    }

    private static async Task<Dictionary<int, int>> TryCountByDriverAsync(
        System.Data.IDbConnection connection,
        int tenantId,
        DateTime from,
        DateTime to,
        string sql,
        CancellationToken ct)
    {
        try
        {
            return await CountByDriverAsync(connection, tenantId, from, to, sql, ct);
        }
        catch (Exception ex) when (ex.Message.Contains("Invalid object name", StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }
    }

    private static async Task<Dictionary<int, int>> CountByRouteAsync(
        System.Data.IDbConnection connection,
        int tenantId,
        DateTime from,
        DateTime to,
        string sql,
        CancellationToken ct)
    {
        var rows = await connection.QueryAsync<(int RouteId, int Cnt)>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, From = from, To = to },
            cancellationToken: ct));
        return rows.ToDictionary(r => r.RouteId, r => r.Cnt);
    }
}
