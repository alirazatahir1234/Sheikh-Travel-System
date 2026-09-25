using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Features.GpsTracking;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;
using SheikhTravelSystem.Infrastructure.Services.Google;

namespace SheikhTravelSystem.Tests.GpsTracking;

public class GoogleMapsClientsTests
{
    private static IOptions<GoogleMapsOptions> MapsOptions(
        string? key,
        int cacheMinutes = 15,
        string? serverHttpReferer = null)
    {
        var opts = new GoogleMapsOptions
        {
            CacheMinutes = cacheMinutes,
            ServerKey = key,
            ServerHttpReferer = serverHttpReferer
        };
        return Options.Create(opts);
    }

    [Fact]
    public async Task GoogleRoutesService_ParsesComputeRoutesResponse()
    {
        const string json = """
            {
              "routes": [
                {
                  "distanceMeters": 12500,
                  "duration": "900s",
                  "polyline": { "encodedPolyline": "abc123" }
                }
              ]
            }
            """;

        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            });

        var factory = CreateFactory(("GoogleRoutes", handler));
        var service = new GoogleRoutesService(
            factory,
            MapsOptions(string.Concat("test", "-", "key")),
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<GoogleRoutesService>.Instance);

        var result = await service.ComputeRouteAsync(31.52, 74.35, 31.55, 74.40);

        result.Should().NotBeNull();
        result!.DistanceMeters.Should().Be(12500);
        result.DurationSeconds.Should().Be(900);
        result.EncodedPolyline.Should().Be("abc123");
    }

    [Fact]
    public void GoogleStaticMapsService_BuildsExpectedUrlShape()
    {
        var placeholder = string.Concat("backend", "-", "key");
        var service = new GoogleStaticMapsService(MapsOptions(placeholder));

        var url = service.BuildStaticMapUrl(new StaticMapRequestDto(
            CenterLatitude: 31.52,
            CenterLongitude: 74.35,
            Zoom: 14,
            Width: 640,
            Height: 480,
            Markers: [new LatLngPoint(31.52, 74.35)],
            Path: [new LatLngPoint(31.52, 74.35), new LatLngPoint(31.53, 74.36)]));

        url.Should().NotBeNull();
        url.Should().StartWith("https://maps.googleapis.com/maps/api/staticmap?");
        url.Should().Contain("center=31.52,74.35");
        url.Should().Contain("size=640x480");
        url.Should().Contain("markers=31.52,74.35");
        url.Should().Contain("path=color:0x0000ff|weight:3");
        url.Should().Contain("key=" + placeholder);
    }

    [Fact]
    public async Task GoogleRoutesService_ReturnsNullWhenApiKeyMissing()
    {
        var factory = CreateFactory(("GoogleRoutes", new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK))));

        var service = new GoogleRoutesService(
            factory,
            MapsOptions(string.Empty),
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<GoogleRoutesService>.Instance);

        var result = await service.ComputeRouteAsync(0, 0, 1, 1);
        result.Should().BeNull();
    }

    [Fact]
    public async Task GooglePlacesNearbyService_ParsesSearchNearbyResponse()
    {
        const string json = """
            {
              "places": [
                {
                  "id": "places/abc",
                  "displayName": { "text": "Shell Station" },
                  "formattedAddress": "GT Road, Gujranwala",
                  "location": { "latitude": 32.216, "longitude": 74.229 },
                  "types": ["gas_station"],
                  "rating": 4.2,
                  "userRatingCount": 88,
                  "currentOpeningHours": {
                    "openNow": true,
                    "weekdayDescriptions": ["Monday: 8:00 AM – 11:00 PM"]
                  },
                  "photos": [
                    {
                      "name": "places/abc/photos/photo-1",
                      "authorAttributions": [
                        { "displayName": "Test Photographer", "uri": "https://maps.google.com" }
                      ]
                    }
                  ]
                }
              ]
            }
            """;

        HttpRequestMessage? captured = null;
        var handler = new StubHttpMessageHandler(req =>
        {
            captured = req;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            };
        });

        var factory = CreateFactory(("GooglePlaces", handler));
        var service = CreateNearbyService(factory);

        var result = await service.SearchNearbyAsync(32.215, 74.228, "fuel", 1500, 8);

        result.Places.Should().NotBeNull();
        result.Places!.Should().HaveCount(1);
        result.Places[0].Name.Should().Be("Shell Station");
        result.Places[0].Category.Should().Be("fuel");
        result.Places[0].DistanceMeters.Should().NotBeNull();
        result.Places[0].OpenNow.Should().BeTrue();
        result.Places[0].OpeningStatus.Should().Contain("Open");
        result.Places[0].PhotoResourceName.Should().Be("places/abc/photos/photo-1");
        result.Places[0].PhotoAttributions.Should().ContainSingle("Test Photographer");
        // Null photo stub — URL enrich skipped; resource name still present for FE fallback.
        result.Places[0].PhotoUrl.Should().BeNull();
        captured!.Method.Should().Be(HttpMethod.Post);
        captured.RequestUri!.ToString().Should().Contain("places:searchNearby");
        captured.Headers.Contains("X-Goog-FieldMask").Should().BeTrue();
        var mask = captured.Headers.GetValues("X-Goog-FieldMask").Single();
        mask.Should().Contain("currentOpeningHours");
        mask.Should().Contain("places.photos");
        SerializePlaces(result.Places).Should().NotContain(string.Concat("test", "-", "key"));
    }

    [Fact]
    public async Task GooglePlacesNearbyService_MoreCategory_UsesCuratedTypes()
    {
        string? requestBody = null;
        var handler = new StubHttpMessageHandler(req =>
        {
            requestBody = req.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"places":[]}""", System.Text.Encoding.UTF8, "application/json")
            };
        });

        var factory = CreateFactory(("GooglePlaces", handler));
        var service = CreateNearbyService(factory);

        var result = await service.SearchNearbyAsync(32.215, 74.228, "more", 1500, 5);
        result.Places.Should().NotBeNull();
        result.Places.Should().BeEmpty();

        requestBody.Should().NotBeNullOrEmpty();
        requestBody.Should().Contain("convenience_store");
        requestBody.Should().Contain("pharmacy");
    }

    [Fact]
    public async Task GooglePlacesNearbyService_ReferrerBlocked_ReturnsActionableError()
    {
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.Forbidden)
            {
                Content = new StringContent(
                    """{"error":{"code":403,"message":"Requests from referer <empty> are blocked.","status":"PERMISSION_DENIED","details":[{"reason":"API_KEY_HTTP_REFERRER_BLOCKED"}]}}""",
                    System.Text.Encoding.UTF8,
                    "application/json")
            });

        var factory = CreateFactory(("GooglePlaces", handler));
        var service = CreateNearbyService(factory);

        var result = await service.SearchNearbyAsync(32.215, 74.228, "fuel", 1500, 8);
        result.Places.Should().BeNull();
        result.ErrorMessage.Should().Contain("HTTP referrer");
        result.ErrorMessage.Should().Contain("IP addresses");
        result.ErrorCode.Should().Be(NearbyPlacesErrorCodes.Authentication);
    }

    [Fact]
    public async Task GooglePlacesNearbyService_HospitalRequest_UsesIncludedTypesRadiusAndFieldMask()
    {
        string? requestBody = null;
        HttpRequestMessage? captured = null;
        var handler = new StubHttpMessageHandler(req =>
        {
            captured = req;
            requestBody = req.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"places":[]}""", System.Text.Encoding.UTF8, "application/json")
            };
        });

        var factory = CreateFactory(("GooglePlaces", handler));
        var service = CreateNearbyService(factory);

        var result = await service.SearchNearbyAsync(32.21527, 74.22597, "hospital", 5000, 20);

        result.Places.Should().NotBeNull();
        result.Places.Should().BeEmpty();
        result.ErrorCode.Should().BeNull();

        captured!.Method.Should().Be(HttpMethod.Post);
        captured.RequestUri!.ToString().Should().Contain("v1/places:searchNearby");
        captured.Headers.Contains("X-Goog-Api-Key").Should().BeTrue();
        captured.Headers.Contains("X-Goog-FieldMask").Should().BeTrue();
        var mask = captured.Headers.GetValues("X-Goog-FieldMask").Single();
        mask.Should().Contain("places.googleMapsUri");
        mask.Should().Contain("places.displayName");
        mask.Should().Contain("places.photos");

        requestBody.Should().NotBeNullOrEmpty();
        requestBody.Should().Contain("\"includedTypes\"");
        requestBody.Should().Contain("hospital");
        requestBody.Should().Contain("32.21527");
        requestBody.Should().Contain("74.22597");
        requestBody.Should().Contain("5000");
        requestBody.Should().NotContain("Hospital");
    }

    [Fact]
    public async Task GooglePlacesNearbyService_SendsConfiguredServerHttpReferer()
    {
        HttpRequestMessage? captured = null;
        var handler = new StubHttpMessageHandler(req =>
        {
            captured = req;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"places":[]}""", System.Text.Encoding.UTF8, "application/json")
            };
        });

        var factory = CreateFactory(("GooglePlaces", handler));
        var service = CreateNearbyService(
            factory,
            MapsOptions(string.Concat("test", "-", "key"), serverHttpReferer: "http://127.0.0.1:5082/"));

        await service.SearchNearbyAsync(32.21527, 74.22597, "hospital", 1500, 5);

        captured.Should().NotBeNull();
        captured!.Headers.Referrer.Should().Be(new Uri("http://127.0.0.1:5082/"));
    }

    [Fact]
    public async Task GooglePlacesNearbyService_MissingKey_ReturnsConfigurationError()
    {
        var factory = CreateFactory(("GooglePlaces", new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK))));

        var service = CreateNearbyService(factory, MapsOptions(string.Empty));

        var result = await service.SearchNearbyAsync(32.21527, 74.22597, "hospital", 5000, 10);
        result.Places.Should().BeNull();
        result.ErrorCode.Should().Be(NearbyPlacesErrorCodes.Configuration);
        result.ErrorMessage.Should().Contain("GoogleMaps");
        result.ErrorMessage.Should().Contain("user secrets");
    }

    [Fact]
    public async Task GooglePlacesNearbyService_MissingPhotos_LeavesPhotoFieldsNull()
    {
        const string json = """
            {
              "places": [
                {
                  "id": "places/no-photo",
                  "displayName": { "text": "No Photo Place" },
                  "location": { "latitude": 32.216, "longitude": 74.229 },
                  "types": ["hospital"]
                }
              ]
            }
            """;

        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            });

        var service = CreateNearbyService(CreateFactory(("GooglePlaces", handler)));

        var result = await service.SearchNearbyAsync(32.215, 74.228, "hospital", 1500, 5);
        result.Places.Should().ContainSingle();
        result.Places![0].PhotoResourceName.Should().BeNull();
        result.Places[0].PhotoUrl.Should().BeNull();
        result.Places[0].Name.Should().Be("No Photo Place");
        result.ErrorCode.Should().BeNull();
    }

    [Fact]
    public async Task GooglePlacesNearbyService_ResolvesPhotoUrlDuringSearch()
    {
        const string nearbyJson = """
            {
              "places": [
                {
                  "id": "places/abc",
                  "displayName": { "text": "Shell Station" },
                  "formattedAddress": "GT Road",
                  "location": { "latitude": 32.216, "longitude": 74.229 },
                  "types": ["gas_station"],
                  "googleMapsUri": "https://maps.google.com/?cid=1",
                  "photos": [{ "name": "places/abc/photos/photo-1" }]
                }
              ]
            }
            """;

        var handler = new StubHttpMessageHandler(req =>
        {
            var path = req.RequestUri?.AbsolutePath ?? "";
            if (path.Contains("searchNearby", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(nearbyJson, System.Text.Encoding.UTF8, "application/json")
                };
            }

            if (path.Contains("/media", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """{"name":"places/abc/photos/photo-1/media","photoUri":"https://lh3.googleusercontent.com/places/abc"}""",
                        System.Text.Encoding.UTF8,
                        "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var factory = CreateFactory(("GooglePlaces", handler));
        var cache = new MemoryCache(new MemoryCacheOptions());
        var opts = MapsOptions(string.Concat("test", "-", "key"));
        var photo = new GooglePlacesPhotoService(
            factory, opts, cache, NullLogger<GooglePlacesPhotoService>.Instance);
        var service = new GooglePlacesNearbyService(
            factory, opts, cache, photo, NullLogger<GooglePlacesNearbyService>.Instance);

        var result = await service.SearchNearbyAsync(32.215, 74.228, "fuel", 1500, 8);

        result.ErrorCode.Should().BeNull();
        result.Places.Should().ContainSingle();
        result.Places![0].PhotoResourceName.Should().Be("places/abc/photos/photo-1");
        result.Places[0].PhotoUrl.Should().Be("https://lh3.googleusercontent.com/places/abc");
        result.Places[0].GoogleMapsUri.Should().Be("https://maps.google.com/?cid=1");
        SerializePlaces(result.Places).Should().NotContain(string.Concat("test", "-", "key"));
        // Avoid literal Google key prefix in source (ADR-009 / secret scanners).
        var googleKeyPrefix = string.Concat("AI", "za");
        SerializePlaces(result.Places).Should().NotContain(googleKeyPrefix);
    }

    [Fact]
    public async Task GooglePlacesNearbyService_MediaFailure_LeavesPhotoUrlNull_PlaceStillOk()
    {
        const string nearbyJson = """
            {
              "places": [
                {
                  "id": "places/abc",
                  "displayName": { "text": "Shell Station" },
                  "location": { "latitude": 32.216, "longitude": 74.229 },
                  "types": ["gas_station"],
                  "photos": [{ "name": "places/abc/photos/photo-1" }]
                }
              ]
            }
            """;

        var handler = new StubHttpMessageHandler(req =>
        {
            var path = req.RequestUri?.AbsolutePath ?? "";
            if (path.Contains("searchNearby", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(nearbyJson, System.Text.Encoding.UTF8, "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.Forbidden)
            {
                Content = new StringContent("""{"error":{"message":"denied"}}""", System.Text.Encoding.UTF8, "application/json")
            };
        });

        var factory = CreateFactory(("GooglePlaces", handler));
        var cache = new MemoryCache(new MemoryCacheOptions());
        var opts = MapsOptions(string.Concat("test", "-", "key"));
        var photo = new GooglePlacesPhotoService(
            factory, opts, cache, NullLogger<GooglePlacesPhotoService>.Instance);
        var service = new GooglePlacesNearbyService(
            factory, opts, cache, photo, NullLogger<GooglePlacesNearbyService>.Instance);

        var result = await service.SearchNearbyAsync(32.215, 74.228, "fuel", 1500, 8);

        result.ErrorCode.Should().BeNull();
        result.Places.Should().ContainSingle();
        result.Places![0].Name.Should().Be("Shell Station");
        result.Places[0].PhotoResourceName.Should().Be("places/abc/photos/photo-1");
        result.Places[0].PhotoUrl.Should().BeNull();
    }

    [Fact]
    public async Task GooglePlacesPhotoService_ResolvesMediaUri()
    {
        HttpRequestMessage? captured = null;
        var handler = new StubHttpMessageHandler(req =>
        {
            captured = req;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"name":"places/test/photos/test-photo/media","photoUri":"https://lh3.googleusercontent.com/places/test"}""",
                    System.Text.Encoding.UTF8,
                    "application/json")
            };
        });

        var service = CreatePhotoService(CreateFactory(("GooglePlaces", handler)));

        var result = await service.ResolveMediaAsync("places/test/photos/test-photo", 800);

        result.Should().NotBeNull();
        result!.PhotoUrl.Should().Be("https://lh3.googleusercontent.com/places/test");
        captured!.Method.Should().Be(HttpMethod.Get);
        captured.RequestUri!.ToString().Should().Contain("v1/places/test/photos/test-photo/media");
        captured.RequestUri.ToString().Should().Contain("maxWidthPx=800");
        captured.RequestUri.ToString().Should().Contain("skipHttpRedirect=true");
        captured.Headers.Contains("X-Goog-Api-Key").Should().BeTrue();
    }

    [Fact]
    public async Task GooglePlacesPhotoService_RejectsInvalidResourceName()
    {
        var service = CreatePhotoService(CreateFactory(("GooglePlaces", new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)))));

        (await service.ResolveMediaAsync("https://evil.example/photo")).Should().BeNull();
        (await service.ResolveMediaAsync("../escape")).Should().BeNull();
        (await service.ResolveMediaAsync("places/only")).Should().BeNull();
    }

    [Fact]
    public async Task GooglePlacesPhotoService_MissingKey_ReturnsNull()
    {
        var service = CreatePhotoService(
            CreateFactory(("GooglePlaces", new StubHttpMessageHandler(_ =>
                new HttpResponseMessage(HttpStatusCode.OK)))),
            MapsOptions(string.Empty));

        var result = await service.ResolveMediaAsync("places/test/photos/test-photo");
        result.Should().BeNull();
    }

    private static GooglePlacesNearbyService CreateNearbyService(
        IHttpClientFactory factory,
        IOptions<GoogleMapsOptions>? options = null,
        IGooglePlacesPhotoService? photoService = null)
    {
        var opts = options ?? MapsOptions(string.Concat("test", "-", "key"));
        return new GooglePlacesNearbyService(
            factory,
            opts,
            new MemoryCache(new MemoryCacheOptions()),
            photoService ?? new NullPlacesPhotoService(),
            NullLogger<GooglePlacesNearbyService>.Instance);
    }

    private static GooglePlacesPhotoService CreatePhotoService(
        IHttpClientFactory factory,
        IOptions<GoogleMapsOptions>? options = null)
    {
        return new GooglePlacesPhotoService(
            factory,
            options ?? MapsOptions(string.Concat("test", "-", "key")),
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<GooglePlacesPhotoService>.Instance);
    }

    private static string SerializePlaces(IReadOnlyList<NearbyPlaceDto> places)
        => System.Text.Json.JsonSerializer.Serialize(places);

    private sealed class NullPlacesPhotoService : IGooglePlacesPhotoService
    {
        public Task<NearbyPlacePhotoDto?> ResolveMediaAsync(
            string photoResourceName,
            int maxWidthPx = 800,
            CancellationToken cancellationToken = default)
            => Task.FromResult<NearbyPlacePhotoDto?>(null);
    }

    private static IHttpClientFactory CreateFactory(params (string Name, HttpMessageHandler Handler)[] clients)
    {
        return new NamedHttpClientFactory(clients.ToDictionary(c => c.Name, c => c.Handler));
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(responder(request));
    }

    private sealed class NamedHttpClientFactory(Dictionary<string, HttpMessageHandler> handlers) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            if (!handlers.TryGetValue(name, out var handler))
                throw new InvalidOperationException($"No handler registered for {name}");

            return new HttpClient(handler, disposeHandler: false)
            {
                BaseAddress = name switch
                {
                    "GoogleRoutes" => new Uri("https://routes.googleapis.com/"),
                    "GoogleRouteOptimization" => new Uri("https://routeoptimization.googleapis.com/"),
                    "GooglePlaces" => new Uri("https://places.googleapis.com/"),
                    _ => new Uri("https://maps.googleapis.com/")
                }
            };
        }
    }
}
