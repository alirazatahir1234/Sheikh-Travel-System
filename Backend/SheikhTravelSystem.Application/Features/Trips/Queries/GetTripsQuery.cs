using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Trips.DTOs;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.Trips.Queries;

public record GetTripDashboardQuery : IRequest<ApiResponse<TripDashboardDto>>;

public class GetTripDashboardQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<GetTripDashboardQuery, ApiResponse<TripDashboardDto>>
{
    public async Task<ApiResponse<TripDashboardDto>> Handle(GetTripDashboardQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();
        var today = DateTime.UtcNow.Date;

        var row = await connection.QuerySingleAsync<TripDashboardDto>(new CommandDefinition("""
            SELECT
                COUNT(*) AS TotalTrips,
                SUM(CASE WHEN Status IN (@Scheduled, @DriverAssigned, @VehicleAssigned) THEN 1 ELSE 0 END) AS ScheduledTrips,
                SUM(CASE WHEN Status IN (@Started, @AtPickup, @Enroute, @Delayed) THEN 1 ELSE 0 END) AS OngoingTrips,
                SUM(CASE WHEN Status = @Completed THEN 1 ELSE 0 END) AS CompletedTrips,
                SUM(CASE WHEN Status = @Cancelled THEN 1 ELSE 0 END) AS CancelledTrips,
                SUM(CASE WHEN Status = @Delayed THEN 1 ELSE 0 END) AS DelayedTrips,
                SUM(CASE WHEN TripDate = @Today THEN 1 ELSE 0 END) AS TodaysTrips,
                SUM(CASE WHEN TripDate > @Today AND Status NOT IN (@Completed, @Cancelled, @Failed) THEN 1 ELSE 0 END) AS UpcomingTrips
            FROM Trips
            WHERE IsDeleted = 0 AND TenantId = @TenantId
            """,
            new
            {
                TenantId = tenantId,
                Today = today,
                Scheduled = (int)TripStatus.Scheduled,
                DriverAssigned = (int)TripStatus.DriverAssigned,
                VehicleAssigned = (int)TripStatus.VehicleAssigned,
                Started = (int)TripStatus.Started,
                AtPickup = (int)TripStatus.AtPickup,
                Enroute = (int)TripStatus.Enroute,
                Delayed = (int)TripStatus.Delayed,
                Completed = (int)TripStatus.Completed,
                Cancelled = (int)TripStatus.Cancelled,
                Failed = (int)TripStatus.Failed
            },
            cancellationToken: cancellationToken));

        return ApiResponse<TripDashboardDto>.SuccessResponse(row);
    }
}

public record GetTripsQuery(
    int Page = 1,
    int PageSize = 20,
    TripStatus? Status = null,
    int? DriverId = null,
    int? VehicleId = null,
    int? RouteId = null,
    int? CustomerId = null,
    DateTime? DateFrom = null,
    DateTime? DateTo = null,
    string? Search = null,
    bool TodayOnly = false,
    bool TomorrowOnly = false,
    bool UpcomingOnly = false
) : IRequest<ApiResponse<PagedResult<TripListItemDto>>>;

public class GetTripsQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<GetTripsQuery, ApiResponse<PagedResult<TripListItemDto>>>
{
    public async Task<ApiResponse<PagedResult<TripListItemDto>>> Handle(GetTripsQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();
        var offset = (request.Page - 1) * request.PageSize;

        var where = "WHERE t.IsDeleted = 0 AND t.TenantId = @TenantId";
        if (request.Status.HasValue) where += " AND t.Status = @Status";
        if (request.DriverId.HasValue) where += " AND t.DriverId = @DriverId";
        if (request.VehicleId.HasValue) where += " AND t.VehicleId = @VehicleId";
        if (request.RouteId.HasValue) where += " AND t.RouteId = @RouteId";
        if (request.CustomerId.HasValue) where += " AND t.CustomerId = @CustomerId";
        if (!string.IsNullOrWhiteSpace(request.Search))
            where += " AND (t.TripNumber LIKE @SearchPattern OR t.TripName LIKE @SearchPattern OR c.FullName LIKE @SearchPattern OR b.BookingNumber LIKE @SearchPattern)";

        DateTime? dateFrom = request.DateFrom;
        DateTime? dateToExclusive = request.DateTo?.Date.AddDays(1);
        if (request.TodayOnly)
        {
            dateFrom = DateTime.UtcNow.Date;
            dateToExclusive = dateFrom.Value.AddDays(1);
        }
        else if (request.TomorrowOnly)
        {
            dateFrom = DateTime.UtcNow.Date.AddDays(1);
            dateToExclusive = dateFrom.Value.AddDays(1);
        }
        else if (request.UpcomingOnly)
        {
            dateFrom = DateTime.UtcNow.Date.AddDays(1);
            dateToExclusive = null;
            where += " AND t.Status NOT IN (@Completed, @Cancelled, @Failed)";
        }

        if (dateFrom.HasValue) where += " AND t.TripDate >= @DateFrom";
        if (dateToExclusive.HasValue) where += " AND t.TripDate < @DateTo";

        var parms = new
        {
            Offset = offset,
            request.PageSize,
            TenantId = tenantId,
            Status = (int?)request.Status,
            request.DriverId,
            request.VehicleId,
            request.RouteId,
            request.CustomerId,
            SearchPattern = $"%{request.Search}%",
            DateFrom = dateFrom,
            DateTo = dateToExclusive,
            Completed = (int)TripStatus.Completed,
            Cancelled = (int)TripStatus.Cancelled,
            Failed = (int)TripStatus.Failed
        };

        var items = await connection.QueryAsync<TripListItemDto>(new CommandDefinition(
            $"{TripSql.ListSelect} {where} ORDER BY t.PlannedStart DESC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY",
            parms,
            cancellationToken: cancellationToken));

        var total = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            $@"SELECT COUNT(*) FROM Trips t
               LEFT JOIN Customers c ON t.CustomerId = c.Id
               LEFT JOIN Bookings b ON t.BookingId = b.Id
               {where}",
            parms,
            cancellationToken: cancellationToken));

        return ApiResponse<PagedResult<TripListItemDto>>.SuccessResponse(new PagedResult<TripListItemDto>
        {
            Items = items.ToList(),
            TotalCount = total,
            Page = request.Page,
            PageSize = request.PageSize
        });
    }
}

public record GetTripByIdQuery(int Id) : IRequest<ApiResponse<TripDetailDto>>;

public class GetTripByIdQueryHandler(
    IDbConnectionFactory dbFactory,
    ITenantContext tenantContext,
    IFileStorageService fileStorage)
    : IRequestHandler<GetTripByIdQuery, ApiResponse<TripDetailDto>>
{
    public async Task<ApiResponse<TripDetailDto>> Handle(GetTripByIdQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        var row = await connection.QuerySingleOrDefaultAsync<TripDetailRow>(new CommandDefinition("""
            SELECT t.Id, t.TripNumber, t.BookingId, b.BookingNumber,
                   t.CustomerId, c.FullName AS CustomerName,
                   t.RouteId,
                   COALESCE(NULLIF(r.Name, ''), r.Source + N' → ' + r.Destination) AS RouteName,
                   t.TripName, t.TripType,
                   t.PickupAddress, t.PickupLatitude, t.PickupLongitude,
                   t.DestinationAddress, t.DestinationLatitude, t.DestinationLongitude,
                   t.TripDate, t.PlannedStart, t.PlannedEnd, t.EstimatedDurationMinutes,
                   t.DriverId, d.FullName AS DriverName,
                   t.AssistantDriverId, ad.FullName AS AssistantDriverName,
                   t.VehicleId, v.Name AS VehicleName,
                   t.PassengerCount, t.Priority, t.Status, t.DriverNotes,
                   t.PlannedDistanceKm, t.ActualDistanceKm, t.ActualStart, t.ActualEnd,
                   t.CancellationReason, t.CreatedAt,
                   CAST(CASE WHEN t.VehicleId IS NOT NULL AND EXISTS (
                       SELECT 1 FROM GpsDevices gd
                       WHERE gd.VehicleId = t.VehicleId AND gd.IsDeleted = 0
                         AND gd.LastSeenAt >= DATEADD(minute, -15, GETUTCDATE())
                   ) THEN 1 ELSE 0 END AS BIT) AS GpsOnline
            FROM Trips t
            LEFT JOIN Bookings b ON t.BookingId = b.Id
            LEFT JOIN Customers c ON t.CustomerId = c.Id
            LEFT JOIN Routes r ON t.RouteId = r.Id
            LEFT JOIN Drivers d ON t.DriverId = d.Id
            LEFT JOIN Drivers ad ON t.AssistantDriverId = ad.Id
            LEFT JOIN Vehicles v ON t.VehicleId = v.Id
            WHERE t.Id = @Id AND t.TenantId = @TenantId AND t.IsDeleted = 0
            """,
            new { request.Id, TenantId = tenantId },
            cancellationToken: cancellationToken));

        if (row is null)
            throw new NotFoundException("Trip", request.Id);

        var stops = (await connection.QueryAsync<TripStopDto>(new CommandDefinition(
            @"SELECT Id, Sequence, Location, Latitude, Longitude, Eta, ArrivalTime, DepartureTime
              FROM TripStops WHERE TripId = @Id AND IsDeleted = 0 ORDER BY Sequence",
            new { request.Id },
            cancellationToken: cancellationToken))).ToList();

        var timeline = (await connection.QueryAsync<TripStatusHistoryDto>(new CommandDefinition(
            @"SELECT Id, FromStatus, ToStatus, ChangedAtUtc, ChangedBy, Note
              FROM TripStatusHistory WHERE TripId = @Id ORDER BY ChangedAtUtc, Id",
            new { request.Id },
            cancellationToken: cancellationToken))).ToList();

        var expenses = (await connection.QueryAsync<TripExpenseDto>(new CommandDefinition(
            @"SELECT Id, ExpenseType, Amount, Description, ExpenseDate, CreatedAt
              FROM TripExpenses WHERE TripId = @Id AND IsDeleted = 0 ORDER BY ExpenseDate DESC, Id DESC",
            new { request.Id },
            cancellationToken: cancellationToken))).ToList();

        var docs = (await connection.QueryAsync<(int Id, string DocumentType, string FileName, string StorageKey, string? UploadedBy, DateTime CreatedAt)>(
            new CommandDefinition(
                @"SELECT Id, DocumentType, FileName, StorageKey, UploadedBy, CreatedAt
                  FROM TripDocuments WHERE TripId = @Id AND IsDeleted = 0 ORDER BY CreatedAt DESC",
                new { request.Id },
                cancellationToken: cancellationToken))).ToList();

        var documents = docs.Select(d => new TripDocumentDto(
            d.Id, d.DocumentType, d.FileName, fileStorage.ResolveReadUrl(d.StorageKey), d.UploadedBy, d.CreatedAt)).ToList();

        var passengers = (await connection.QueryAsync<TripPassengerDto>(new CommandDefinition(
            @"SELECT Id, FullName, Phone, BoardingStatus, DropStatus, Notes
              FROM TripPassengers WHERE TripId = @Id AND IsDeleted = 0 ORDER BY Id",
            new { request.Id },
            cancellationToken: cancellationToken))).ToList();

        var openAlerts = 0;
        if (row.VehicleId is int vehicleId)
        {
            openAlerts = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
                SELECT COUNT(*) FROM GpsAlertEvents
                WHERE VehicleId = @VehicleId AND IsDeleted = 0
                  AND (Status IS NULL OR LOWER(Status) = N'active')
                  AND Timestamp >= DATEADD(day, -7, GETUTCDATE())
                """,
                new { VehicleId = vehicleId },
                cancellationToken: cancellationToken));
        }

        var detail = new TripDetailDto(
            row.Id, row.TripNumber, row.BookingId, row.BookingNumber,
            row.CustomerId, row.CustomerName, row.RouteId, row.RouteName,
            row.TripName, (TripType)row.TripType,
            row.PickupAddress, row.PickupLatitude, row.PickupLongitude,
            row.DestinationAddress, row.DestinationLatitude, row.DestinationLongitude,
            row.TripDate, row.PlannedStart, row.PlannedEnd, row.EstimatedDurationMinutes,
            row.DriverId, row.DriverName, row.AssistantDriverId, row.AssistantDriverName,
            row.VehicleId, row.VehicleName, row.PassengerCount,
            (TripPriority)row.Priority, (TripStatus)row.Status, row.DriverNotes,
            row.PlannedDistanceKm, row.ActualDistanceKm, row.ActualStart, row.ActualEnd,
            row.CancellationReason, row.GpsOnline, row.CreatedAt, stops, timeline,
            expenses, documents, passengers, openAlerts);

        return ApiResponse<TripDetailDto>.SuccessResponse(detail);
    }

    private sealed class TripDetailRow
    {
        public int Id { get; init; }
        public string TripNumber { get; init; } = "";
        public int? BookingId { get; init; }
        public string? BookingNumber { get; init; }
        public int CustomerId { get; init; }
        public string? CustomerName { get; init; }
        public int? RouteId { get; init; }
        public string? RouteName { get; init; }
        public string TripName { get; init; } = "";
        public int TripType { get; init; }
        public string? PickupAddress { get; init; }
        public double? PickupLatitude { get; init; }
        public double? PickupLongitude { get; init; }
        public string? DestinationAddress { get; init; }
        public double? DestinationLatitude { get; init; }
        public double? DestinationLongitude { get; init; }
        public DateTime TripDate { get; init; }
        public DateTime PlannedStart { get; init; }
        public DateTime? PlannedEnd { get; init; }
        public int? EstimatedDurationMinutes { get; init; }
        public int? DriverId { get; init; }
        public string? DriverName { get; init; }
        public int? AssistantDriverId { get; init; }
        public string? AssistantDriverName { get; init; }
        public int? VehicleId { get; init; }
        public string? VehicleName { get; init; }
        public int PassengerCount { get; init; }
        public int Priority { get; init; }
        public int Status { get; init; }
        public string? DriverNotes { get; init; }
        public decimal? PlannedDistanceKm { get; init; }
        public decimal? ActualDistanceKm { get; init; }
        public DateTime? ActualStart { get; init; }
        public DateTime? ActualEnd { get; init; }
        public string? CancellationReason { get; init; }
        public bool GpsOnline { get; init; }
        public DateTime CreatedAt { get; init; }
    }
}
