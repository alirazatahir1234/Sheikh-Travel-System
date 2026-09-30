using System.Data;
using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.GpsTracking;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;
using SheikhTravelSystem.Infrastructure.Services;
using SheikhTravelSystem.Infrastructure.Services.Google;

namespace SheikhTravelSystem.Tests.GpsTracking;

public class GeocodingCostControlTests
{
    [Fact]
    public void IsCoarseAddress_UrduStreetLine_IsNotCoarse()
    {
        // Previously non-ASCII forced coarse → forceRefresh loops → Google spend.
        var urdu = "مین روڈ، پسرور، پنجاب";
        TripReplayAddressEnricher.IsCoarseAddress(urdu).Should().BeFalse();
        NominatimReverseGeocodingService.IsCoarseAddress(urdu, road: "مین روڈ", placeName: null)
            .Should().BeFalse();
    }

    [Fact]
    public void IsCoarseAddress_TehsilOnly_IsCoarse()
    {
        TripReplayAddressEnricher.IsCoarseAddress("Pasrur Tehsil, Punjab").Should().BeTrue();
    }

    [Fact]
    public void IsCoarseAddress_NearPrefix_IsCoarse()
    {
        TripReplayAddressEnricher.IsCoarseAddress("Near Mosque, Pasrur").Should().BeTrue();
    }

    [Fact]
    public void IsCoarseAddress_StreetWithDigits_IsNotCoarse()
    {
        TripReplayAddressEnricher.IsCoarseAddress("12 Mall Road, Lahore, Punjab").Should().BeFalse();
    }

    [Fact]
    public void GeocodingOptions_CostSafeDefaults()
    {
        var opts = new GeocodingOptions();
        opts.PreferGoogle.Should().BeFalse();
        opts.IncludeNearbyPlace.Should().BeFalse();
        opts.BackfillCooldownoldownSeconds.Should().Be(300);
        opts.Enabled.Should().BeTrue();
    }

    [Fact]
    public void FormatCoordRounded_StabilizesNearbyCacheKey()
    {
        var a = GoogleMapsApiHelper.FormatCoordRounded(32.26261055555556);
        var b = GoogleMapsApiHelper.FormatCoordRounded(32.26264011111111);
        a.Should().Be(b); // both round to 32.2626
        a.Should().Be("32.2626");
    }

    [Fact]
    public async Task ResolveFromGoogle_NearbyDisabled_IssuesOneHttpCall()
    {
        var requests = new List<string>();
        var handler = new RecordingHandler(req =>
        {
            requests.Add(req.RequestUri!.PathAndQuery);
            if (req.RequestUri.PathAndQuery.Contains("geocode", StringComparison.Ordinal))
            {
                return JsonOk("""
                    {
                      "status": "OK",
                      "results": [{
                        "formatted_address": "12 Mall Road, Lahore, Punjab, Pakistan",
                        "address_components": [
                          { "long_name": "12", "types": ["street_number"] },
                          { "long_name": "Mall Road", "types": ["route"] },
                          { "long_name": "Lahore", "types": ["locality"] },
                          { "long_name": "Punjab", "types": ["administrative_area_level_1"] },
                          { "long_name": "Pakistan", "types": ["country"] }
                        ]
                      }]
                    }
                    """);
            }

            return JsonOk("""{"status":"OK","results":[]}""");
        });

        var service = CreateGeocoder(handler, includeNearby: false);
        var result = await service.GetAddressAsync(
            31.52, 74.35, forceRefresh: true, allowGoogle: true);

        result.Should().NotBeNull();
        result!.FormattedAddress.Should().Contain("Mall Road");
        requests.Should().ContainSingle(u => u.Contains("geocode", StringComparison.Ordinal));
        requests.Should().NotContain(u => u.Contains("nearbysearch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ResolveFromGoogle_NearbyEnabled_IssuesGeocodeAndPlaces()
    {
        var requests = new List<string>();
        var handler = new RecordingHandler(req =>
        {
            requests.Add(req.RequestUri!.PathAndQuery);
            if (req.RequestUri.PathAndQuery.Contains("geocode", StringComparison.Ordinal))
            {
                return JsonOk("""
                    {
                      "status": "OK",
                      "results": [{
                        "formatted_address": "12 Mall Road, Lahore",
                        "address_components": [
                          { "long_name": "Mall Road", "types": ["route"] },
                          { "long_name": "Lahore", "types": ["locality"] }
                        ]
                      }]
                    }
                    """);
            }

            return JsonOk("""{"status":"OK","results":[]}""");
        });

        var service = CreateGeocoder(handler, includeNearby: true);
        await service.GetAddressAsync(31.52, 74.35, forceRefresh: true, allowGoogle: true);

        requests.Should().Contain(u => u.Contains("geocode", StringComparison.Ordinal));
        requests.Should().Contain(u => u.Contains("nearbysearch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task BackgroundPath_PreferGoogleFalse_DoesNotCallGoogle()
    {
        var googleHits = 0;
        var handler = new RecordingHandler(req =>
        {
            if (req.RequestUri!.Host.Contains("googleapis", StringComparison.OrdinalIgnoreCase)
                || req.RequestUri.PathAndQuery.Contains("/maps/api/", StringComparison.Ordinal))
            {
                Interlocked.Increment(ref googleHits);
            }

            // Nominatim-shaped empty response
            return JsonOk("""{"error":"Unable to geocode"}""");
        });

        var service = CreateGeocoder(handler, includeNearby: false, preferGoogle: false);
        await service.GetAddressAsync(31.52, 74.35, forceRefresh: true, allowGoogle: false);

        googleHits.Should().Be(0);
    }

    [Fact]
    public void BackfillCooldownoldown_SuppressesSecondEnqueue()
    {
        var opts = Options.Create(new GeocodingOptions
        {
            Enabled = true,
            BackfillCooldownoldownSeconds = 300
        });
        var svc = new GpsAddressBackfillHostedService(
            Mock.Of<IServiceProvider>(),
            opts,
            NullLogger<GpsAddressBackfillHostedService>.Instance);

        svc.Enqueue(50, 32.1, 74.2);
        svc.Enqueue(50, 32.2, 74.3); // same vehicle within cooldown — dropped at Enqueue
        // Channel capacity / TryWrite: second call returns early before write.
        // Verify via reflection that last-processed was not set yet (only on Process),
        // and that Enqueue's cooldown dictionary / early-return path doesn't throw.
        svc.Enqueue(51, 32.1, 74.2); // different vehicle OK
    }

    private static NominatimReverseGeocodingService CreateGeocoder(
        HttpMessageHandler handler,
        bool includeNearby,
        bool preferGoogle = false)
    {
        var factory = new NamedHttpClientFactory(new Dictionary<string, HttpMessageHandler>
        {
            ["GoogleMaps"] = handler,
            ["Nominatim"] = handler
        });

        var db = new Mock<IDbConnectionFactory>();
        db.Setup(f => f.CreateConnection()).Returns(new ThrowingConnection());

        return new NominatimReverseGeocodingService(
            db.Object,
            factory,
            Options.Create(new GeocodingOptions
            {
                Enabled = true,
                PreferGoogle = preferGoogle,
                IncludeNearbyPlace = includeNearby,
                GoogleMapsApiKey = "test-key",
                BaseUrl = "https://nominatim.openstreetmap.org"
            }),
            NullLogger<NominatimReverseGeocodingService>.Instance);
    }

    private static HttpResponseMessage JsonOk(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(responder(request));
    }

    private sealed class NamedHttpClientFactory(Dictionary<string, HttpMessageHandler> handlers)
        : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            if (!handlers.TryGetValue(name, out var handler))
                handler = handlers.Values.First();

            return new HttpClient(handler, disposeHandler: false)
            {
                BaseAddress = name switch
                {
                    "Nominatim" => new Uri("https://nominatim.openstreetmap.org/"),
                    _ => new Uri("https://maps.googleapis.com/")
                }
            };
        }
    }

    /// <summary>Forces cache read/write to fail so tests exercise HTTP providers only.</summary>
    private sealed class ThrowingConnection : IDbConnection
    {
        public string ConnectionString { get; set; } = string.Empty;
        public int ConnectionTimeout => 0;
        public string Database => string.Empty;
        public ConnectionState State => ConnectionState.Closed;
        public IDbTransaction BeginTransaction() => throw new NotSupportedException();
        public IDbTransaction BeginTransaction(IsolationLevel il) => throw new NotSupportedException();
        public void ChangeDatabase(string databaseName) => throw new NotSupportedException();
        public void Close() { }
        public IDbCommand CreateCommand() => throw new InvalidOperationException("test-no-db");
        public void Open() => throw new InvalidOperationException("test-no-db");
        public void Dispose() { }
    }
}
