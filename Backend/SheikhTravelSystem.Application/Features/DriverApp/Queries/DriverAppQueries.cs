using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.DriverApp.Commands;
using SheikhTravelSystem.Application.Features.DriverApp.DTOs;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.DriverApp.Queries;

public record GetDriverTripsQuery : IRequest<ApiResponse<List<DriverTripDto>>>;
public record GetDriverProfileQuery : IRequest<ApiResponse<DriverProfileDto>>;
public record GetDriverDashboardQuery : IRequest<ApiResponse<DriverDashboardDto>>;
public record GetDriverAttendanceHistoryQuery(DateTime? From, DateTime? To, int Page = 1, int PageSize = 30)
    : IRequest<ApiResponse<List<DriverAttendanceRecordDto>>>;
public record GetDriverTimelineQuery(int Page = 1, int PageSize = 50)
    : IRequest<ApiResponse<List<DriverTimelineEventDto>>>;
public record GetDriverEarningsQuery(DateTime? FromDate, DateTime? ToDate) : IRequest<ApiResponse<DriverEarningsDto>>;

public class GetDriverProfileQueryHandler(
    IDriverAppRepository repository,
    ICurrentUserService currentUser,
    ITenantContext tenantContext)
    : IRequestHandler<GetDriverProfileQuery, ApiResponse<DriverProfileDto>>
{
    public async Task<ApiResponse<DriverProfileDto>> Handle(GetDriverProfileQuery request, CancellationToken cancellationToken)
    {
        var driverId = currentUser.DriverId;
        if (!driverId.HasValue) return ApiResponse<DriverProfileDto>.FailResponse("Driver identity required.");

        var row = await repository.GetProfileAsync(driverId.Value, tenantContext.GetRequiredTenantId(), cancellationToken);
        if (row is null) return ApiResponse<DriverProfileDto>.FailResponse("Driver not found.");
        return ApiResponse<DriverProfileDto>.SuccessResponse(row);
    }
}

public class GetDriverDashboardQueryHandler(
    IDriverAppRepository repository,
    ICurrentUserService currentUser,
    ITenantContext tenantContext)
    : IRequestHandler<GetDriverDashboardQuery, ApiResponse<DriverDashboardDto>>
{
    public async Task<ApiResponse<DriverDashboardDto>> Handle(GetDriverDashboardQuery request, CancellationToken cancellationToken)
    {
        var driverId = currentUser.DriverId;
        if (!driverId.HasValue) return ApiResponse<DriverDashboardDto>.FailResponse("Driver identity required.");

        var today = DateTime.UtcNow.Date;
        var weekStart = today.AddDays(-(int)today.DayOfWeek);
        var stats = await repository.GetDashboardStatsAsync(
            driverId.Value, tenantContext.GetRequiredTenantId(), currentUser.UserId, today, weekStart, cancellationToken);
        if (stats is null) return ApiResponse<DriverDashboardDto>.FailResponse("Driver not found.");

        var statusName = stats.Status switch
        {
            1 => "Available", 2 => "On Trip", 3 => "Off Duty", 4 => "Suspended", 5 => "On Leave", _ => "Unknown"
        };

        return ApiResponse<DriverDashboardDto>.SuccessResponse(new DriverDashboardDto(
            stats.Assigned, stats.Completed, stats.ClockedIn,
            stats.Vehicle, stats.Plate, stats.WeekEarnings, stats.Unread, statusName));
    }
}

public class GetDriverAttendanceHistoryQueryHandler(
    IDriverAppRepository repository,
    ICurrentUserService currentUser)
    : IRequestHandler<GetDriverAttendanceHistoryQuery, ApiResponse<List<DriverAttendanceRecordDto>>>
{
    public async Task<ApiResponse<List<DriverAttendanceRecordDto>>> Handle(GetDriverAttendanceHistoryQuery request, CancellationToken cancellationToken)
    {
        var driverId = currentUser.DriverId;
        if (!driverId.HasValue) return ApiResponse<List<DriverAttendanceRecordDto>>.FailResponse("Driver identity required.");

        var from = request.From ?? DateTime.UtcNow.AddDays(-30);
        var to = request.To ?? DateTime.UtcNow;
        var offset = (request.Page - 1) * request.PageSize;

        var rows = await repository.GetAttendanceHistoryAsync(
            driverId.Value, from, to, offset, request.PageSize, cancellationToken);
        return ApiResponse<List<DriverAttendanceRecordDto>>.SuccessResponse(rows.ToList());
    }
}

public class GetDriverTripsQueryHandler(
    IDriverAppRepository repository,
    ICurrentUserService currentUser,
    ITenantContext tenantContext)
    : IRequestHandler<GetDriverTripsQuery, ApiResponse<List<DriverTripDto>>>
{
    public async Task<ApiResponse<List<DriverTripDto>>> Handle(GetDriverTripsQuery request, CancellationToken cancellationToken)
    {
        var driverId = currentUser.DriverId;
        if (!driverId.HasValue)
            return ApiResponse<List<DriverTripDto>>.FailResponse("Driver identity required.");

        var tenantId = tenantContext.GetRequiredTenantId();
        var results = new List<DriverTripDto>();

        var tripRows = await repository.GetOperationalTripsAsync(driverId.Value, tenantId, cancellationToken);
        var linkedBookingIds = new HashSet<int>();
        foreach (var r in tripRows)
        {
            if (r.BookingId is int bid) linkedBookingIds.Add(bid);
            var status = (TripStatus)r.Status;
            var pickupAddr = string.IsNullOrWhiteSpace(r.PickupAddress) ? r.RouteSource : r.PickupAddress;
            var dropAddr = string.IsNullOrWhiteSpace(r.DropoffAddress) ? r.RouteDestination : r.DropoffAddress;
            var (plat, plng) = DriverAppGeo.ResolveCoords(r.PickupLatitude, r.PickupLongitude, pickupAddr);
            var (dlat, dlng) = DriverAppGeo.ResolveCoords(r.DropLatitude, r.DropLongitude, dropAddr);
            results.Add(new DriverTripDto(
                Id: r.Id,
                BookingNumber: !string.IsNullOrWhiteSpace(r.BookingNumber) ? r.BookingNumber! : r.TripNumber ?? $"T-{r.Id}",
                CustomerName: r.CustomerName ?? "",
                RouteName: r.RouteName ?? "",
                PickupTime: r.PickupTime, DropoffTime: r.DropoffTime,
                Status: MapTripToLegacyBookingStatus(status),
                StatusName: DriverTripLabels.Name(status),
                VehicleId: r.VehicleId, VehicleName: r.VehicleName, TotalAmount: r.TotalAmount,
                PickupAddress: pickupAddr, PickupLatitude: plat, PickupLongitude: plng,
                DropoffAddress: dropAddr, DropLatitude: dlat, DropLongitude: dlng,
                GoogleMapsUrl: DriverAppGeo.BuildGoogleMapsUrl(plat, plng, pickupAddr, dlat, dlng, dropAddr),
                GoogleDirectionsUrl: DriverAppGeo.BuildGoogleMapsUrl(plat, plng, pickupAddr, dlat, dlng, dropAddr),
                TripId: r.Id, BookingId: r.BookingId, Source: "Trip",
                LifecycleStatus: r.Status, LifecycleStatusName: DriverTripLabels.Name(status),
                NextActions: DriverTripLabels.NextActions(status)));
        }

        var bookingRows = await repository.GetLegacyBookingTripsAsync(driverId.Value, tenantId, cancellationToken);
        foreach (var r in bookingRows)
        {
            if (linkedBookingIds.Contains(r.Id)) continue;
            var (plat, plng) = DriverAppGeo.ResolveCoords(r.PickupLatitude, r.PickupLongitude, r.RouteSource);
            var (dlat, dlng) = DriverAppGeo.ResolveCoords(r.DropLatitude, r.DropLongitude, r.RouteDestination);
            var pickupAddr = string.IsNullOrWhiteSpace(r.PickupAddress) ? r.RouteSource : r.PickupAddress;
            var dropAddr = string.IsNullOrWhiteSpace(r.DropoffAddress) ? r.RouteDestination : r.DropoffAddress;
            var bookingStatus = (BookingStatus)r.Status;
            var lifecycle = MapBookingToTripStatus(bookingStatus);

            results.Add(new DriverTripDto(
                r.Id, r.BookingNumber, r.CustomerName ?? "", r.RouteName ?? "",
                r.PickupTime, r.DropoffTime, r.Status, r.StatusName ?? "",
                r.VehicleId, r.VehicleName, r.TotalAmount,
                pickupAddr, plat, plng, dropAddr, dlat, dlng,
                DriverAppGeo.BuildGoogleMapsUrl(plat, plng, pickupAddr, dlat, dlng, dropAddr),
                DriverAppGeo.BuildGoogleMapsUrl(plat, plng, pickupAddr, dlat, dlng, dropAddr),
                TripId: null, BookingId: r.Id, Source: "Booking",
                LifecycleStatus: (int)lifecycle, LifecycleStatusName: DriverTripLabels.Name(lifecycle),
                NextActions: DriverTripLabels.NextActionsFromBooking(bookingStatus)));
        }

        var bookingIds = results.Select(t => t.BookingId ?? 0).Where(id => id > 0).Distinct().ToArray();
        if (bookingIds.Length > 0)
        {
            var paidRows = await repository.GetPaidAmountsForBookingsAsync(bookingIds, cancellationToken);
            var paidMap = paidRows.ToDictionary(x => x.BookingId, x => x.PaidAmount);
            for (var i = 0; i < results.Count; i++)
            {
                var t = results[i];
                var bookingId = t.BookingId ?? 0;
                var paid = bookingId > 0 && paidMap.TryGetValue(bookingId, out var p) ? p : 0m;
                var balance = Math.Max(0, t.TotalAmount - paid);
                var payStatus = balance <= 0 ? "Paid" : (paid > 0 ? "PartiallyPaid" : "Pending");
                results[i] = t with
                {
                    PaidAmount = paid, BalanceDue = balance,
                    PaymentRequired = balance > 0, PaymentStatus = payStatus
                };
            }
        }

        return ApiResponse<List<DriverTripDto>>.SuccessResponse(results.OrderBy(t => t.PickupTime).ToList());
    }

    private static TripStatus MapBookingToTripStatus(BookingStatus status) => status switch
    {
        BookingStatus.Confirmed => TripStatus.DriverAssigned,
        BookingStatus.Started => TripStatus.Started,
        BookingStatus.Completed => TripStatus.Completed,
        BookingStatus.Cancelled => TripStatus.Cancelled,
        _ => TripStatus.Scheduled
    };

    private static int MapTripToLegacyBookingStatus(TripStatus status) => status switch
    {
        TripStatus.Completed => (int)BookingStatus.Completed,
        TripStatus.Cancelled or TripStatus.Failed => (int)BookingStatus.Cancelled,
        TripStatus.Started or TripStatus.AtPickup or TripStatus.Enroute or TripStatus.Delayed
            => (int)BookingStatus.Started,
        _ => (int)BookingStatus.Confirmed
    };
}

public class GetDriverTimelineQueryHandler(
    IDriverAppRepository repository,
    ICurrentUserService currentUser,
    ITenantContext tenantContext)
    : IRequestHandler<GetDriverTimelineQuery, ApiResponse<List<DriverTimelineEventDto>>>
{
    public async Task<ApiResponse<List<DriverTimelineEventDto>>> Handle(
        GetDriverTimelineQuery request, CancellationToken cancellationToken)
    {
        var driverId = currentUser.DriverId;
        if (!driverId.HasValue)
            return ApiResponse<List<DriverTimelineEventDto>>.FailResponse("Driver identity required.");

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 100 ? 50 : request.PageSize;
        var offset = (page - 1) * pageSize;

        var rows = await repository.GetTimelineAsync(
            driverId.Value, tenantContext.GetRequiredTenantId(), currentUser.UserId, offset, pageSize, cancellationToken);
        return ApiResponse<List<DriverTimelineEventDto>>.SuccessResponse(rows.ToList());
    }
}

public class GetDriverEarningsQueryHandler(
    IDriverAppRepository repository,
    ICurrentUserService currentUser)
    : IRequestHandler<GetDriverEarningsQuery, ApiResponse<DriverEarningsDto>>
{
    public async Task<ApiResponse<DriverEarningsDto>> Handle(GetDriverEarningsQuery request, CancellationToken cancellationToken)
    {
        var driverId = currentUser.DriverId;
        if (!driverId.HasValue)
            return ApiResponse<DriverEarningsDto>.FailResponse("Driver identity required.");

        var now = DateTime.UtcNow;
        var today = now.Date;
        var weekStart = today.AddDays(-(int)today.DayOfWeek);
        var monthStart = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var from = request.FromDate ?? weekStart.AddDays(-21);
        var to = request.ToDate ?? now;
        var d = driverId.Value;
        var paidStatus = (int)PaymentStatus.Paid;

        var todayAmt = await repository.SumPaymentsAsync(d, today, now, null, cancellationToken);
        var weekAmt = await repository.SumPaymentsAsync(d, weekStart, now, null, cancellationToken);
        var monthAmt = await repository.SumPaymentsAsync(d, monthStart, now, null, cancellationToken);
        var rangeAmt = await repository.SumPaymentsAsync(d, from, to, null, cancellationToken);
        var paidAmt = await repository.SumPaymentsAsync(d, from, to, paidStatus, cancellationToken);
        var pendingAmt = await repository.SumPendingPartialPaymentsAsync(d, from, to, cancellationToken);
        var completed = await repository.CountCompletedBookingsAsync(d, from, to, cancellationToken);
        var fuelCost = await repository.SumFuelCostAsync(d, from, to, cancellationToken);
        var distanceKm = await repository.SumBookingDistanceAsync(d, from, to, cancellationToken);
        var tripDistance = await repository.SumTripDistanceAsync(d, from, to, cancellationToken);
        if (tripDistance is > 0)
            distanceKm = tripDistance.Value;
        var hours = await repository.SumBookingHoursAsync(d, from, to, cancellationToken);
        var dailyRows = await repository.GetDailyEarningsAsync(d, today.AddDays(-6), today.AddDays(1), cancellationToken);

        var byDay = dailyRows.ToDictionary(r => r.Day.Date, r => r);
        var daily = new List<DriverEarningsDayDto>();
        for (var i = 6; i >= 0; i--)
        {
            var day = today.AddDays(-i);
            if (byDay.TryGetValue(day, out var row))
                daily.Add(new DriverEarningsDayDto(day, row.Amount, row.TripCount));
            else
                daily.Add(new DriverEarningsDayDto(day, 0, 0));
        }

        return ApiResponse<DriverEarningsDto>.SuccessResponse(new DriverEarningsDto(
            rangeAmt, completed, from, to, todayAmt, weekAmt, monthAmt, pendingAmt, paidAmt,
            fuelCost, Math.Round(distanceKm, 1), Math.Round(hours, 1), daily));
    }
}
