using MediatR;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Queries;

public record GetPlannedRouteQuery(
    double OriginLatitude,
    double OriginLongitude,
    double DestinationLatitude,
    double DestinationLongitude,
    IReadOnlyList<LatLngPoint>? Waypoints = null) : IRequest<ApiResponse<GoogleRouteResult>>;

public class GetPlannedRouteQueryHandler(IGoogleRoutesService routesService)
    : IRequestHandler<GetPlannedRouteQuery, ApiResponse<GoogleRouteResult>>
{
    public async Task<ApiResponse<GoogleRouteResult>> Handle(
        GetPlannedRouteQuery request, CancellationToken cancellationToken)
    {
        if (!IsValidCoord(request.OriginLatitude, request.OriginLongitude)
            || !IsValidCoord(request.DestinationLatitude, request.DestinationLongitude))
        {
            return ApiResponse<GoogleRouteResult>.FailResponse("Invalid coordinates.");
        }

        var route = await routesService.ComputeRouteAsync(
            request.OriginLatitude,
            request.OriginLongitude,
            request.DestinationLatitude,
            request.DestinationLongitude,
            request.Waypoints,
            cancellationToken);

        if (route is null)
            return ApiResponse<GoogleRouteResult>.FailResponse("Route could not be computed.");

        return ApiResponse<GoogleRouteResult>.SuccessResponse(route);
    }

    private static bool IsValidCoord(double lat, double lng)
        => lat is >= -90 and <= 90 && lng is >= -180 and <= 180;
}

public record GetLocationTimeZoneQuery(
    double Latitude,
    double Longitude,
    DateTime? TimestampUtc = null) : IRequest<ApiResponse<GoogleTimeZoneResult>>;

public class GetLocationTimeZoneQueryHandler(IGoogleTimeZoneService timeZoneService)
    : IRequestHandler<GetLocationTimeZoneQuery, ApiResponse<GoogleTimeZoneResult>>
{
    public async Task<ApiResponse<GoogleTimeZoneResult>> Handle(
        GetLocationTimeZoneQuery request, CancellationToken cancellationToken)
    {
        if (request.Latitude is < -90 or > 90 || request.Longitude is < -180 or > 180)
            return ApiResponse<GoogleTimeZoneResult>.FailResponse("Invalid coordinates.");

        var result = await timeZoneService.GetTimeZoneAsync(
            request.Latitude, request.Longitude, request.TimestampUtc, cancellationToken);

        if (result is null)
            return ApiResponse<GoogleTimeZoneResult>.FailResponse("Time zone could not be resolved.");

        return ApiResponse<GoogleTimeZoneResult>.SuccessResponse(result);
    }
}

public record SnapGpsPathQuery(IReadOnlyList<LatLngPoint> Points)
    : IRequest<ApiResponse<IReadOnlyList<SnappedGpsPoint>>>;

public class SnapGpsPathQueryHandler(
    IGoogleRoadsService roadsService,
    IOptions<GoogleMapsOptions> options)
    : IRequestHandler<SnapGpsPathQuery, ApiResponse<IReadOnlyList<SnappedGpsPoint>>>
{
    public async Task<ApiResponse<IReadOnlyList<SnappedGpsPoint>>> Handle(
        SnapGpsPathQuery request, CancellationToken cancellationToken)
    {
        if (request.Points.Count == 0)
            return ApiResponse<IReadOnlyList<SnappedGpsPoint>>.FailResponse("At least one point is required.");

        if (request.Points.Count < options.Value.RoadsMinPoints)
        {
            return ApiResponse<IReadOnlyList<SnappedGpsPoint>>.FailResponse(
                $"At least {options.Value.RoadsMinPoints} points are required for road snapping.");
        }

        foreach (var point in request.Points)
        {
            if (point.Latitude is < -90 or > 90 || point.Longitude is < -180 or > 180)
                return ApiResponse<IReadOnlyList<SnappedGpsPoint>>.FailResponse("Invalid coordinates in path.");
        }

        var snapped = await roadsService.SnapToRoadsAsync(request.Points, cancellationToken);
        if (snapped is null || snapped.Count == 0)
            return ApiResponse<IReadOnlyList<SnappedGpsPoint>>.FailResponse("Path could not be snapped to roads.");

        return ApiResponse<IReadOnlyList<SnappedGpsPoint>>.SuccessResponse(snapped);
    }
}

public record GetStaticMapUrlQuery(
    double CenterLatitude,
    double CenterLongitude,
    int Zoom = 14,
    int Width = 640,
    int Height = 480,
    IReadOnlyList<LatLngPoint>? Markers = null,
    IReadOnlyList<LatLngPoint>? Path = null) : IRequest<ApiResponse<string>>;

public class GetStaticMapUrlQueryHandler(IGoogleStaticMapsService staticMapsService)
    : IRequestHandler<GetStaticMapUrlQuery, ApiResponse<string>>
{
    public Task<ApiResponse<string>> Handle(GetStaticMapUrlQuery request, CancellationToken cancellationToken)
    {
        var url = staticMapsService.BuildStaticMapUrl(new StaticMapRequestDto(
            request.CenterLatitude,
            request.CenterLongitude,
            request.Zoom,
            request.Width,
            request.Height,
            request.Markers,
            request.Path));

        if (url is null)
            return Task.FromResult(ApiResponse<string>.FailResponse("Static map URL is unavailable."));

        return Task.FromResult(ApiResponse<string>.SuccessResponse(url));
    }
}

public record GetStreetViewUrlQuery(
    double Latitude,
    double Longitude,
    int Width = 600,
    int Height = 400,
    int? Heading = null,
    int? Pitch = null,
    int? Fov = null) : IRequest<ApiResponse<string>>;

public class GetStreetViewUrlQueryHandler(IGoogleStreetViewService streetViewService)
    : IRequestHandler<GetStreetViewUrlQuery, ApiResponse<string>>
{
    public Task<ApiResponse<string>> Handle(GetStreetViewUrlQuery request, CancellationToken cancellationToken)
    {
        var url = streetViewService.BuildStreetViewUrl(new StreetViewRequestDto(
            request.Latitude,
            request.Longitude,
            request.Width,
            request.Height,
            request.Heading,
            request.Pitch,
            request.Fov));

        if (url is null)
            return Task.FromResult(ApiResponse<string>.FailResponse("Street View URL is unavailable."));

        return Task.FromResult(ApiResponse<string>.SuccessResponse(url));
    }
}

public record GetNearbyPlacesQuery(
    double Latitude,
    double Longitude,
    string Category,
    int RadiusMeters = 1500,
    int MaxResults = 8) : IRequest<ApiResponse<IReadOnlyList<NearbyPlaceDto>>>;

public class GetNearbyPlacesQueryHandler(IGooglePlacesNearbyService placesNearbyService)
    : IRequestHandler<GetNearbyPlacesQuery, ApiResponse<IReadOnlyList<NearbyPlaceDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<NearbyPlaceDto>>> Handle(
        GetNearbyPlacesQuery request, CancellationToken cancellationToken)
    {
        if (request.Latitude is < -90 or > 90 || request.Longitude is < -180 or > 180)
            return ApiResponse<IReadOnlyList<NearbyPlaceDto>>.FailResponse("Invalid coordinates.");

        if (string.IsNullOrWhiteSpace(request.Category))
            return ApiResponse<IReadOnlyList<NearbyPlaceDto>>.FailResponse("Category is required.");

        var outcome = await placesNearbyService.SearchNearbyAsync(
            request.Latitude,
            request.Longitude,
            request.Category,
            request.RadiusMeters,
            request.MaxResults,
            cancellationToken);

        if (outcome.Places is null)
            return ApiResponse<IReadOnlyList<NearbyPlaceDto>>.FailResponse(
                outcome.ErrorMessage
                ?? "Nearby places are unavailable. Ensure Places API (New) is enabled and the backend key uses IP restrictions (not HTTP referrers).");

        return ApiResponse<IReadOnlyList<NearbyPlaceDto>>.SuccessResponse(outcome.Places);
    }
}

public record GetNearbyPlacePhotoQuery(string PhotoResourceName, int MaxWidthPx = 800)
    : IRequest<ApiResponse<NearbyPlacePhotoDto>>;

public class GetNearbyPlacePhotoQueryHandler(IGooglePlacesPhotoService photoService)
    : IRequestHandler<GetNearbyPlacePhotoQuery, ApiResponse<NearbyPlacePhotoDto>>
{
    public async Task<ApiResponse<NearbyPlacePhotoDto>> Handle(
        GetNearbyPlacePhotoQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.PhotoResourceName))
            return ApiResponse<NearbyPlacePhotoDto>.FailResponse("Photo resource name is required.");

        var result = await photoService.ResolveMediaAsync(
            request.PhotoResourceName,
            request.MaxWidthPx,
            cancellationToken);

        if (result is null || string.IsNullOrWhiteSpace(result.PhotoUrl))
        {
            // Soft success with empty URL — FE keeps category icon; not a Nearby Search failure.
            return ApiResponse<NearbyPlacePhotoDto>.SuccessResponse(
                new NearbyPlacePhotoDto(null, Array.Empty<string>()));
        }

        return ApiResponse<NearbyPlacePhotoDto>.SuccessResponse(result);
    }
}
