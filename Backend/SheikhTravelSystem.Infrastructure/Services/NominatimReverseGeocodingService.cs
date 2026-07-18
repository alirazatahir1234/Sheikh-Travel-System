using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.GpsTracking;

namespace SheikhTravelSystem.Infrastructure.Services;

/// <summary>
/// Nominatim-backed reverse geocoder with a SQL cache keyed to ~11 m grid cells.
/// Throttled to 1 req/sec per Nominatim usage policy.
/// </summary>
public sealed class NominatimReverseGeocodingService(
    IDbConnectionFactory dbFactory,
    IHttpClientFactory httpClientFactory,
    IOptions<GeocodingOptions> options,
    ILogger<NominatimReverseGeocodingService> logger) : IReverseGeocodingService
{
    private const int CoordinateDecimals = 4;
    private static readonly TimeSpan ThrottleInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan CacheTtl = TimeSpan.FromDays(30);

    private readonly object _throttleLock = new();
    private DateTime _nextAllowedCallUtc = DateTime.MinValue;

    public async Task<ReverseGeocodeResult?> GetAddressAsync(
        double latitude,
        double longitude,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        if (!options.Value.Enabled)
            return null;

        var latKey = Math.Round(latitude, CoordinateDecimals, MidpointRounding.AwayFromZero);
        var lngKey = Math.Round(longitude, CoordinateDecimals, MidpointRounding.AwayFromZero);

        using var connection = dbFactory.CreateConnection();

        if (!forceRefresh)
        {
            var cached = await connection.QuerySingleOrDefaultAsync<CachedRow>(new CommandDefinition("""
                SELECT TOP 1 Address, City, State, Country, PostalCode, Road, ResolvedAt
                FROM GpsAddressCache
                WHERE LatitudeKey = @LatKey AND LongitudeKey = @LngKey
                """,
                new { LatKey = latKey, LngKey = lngKey },
                cancellationToken: cancellationToken));

            if (cached is not null
                && !string.IsNullOrWhiteSpace(cached.Address)
                && DateTime.UtcNow - cached.ResolvedAt < CacheTtl)
            {
                return new ReverseGeocodeResult(
                    cached.Address,
                    cached.Road,
                    cached.City,
                    cached.State,
                    cached.Country,
                    cached.PostalCode,
                    FromCache: true);
            }
        }

        var resolved = await ResolveFromNominatimAsync(latitude, longitude, cancellationToken);
        if (resolved is null || string.IsNullOrWhiteSpace(resolved.FormattedAddress))
            return null;

        await connection.ExecuteAsync(new CommandDefinition("""
            MERGE GpsAddressCache AS target
            USING (SELECT @LatKey AS LatitudeKey, @LngKey AS LongitudeKey) AS source
            ON target.LatitudeKey = source.LatitudeKey AND target.LongitudeKey = source.LongitudeKey
            WHEN MATCHED THEN UPDATE SET
                Address = @Address, Road = @Road, City = @City, State = @State,
                Country = @Country, PostalCode = @PostalCode, ResolvedAt = @ResolvedAt
            WHEN NOT MATCHED THEN INSERT
                (LatitudeKey, LongitudeKey, Address, Road, City, State, Country, PostalCode, ResolvedAt)
                VALUES (@LatKey, @LngKey, @Address, @Road, @City, @State, @Country, @PostalCode, @ResolvedAt);
            """,
            new
            {
                LatKey = latKey,
                LngKey = lngKey,
                Address = resolved.FormattedAddress,
                resolved.Road,
                resolved.City,
                resolved.State,
                resolved.Country,
                resolved.PostalCode,
                ResolvedAt = DateTime.UtcNow
            },
            cancellationToken: cancellationToken));

        return resolved with { FromCache = false };
    }

    private async Task<ReverseGeocodeResult?> ResolveFromNominatimAsync(
        double latitude, double longitude, CancellationToken cancellationToken)
    {
        TimeSpan wait;
        lock (_throttleLock)
        {
            wait = _nextAllowedCallUtc - DateTime.UtcNow;
            if (wait < TimeSpan.Zero) wait = TimeSpan.Zero;
            _nextAllowedCallUtc = DateTime.UtcNow.Add(ThrottleInterval) + wait;
        }

        if (wait > TimeSpan.Zero)
            await Task.Delay(wait, cancellationToken);

        var client = httpClientFactory.CreateClient("Nominatim");
        var url =
            $"/reverse?format=jsonv2&lat={latitude.ToString(CultureInfo.InvariantCulture)}" +
            $"&lon={longitude.ToString(CultureInfo.InvariantCulture)}" +
            "&zoom=18&addressdetails=1&accept-language=en";

        try
        {
            var response = await client.GetFromJsonAsync<NominatimReverseResponse>(url, cancellationToken);
            if (response is null || string.IsNullOrWhiteSpace(response.DisplayName))
                return null;

            var a = response.Address;
            var road = FirstNonEmpty(a?.Road, a?.Pedestrian, a?.Path, a?.Neighbourhood, a?.Suburb);
            var city = FirstNonEmpty(a?.City, a?.Town, a?.Village, a?.Municipality, a?.County);
            var state = FirstNonEmpty(a?.State, a?.Province, a?.Region);
            var country = a?.Country;
            var postal = a?.Postcode;

            var shortLine = string.Join(", ", new[] { road, city, state, country }
                .Where(s => !string.IsNullOrWhiteSpace(s)));

            return new ReverseGeocodeResult(
                string.IsNullOrWhiteSpace(shortLine) ? response.DisplayName : shortLine,
                road,
                city,
                state,
                country,
                postal);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Nominatim reverse geocode failed for {Lat},{Lng}", latitude, longitude);
            return null;
        }
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    private sealed record CachedRow(
        string Address, string? City, string? State, string? Country, string? PostalCode, string? Road, DateTime ResolvedAt);

    private sealed record NominatimReverseResponse(
        [property: JsonPropertyName("display_name")] string? DisplayName,
        [property: JsonPropertyName("address")] NominatimAddress? Address);

    private sealed record NominatimAddress(
        [property: JsonPropertyName("road")] string? Road,
        [property: JsonPropertyName("pedestrian")] string? Pedestrian,
        [property: JsonPropertyName("path")] string? Path,
        [property: JsonPropertyName("neighbourhood")] string? Neighbourhood,
        [property: JsonPropertyName("suburb")] string? Suburb,
        [property: JsonPropertyName("city")] string? City,
        [property: JsonPropertyName("town")] string? Town,
        [property: JsonPropertyName("village")] string? Village,
        [property: JsonPropertyName("municipality")] string? Municipality,
        [property: JsonPropertyName("county")] string? County,
        [property: JsonPropertyName("state")] string? State,
        [property: JsonPropertyName("province")] string? Province,
        [property: JsonPropertyName("region")] string? Region,
        [property: JsonPropertyName("country")] string? Country,
        [property: JsonPropertyName("postcode")] string? Postcode);
}
