using System.Text;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Features.GpsTracking;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;

namespace SheikhTravelSystem.Infrastructure.Services.Google;

public sealed class GoogleStreetViewService(IOptions<GoogleMapsOptions> options) : IGoogleStreetViewService
{
    public string? BuildStreetViewUrl(StreetViewRequestDto request)
    {
        var credential = GoogleMapsApiHelper.ResolveServerKey(options);
        if (credential is null) return null;

        var width = Math.Clamp(request.Width, 1, 640);
        var height = Math.Clamp(request.Height, 1, 640);

        var sb = new StringBuilder("https://maps.googleapis.com/maps/api/streetview?");
        sb.Append("location=")
            .Append(GoogleMapsApiHelper.FormatCoord(request.Latitude))
            .Append(',')
            .Append(GoogleMapsApiHelper.FormatCoord(request.Longitude));
        sb.Append("&size=").Append(width).Append('x').Append(height);

        if (request.Heading is >= 0 and <= 360)
            sb.Append("&heading=").Append(request.Heading.Value);
        if (request.Pitch is >= -90 and <= 90)
            sb.Append("&pitch=").Append(request.Pitch.Value);
        if (request.Fov is >= 10 and <= 120)
            sb.Append("&fov=").Append(request.Fov.Value);

        sb.Append("&key=").Append(Uri.EscapeDataString(credential));
        return sb.ToString();
    }
}
