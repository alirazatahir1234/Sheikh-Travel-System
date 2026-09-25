using System.Text;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Features.GpsTracking;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;

namespace SheikhTravelSystem.Infrastructure.Services.Google;

public sealed class GoogleStaticMapsService(IOptions<GoogleMapsOptions> options) : IGoogleStaticMapsService
{
    public string? BuildStaticMapUrl(StaticMapRequestDto request)
    {
        var credential = GoogleMapsApiHelper.ResolveServerKey(options);
        if (credential is null) return null;

        var width = Math.Clamp(request.Width, 1, 640);
        var height = Math.Clamp(request.Height, 1, 640);
        var zoom = Math.Clamp(request.Zoom, 0, 21);

        var sb = new StringBuilder("https://maps.googleapis.com/maps/api/staticmap?");
        sb.Append("center=")
            .Append(GoogleMapsApiHelper.FormatCoord(request.CenterLatitude))
            .Append(',')
            .Append(GoogleMapsApiHelper.FormatCoord(request.CenterLongitude));
        sb.Append("&zoom=").Append(zoom);
        sb.Append("&size=").Append(width).Append('x').Append(height);
        sb.Append("&maptype=roadmap");

        if (request.Markers is { Count: > 0 })
        {
            foreach (var marker in request.Markers)
            {
                sb.Append("&markers=")
                    .Append(GoogleMapsApiHelper.FormatCoord(marker.Latitude))
                    .Append(',')
                    .Append(GoogleMapsApiHelper.FormatCoord(marker.Longitude));
            }
        }

        if (request.Path is { Count: > 1 })
        {
            sb.Append("&path=color:0x0000ff|weight:3");
            foreach (var point in request.Path)
            {
                sb.Append('|')
                    .Append(GoogleMapsApiHelper.FormatCoord(point.Latitude))
                    .Append(',')
                    .Append(GoogleMapsApiHelper.FormatCoord(point.Longitude));
            }
        }

        sb.Append("&key=").Append(Uri.EscapeDataString(credential));
        return sb.ToString();
    }
}
