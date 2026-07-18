using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.GpsTracking;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Queries;

public record ReverseGeocodeQuery(double Latitude, double Longitude)
    : IRequest<ApiResponse<ReverseGeocodeResult>>;

public class ReverseGeocodeQueryHandler(IReverseGeocodingService geocoder)
    : IRequestHandler<ReverseGeocodeQuery, ApiResponse<ReverseGeocodeResult>>
{
    public async Task<ApiResponse<ReverseGeocodeResult>> Handle(
        ReverseGeocodeQuery request, CancellationToken cancellationToken)
    {
        if (request.Latitude is < -90 or > 90 || request.Longitude is < -180 or > 180)
            return ApiResponse<ReverseGeocodeResult>.FailResponse("Invalid coordinates.");

        var result = await geocoder.GetAddressAsync(
            request.Latitude, request.Longitude, forceRefresh: false, cancellationToken);

        if (result is null)
            return ApiResponse<ReverseGeocodeResult>.FailResponse(
                "Address could not be resolved. Showing coordinates as fallback.");

        return ApiResponse<ReverseGeocodeResult>.SuccessResponse(result);
    }
}
