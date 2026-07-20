using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Trips.DTOs;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.Trips.Queries;

public record GetTripCalendarQuery(DateTime From, DateTime To)
    : IRequest<ApiResponse<IReadOnlyList<TripCalendarItemDto>>>;

public class GetTripCalendarQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<GetTripCalendarQuery, ApiResponse<IReadOnlyList<TripCalendarItemDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<TripCalendarItemDto>>> Handle(
        GetTripCalendarQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();
        var from = request.From.Date;
        var toExclusive = request.To.Date.AddDays(1);

        var items = (await connection.QueryAsync<TripCalendarItemDto>(new CommandDefinition("""
            SELECT t.Id, t.TripNumber, t.TripName, t.TripDate, t.PlannedStart, t.PlannedEnd,
                   t.Status, c.FullName AS CustomerName, d.FullName AS DriverName,
                   v.Name AS VehicleName, t.Priority
            FROM Trips t
            LEFT JOIN Customers c ON t.CustomerId = c.Id
            LEFT JOIN Drivers d ON t.DriverId = d.Id
            LEFT JOIN Vehicles v ON t.VehicleId = v.Id
            WHERE t.IsDeleted = 0 AND t.TenantId = @TenantId
              AND t.TripDate >= @From AND t.TripDate < @ToExclusive
            ORDER BY t.TripDate, t.PlannedStart
            """,
            new { TenantId = tenantId, From = from, ToExclusive = toExclusive },
            cancellationToken: cancellationToken))).ToList();

        return ApiResponse<IReadOnlyList<TripCalendarItemDto>>.SuccessResponse(items);
    }
}

public record GetLiveTripsQuery(bool TodayOnly = true)
    : IRequest<ApiResponse<IReadOnlyList<TripListItemDto>>>;

public class GetLiveTripsQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<GetLiveTripsQuery, ApiResponse<IReadOnlyList<TripListItemDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<TripListItemDto>>> Handle(
        GetLiveTripsQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();
        var today = DateTime.UtcNow.Date;

        var where = """
            WHERE t.IsDeleted = 0 AND t.TenantId = @TenantId
              AND t.Status NOT IN (@Completed, @Cancelled, @Failed)
              AND (
                    t.Status IN (@Started, @AtPickup, @Enroute, @Delayed)
                 OR (@TodayOnly = 0)
                 OR (t.TripDate = @Today)
              )
            """;

        var items = (await connection.QueryAsync<TripListItemDto>(new CommandDefinition(
            $"{TripSql.ListSelect} {where} ORDER BY t.PlannedStart",
            new
            {
                TenantId = tenantId,
                Today = today,
                TodayOnly = request.TodayOnly ? 1 : 0,
                Started = (int)TripStatus.Started,
                AtPickup = (int)TripStatus.AtPickup,
                Enroute = (int)TripStatus.Enroute,
                Delayed = (int)TripStatus.Delayed,
                Completed = (int)TripStatus.Completed,
                Cancelled = (int)TripStatus.Cancelled,
                Failed = (int)TripStatus.Failed
            },
            cancellationToken: cancellationToken))).ToList();

        return ApiResponse<IReadOnlyList<TripListItemDto>>.SuccessResponse(items);
    }
}

public record GetTripAnalyticsQuery(DateTime? From = null, DateTime? To = null)
    : IRequest<ApiResponse<TripAnalyticsDto>>;

public class GetTripAnalyticsQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<GetTripAnalyticsQuery, ApiResponse<TripAnalyticsDto>>
{
    public async Task<ApiResponse<TripAnalyticsDto>> Handle(
        GetTripAnalyticsQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();
        var to = (request.To ?? DateTime.UtcNow).Date;
        var from = (request.From ?? to.AddDays(-29)).Date;
        var toExclusive = to.AddDays(1);

        var summary = await connection.QuerySingleAsync<(
            int TotalTrips,
            int CompletedTrips,
            int CancelledTrips,
            int DelayedTrips,
            int OngoingTrips,
            decimal? TotalPlannedDistanceKm,
            decimal? TotalActualDistanceKm)>(new CommandDefinition("""
            SELECT
                COUNT(*) AS TotalTrips,
                SUM(CASE WHEN Status = @Completed THEN 1 ELSE 0 END) AS CompletedTrips,
                SUM(CASE WHEN Status = @Cancelled THEN 1 ELSE 0 END) AS CancelledTrips,
                SUM(CASE WHEN Status = @Delayed THEN 1 ELSE 0 END) AS DelayedTrips,
                SUM(CASE WHEN Status IN (@Started, @AtPickup, @Enroute, @Delayed) THEN 1 ELSE 0 END) AS OngoingTrips,
                SUM(PlannedDistanceKm) AS TotalPlannedDistanceKm,
                SUM(ActualDistanceKm) AS TotalActualDistanceKm
            FROM Trips
            WHERE IsDeleted = 0 AND TenantId = @TenantId
              AND TripDate >= @From AND TripDate < @ToExclusive
            """,
            new
            {
                TenantId = tenantId,
                From = from,
                ToExclusive = toExclusive,
                Completed = (int)TripStatus.Completed,
                Cancelled = (int)TripStatus.Cancelled,
                Delayed = (int)TripStatus.Delayed,
                Started = (int)TripStatus.Started,
                AtPickup = (int)TripStatus.AtPickup,
                Enroute = (int)TripStatus.Enroute
            },
            cancellationToken: cancellationToken));

        var totalExpenses = await connection.ExecuteScalarAsync<decimal>(new CommandDefinition("""
            SELECT COALESCE(SUM(e.Amount), 0)
            FROM TripExpenses e
            INNER JOIN Trips t ON t.Id = e.TripId
            WHERE e.IsDeleted = 0 AND t.IsDeleted = 0 AND t.TenantId = @TenantId
              AND t.TripDate >= @From AND t.TripDate < @ToExclusive
            """,
            new { TenantId = tenantId, From = from, ToExclusive = toExclusive },
            cancellationToken: cancellationToken));

        var byStatus = (await connection.QueryAsync<TripNamedCountDto>(new CommandDefinition("""
            SELECT CAST(Status AS nvarchar(32)) AS Name, COUNT(*) AS Count
            FROM Trips
            WHERE IsDeleted = 0 AND TenantId = @TenantId
              AND TripDate >= @From AND TripDate < @ToExclusive
            GROUP BY Status
            ORDER BY Count DESC
            """,
            new { TenantId = tenantId, From = from, ToExclusive = toExclusive },
            cancellationToken: cancellationToken))).ToList();

        // Map numeric status to enum names for the client
        byStatus = byStatus.Select(x =>
        {
            if (int.TryParse(x.Name, out var n) && Enum.IsDefined(typeof(TripStatus), n))
                return new TripNamedCountDto(((TripStatus)n).ToString(), x.Count);
            return x;
        }).ToList();

        var byType = (await connection.QueryAsync<(int Type, int Count)>(new CommandDefinition("""
            SELECT TripType AS Type, COUNT(*) AS Count
            FROM Trips
            WHERE IsDeleted = 0 AND TenantId = @TenantId
              AND TripDate >= @From AND TripDate < @ToExclusive
            GROUP BY TripType
            ORDER BY Count DESC
            """,
            new { TenantId = tenantId, From = from, ToExclusive = toExclusive },
            cancellationToken: cancellationToken)))
            .Select(x => new TripNamedCountDto(
                Enum.IsDefined(typeof(TripType), x.Type) ? ((TripType)x.Type).ToString() : x.Type.ToString(),
                x.Count))
            .ToList();

        var byDriver = (await connection.QueryAsync<TripNamedCountDto>(new CommandDefinition("""
            SELECT COALESCE(d.FullName, N'Unassigned') AS Name, COUNT(*) AS Count
            FROM Trips t
            LEFT JOIN Drivers d ON t.DriverId = d.Id
            WHERE t.IsDeleted = 0 AND t.TenantId = @TenantId
              AND t.TripDate >= @From AND t.TripDate < @ToExclusive
            GROUP BY COALESCE(d.FullName, N'Unassigned')
            ORDER BY Count DESC
            """,
            new { TenantId = tenantId, From = from, ToExclusive = toExclusive },
            cancellationToken: cancellationToken))).Take(10).ToList();

        var byVehicle = (await connection.QueryAsync<TripNamedCountDto>(new CommandDefinition("""
            SELECT COALESCE(v.Name, N'Unassigned') AS Name, COUNT(*) AS Count
            FROM Trips t
            LEFT JOIN Vehicles v ON t.VehicleId = v.Id
            WHERE t.IsDeleted = 0 AND t.TenantId = @TenantId
              AND t.TripDate >= @From AND t.TripDate < @ToExclusive
            GROUP BY COALESCE(v.Name, N'Unassigned')
            ORDER BY COUNT(*) DESC
            """,
            new { TenantId = tenantId, From = from, ToExclusive = toExclusive },
            cancellationToken: cancellationToken))).Take(10).ToList();

        var completionRate = summary.TotalTrips == 0
            ? 0m
            : Math.Round(100m * summary.CompletedTrips / summary.TotalTrips, 1);

        return ApiResponse<TripAnalyticsDto>.SuccessResponse(new TripAnalyticsDto(
            from,
            to,
            summary.TotalTrips,
            summary.CompletedTrips,
            summary.CancelledTrips,
            summary.DelayedTrips,
            summary.OngoingTrips,
            completionRate,
            summary.TotalPlannedDistanceKm,
            summary.TotalActualDistanceKm,
            totalExpenses,
            byStatus,
            byType,
            byDriver,
            byVehicle));
    }
}
