using System.Globalization;
using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Trips;
using SheikhTravelSystem.Application.Features.Trips.DTOs;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.Trips.Queries;

public record GetTripRouteSummaryQuery(int TripId) : IRequest<ApiResponse<TripRouteSummaryDto>>;

public class GetTripRouteSummaryQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<GetTripRouteSummaryQuery, ApiResponse<TripRouteSummaryDto>>
{
    private const double AvgSpeedKmh = 40d;

    public async Task<ApiResponse<TripRouteSummaryDto>> Handle(
        GetTripRouteSummaryQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        var trip = await connection.QuerySingleOrDefaultAsync<TripRouteRow>(new CommandDefinition("""
            SELECT t.Id, t.TripNumber, t.RouteId,
                   COALESCE(NULLIF(r.Name, ''), r.Source + N' → ' + r.Destination) AS RouteName,
                   r.Distance AS RouteDistanceKm, r.EstimatedMinutes AS RouteEstimatedMinutes,
                   t.PickupAddress, t.PickupLatitude, t.PickupLongitude,
                   t.DestinationAddress, t.DestinationLatitude, t.DestinationLongitude,
                   t.PlannedDistanceKm, t.EstimatedDurationMinutes, t.ActualDistanceKm,
                   t.PlannedStart, t.PlannedEnd, t.ActualStart, t.Status, t.VehicleId
            FROM Trips t
            LEFT JOIN Routes r ON t.RouteId = r.Id
            WHERE t.Id = @Id AND t.TenantId = @TenantId AND t.IsDeleted = 0
            """,
            new { Id = request.TripId, TenantId = tenantId },
            cancellationToken: cancellationToken));

        if (trip is null)
            throw new NotFoundException("Trip", request.TripId);

        double? liveLat = null, liveLng = null, liveSpeed = null;
        DateTime? liveAt = null;
        bool? ignition = null;
        if (trip.VehicleId is int vehicleId)
        {
            var live = await connection.QuerySingleOrDefaultAsync<(double Latitude, double Longitude, double? Speed, bool? Ignition, DateTime Timestamp)>(
                new CommandDefinition("""
                    SELECT Latitude, Longitude, Speed, Ignition, Timestamp
                    FROM VehicleCurrentLocation WHERE VehicleId = @VehicleId
                    """,
                    new { VehicleId = vehicleId },
                    cancellationToken: cancellationToken));

            if (live.Timestamp != default)
            {
                liveLat = live.Latitude;
                liveLng = live.Longitude;
                liveSpeed = live.Speed;
                ignition = live.Ignition;
                liveAt = live.Timestamp;
            }
        }

        var plannedKm = trip.PlannedDistanceKm
            ?? trip.RouteDistanceKm
            ?? (decimal?)HaversineKm(
                trip.PickupLatitude, trip.PickupLongitude,
                trip.DestinationLatitude, trip.DestinationLongitude);

        var estimatedMinutes = trip.EstimatedDurationMinutes
            ?? trip.RouteEstimatedMinutes
            ?? (plannedKm.HasValue ? (int)Math.Ceiling((double)plannedKm.Value / AvgSpeedKmh * 60d) : null);

        decimal? remainingKm = null;
        int? etaMinutes = null;
        if (liveLat.HasValue && liveLng.HasValue
            && trip.DestinationLatitude.HasValue && trip.DestinationLongitude.HasValue)
        {
            remainingKm = (decimal?)HaversineKm(
                liveLat, liveLng, trip.DestinationLatitude, trip.DestinationLongitude);
            if (remainingKm.HasValue)
            {
                var speed = liveSpeed is > 5 ? liveSpeed.Value : AvgSpeedKmh;
                etaMinutes = (int)Math.Ceiling((double)remainingKm.Value / speed * 60d);
            }
        }

        decimal? coveredKm = null;
        if (plannedKm.HasValue && remainingKm.HasValue)
            coveredKm = Math.Max(0, plannedKm.Value - remainingKm.Value);

        var googleMapsUrl = BuildGoogleMapsUrl(
            trip.PickupLatitude, trip.PickupLongitude, trip.PickupAddress,
            trip.DestinationLatitude, trip.DestinationLongitude, trip.DestinationAddress);

        var googleDirectionsUrl = BuildGoogleDirectionsUrl(
            trip.PickupLatitude, trip.PickupLongitude, trip.PickupAddress,
            trip.DestinationLatitude, trip.DestinationLongitude, trip.DestinationAddress);

        return ApiResponse<TripRouteSummaryDto>.SuccessResponse(new TripRouteSummaryDto(
            trip.Id,
            trip.TripNumber,
            trip.RouteId,
            trip.RouteName,
            plannedKm,
            estimatedMinutes,
            trip.ActualDistanceKm,
            remainingKm,
            coveredKm,
            etaMinutes,
            liveLat,
            liveLng,
            liveSpeed,
            ignition,
            liveAt,
            googleMapsUrl,
            googleDirectionsUrl,
            HasCoordinates: trip.PickupLatitude.HasValue && trip.DestinationLatitude.HasValue,
            CanOptimize: trip.RouteId.HasValue
                || (trip.PickupLatitude.HasValue && trip.DestinationLatitude.HasValue)));
    }

    internal static double? HaversineKm(double? lat1, double? lon1, double? lat2, double? lon2)
    {
        if (lat1 is null || lon1 is null || lat2 is null || lon2 is null) return null;
        const double R = 6371d;
        var dLat = DegreesToRadians(lat2.Value - lat1.Value);
        var dLon = DegreesToRadians(lon2.Value - lon1.Value);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                + Math.Cos(DegreesToRadians(lat1.Value)) * Math.Cos(DegreesToRadians(lat2.Value))
                * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return Math.Round(R * c, 2);
    }

    private static double DegreesToRadians(double deg) => deg * Math.PI / 180d;

    private static string? BuildGoogleMapsUrl(
        double? plat, double? plng, string? pAddr,
        double? dlat, double? dlng, string? dAddr)
    {
        var origin = FormatPoint(plat, plng, pAddr);
        var dest = FormatPoint(dlat, dlng, dAddr);
        if (origin is null && dest is null) return null;
        if (dest is null) return $"https://www.google.com/maps/search/?api=1&query={Uri.EscapeDataString(origin!)}";
        if (origin is null) return $"https://www.google.com/maps/search/?api=1&query={Uri.EscapeDataString(dest)}";
        return $"https://www.google.com/maps/dir/?api=1&origin={Uri.EscapeDataString(origin)}&destination={Uri.EscapeDataString(dest)}";
    }

    private static string? BuildGoogleDirectionsUrl(
        double? plat, double? plng, string? pAddr,
        double? dlat, double? dlng, string? dAddr)
        => BuildGoogleMapsUrl(plat, plng, pAddr, dlat, dlng, dAddr);

    private static string? FormatPoint(double? lat, double? lng, string? address)
    {
        if (lat.HasValue && lng.HasValue)
            return string.Create(CultureInfo.InvariantCulture, $"{lat.Value},{lng.Value}");
        return string.IsNullOrWhiteSpace(address) ? null : address.Trim();
    }

    private sealed class TripRouteRow
    {
        public int Id { get; init; }
        public string TripNumber { get; init; } = "";
        public int? RouteId { get; init; }
        public string? RouteName { get; init; }
        public decimal? RouteDistanceKm { get; init; }
        public int? RouteEstimatedMinutes { get; init; }
        public string? PickupAddress { get; init; }
        public double? PickupLatitude { get; init; }
        public double? PickupLongitude { get; init; }
        public string? DestinationAddress { get; init; }
        public double? DestinationLatitude { get; init; }
        public double? DestinationLongitude { get; init; }
        public decimal? PlannedDistanceKm { get; init; }
        public int? EstimatedDurationMinutes { get; init; }
        public decimal? ActualDistanceKm { get; init; }
        public DateTime PlannedStart { get; init; }
        public DateTime? PlannedEnd { get; init; }
        public DateTime? ActualStart { get; init; }
        public TripStatus Status { get; init; }
        public int? VehicleId { get; init; }
    }
}

public record OptimizeTripRouteCommand(int TripId) : IRequest<ApiResponse<TripRouteSummaryDto>>, IAuditableCommand
{
    public string AuditAction => "OptimizeRoute";
    public string AuditEntityName => "Trip";
    public int? AuditEntityId => TripId;
}

public class OptimizeTripRouteCommandHandler(
    IDbConnectionFactory dbFactory,
    ITenantContext tenantContext,
    ICurrentUserService currentUser,
    IMediator mediator)
    : IRequestHandler<OptimizeTripRouteCommand, ApiResponse<TripRouteSummaryDto>>
{
    private const double AvgSpeedKmh = 40d;

    public async Task<ApiResponse<TripRouteSummaryDto>> Handle(
        OptimizeTripRouteCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        var trip = await connection.QuerySingleOrDefaultAsync<OptimizeTripRow>(new CommandDefinition("""
            SELECT Id, RouteId, PickupLatitude, PickupLongitude, DestinationLatitude, DestinationLongitude, Status
            FROM Trips WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0
            """,
            new { Id = request.TripId, TenantId = tenantId },
            cancellationToken: cancellationToken));

        if (trip is null)
            throw new NotFoundException("Trip", request.TripId);

        if (TripLifecycle.IsTerminal(trip.Status))
            return ApiResponse<TripRouteSummaryDto>.FailResponse("Cannot optimize a completed or cancelled trip.");

        decimal? distanceKm = null;
        int? minutes = null;
        string source;

        if (trip.RouteId is int routeId)
        {
            var route = await connection.QuerySingleOrDefaultAsync<(decimal? Distance, int? EstimatedMinutes)>(
                new CommandDefinition(
                    "SELECT Distance, EstimatedMinutes FROM Routes WHERE Id = @Id AND IsDeleted = 0",
                    new { Id = routeId },
                    cancellationToken: cancellationToken));
            distanceKm = route.Distance;
            minutes = route.EstimatedMinutes;
            source = "linked route";
        }
        else
        {
            var hv = GetTripRouteSummaryQueryHandler.HaversineKm(
                trip.PickupLatitude, trip.PickupLongitude,
                trip.DestinationLatitude, trip.DestinationLongitude);
            if (hv is null)
                return ApiResponse<TripRouteSummaryDto>.FailResponse(
                    "Add pickup/destination coordinates or link a route before optimizing.");
            distanceKm = (decimal)hv.Value;
            minutes = (int)Math.Ceiling(hv.Value / AvgSpeedKmh * 60d);
            source = "straight-line estimate";
        }

        if (!minutes.HasValue && distanceKm.HasValue)
            minutes = (int)Math.Ceiling((double)distanceKm.Value / AvgSpeedKmh * 60d);

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE Trips SET
                PlannedDistanceKm = @DistanceKm,
                EstimatedDurationMinutes = @Minutes,
                UpdatedAt = GETUTCDATE(),
                UpdatedBy = @Actor
            WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0
            """,
            new
            {
                Id = request.TripId,
                TenantId = tenantId,
                DistanceKm = distanceKm,
                Minutes = minutes,
                Actor = currentUser.UserId?.ToString()
            },
            cancellationToken: cancellationToken));

        var summary = await mediator.Send(new GetTripRouteSummaryQuery(request.TripId), cancellationToken);
        return ApiResponse<TripRouteSummaryDto>.SuccessResponse(
            summary.Data!,
            $"Route optimized from {source}.");
    }

    private sealed class OptimizeTripRow
    {
        public int Id { get; init; }
        public int? RouteId { get; init; }
        public double? PickupLatitude { get; init; }
        public double? PickupLongitude { get; init; }
        public double? DestinationLatitude { get; init; }
        public double? DestinationLongitude { get; init; }
        public TripStatus Status { get; init; }
    }
}
