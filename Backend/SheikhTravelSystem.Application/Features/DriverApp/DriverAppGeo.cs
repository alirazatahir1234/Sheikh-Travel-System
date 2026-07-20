namespace SheikhTravelSystem.Application.Features.DriverApp;

internal static class DriverAppGeo
{
    internal static (double? Lat, double? Lng) ResolveCoords(double? lat, double? lng, string? placeHint)
    {
        if (lat.HasValue && lng.HasValue && !(lat.Value == 0 && lng.Value == 0))
            return (lat, lng);

        if (string.IsNullOrWhiteSpace(placeHint)) return (null, null);
        var source = placeHint.ToLowerInvariant();
        if (source.Contains("murree")) return (33.9070, 73.3943);
        if (source.Contains("islamabad")) return (33.6844, 73.0479);
        if (source.Contains("rawalpindi")) return (33.5651, 73.0169);
        if (source.Contains("lahore")) return (31.5204, 74.3587);
        if (source.Contains("sialkot")) return (32.4945, 74.5229);
        if (source.Contains("karachi")) return (24.8607, 67.0011);
        if (source.Contains("multan")) return (30.1575, 71.5249);
        if (source.Contains("faisalabad")) return (31.4504, 73.1350);
        if (source.Contains("peshawar")) return (34.0151, 71.5249);
        if (source.Contains("quetta")) return (30.1798, 66.9750);
        return (null, null);
    }

    internal static string? BuildGoogleMapsUrl(
        double? plat, double? plng, string? pAddr,
        double? dlat, double? dlng, string? dAddr)
    {
        static string? Format(double? lat, double? lng, string? address)
        {
            if (lat.HasValue && lng.HasValue)
                return string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{lat.Value},{lng.Value}");
            return string.IsNullOrWhiteSpace(address) ? null : address.Trim();
        }

        var origin = Format(plat, plng, pAddr);
        var dest = Format(dlat, dlng, dAddr);
        if (origin is null && dest is null) return null;
        if (dest is null) return $"https://www.google.com/maps/search/?api=1&query={Uri.EscapeDataString(origin!)}";
        if (origin is null) return $"https://www.google.com/maps/search/?api=1&query={Uri.EscapeDataString(dest)}";
        return $"https://www.google.com/maps/dir/?api=1&origin={Uri.EscapeDataString(origin)}&destination={Uri.EscapeDataString(dest)}";
    }
}
