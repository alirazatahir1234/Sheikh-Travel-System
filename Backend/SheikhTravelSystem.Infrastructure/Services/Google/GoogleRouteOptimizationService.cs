using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Features.GpsTracking;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;

namespace SheikhTravelSystem.Infrastructure.Services.Google;

public sealed class GoogleRouteOptimizationService(
    IHttpClientFactory httpClientFactory,
    IOptions<GoogleMapsOptions> options,
    ILogger<GoogleRouteOptimizationService> logger) : IGoogleRouteOptimizationService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<OptimizeDispatchToursResultDto?> OptimizeToursAsync(
        OptimizeDispatchToursRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var credential = GoogleMapsApiHelper.ResolveServerKey(options);
        if (credential is null || request.Vehicles.Count == 0 || request.Shipments.Count == 0)
            return null;

        var projectId = request.ProjectId?.Trim();
        if (string.IsNullOrWhiteSpace(projectId))
            return null;

        try
        {
            var body = BuildRequestBody(request);
            var client = httpClientFactory.CreateClient("GoogleRouteOptimization");
            using var httpRequest = new HttpRequestMessage(
                HttpMethod.Post,
                $"/v1/projects/{Uri.EscapeDataString(projectId)}:optimizeTours")
            {
                Content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json")
            };
            httpRequest.Headers.Add("X-Goog-Api-Key", credential);

            using var response = await client.SendAsync(httpRequest, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Google Route Optimization API returned {StatusCode}", response.StatusCode);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var payload = await JsonSerializer.DeserializeAsync<OptimizeToursResponse>(stream, JsonOptions, cancellationToken);
            if (payload?.Routes is not { Count: > 0 })
                return null;

            var tours = new List<OptimizedTourDto>();
            foreach (var route in payload.Routes)
            {
                var vehicleLabel = route.VehicleLabel ?? route.VehicleIndex?.ToString() ?? "vehicle";
                var visits = new List<OptimizedTourVisitDto>();
                var sequence = 0;
                if (route.Visits is not null)
                {
                    foreach (var visit in route.Visits)
                    {
                        var label = visit.ShipmentLabel
                            ?? visit.ShipmentIndex?.ToString()
                            ?? $"stop-{sequence + 1}";
                        visits.Add(new OptimizedTourVisitDto(label, ++sequence));
                    }
                }

                tours.Add(new OptimizedTourDto(vehicleLabel, visits));
            }

            return new OptimizeDispatchToursResultDto(tours);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Google Route Optimization API call failed");
            return null;
        }
    }

    private static OptimizeToursRequestBody BuildRequestBody(OptimizeDispatchToursRequestDto request)
    {
        var vehicles = request.Vehicles.Select((v, i) => new OptimizeVehicleBody
        {
            Label = string.IsNullOrWhiteSpace(v.Label) ? $"vehicle-{i + 1}" : v.Label,
            StartLocation = LatLng(v.StartLatitude, v.StartLongitude),
            EndLocation = LatLng(v.EndLatitude, v.EndLongitude),
            LoadLimits = v.Capacity is > 0
                ? new Dictionary<string, OptimizeLoadLimitBody>
                {
                    ["weight"] = new OptimizeLoadLimitBody { MaxLoad = v.Capacity.Value.ToString() }
                }
                : null
        }).ToList();

        var shipments = request.Shipments.Select((s, i) => new OptimizeShipmentBody
        {
            Label = string.IsNullOrWhiteSpace(s.Label) ? $"shipment-{i + 1}" : s.Label,
            Deliveries =
            [
                new OptimizeVisitRequestBody
                {
                    ArrivalLocation = LatLng(s.Latitude, s.Longitude),
                    Duration = $"{Math.Max(0, s.ServiceDurationSeconds)}s"
                }
            ],
            LoadDemands = s.LoadDemand is > 0
                ? new Dictionary<string, OptimizeLoadLimitBody>
                {
                    ["weight"] = new OptimizeLoadLimitBody { Amount = s.LoadDemand.Value.ToString() }
                }
                : null
        }).ToList();

        return new OptimizeToursRequestBody
        {
            Model = new OptimizeModelBody
            {
                Vehicles = vehicles,
                Shipments = shipments
            }
        };
    }

    private static OptimizeLatLngBody LatLng(double lat, double lng) => new()
    {
        Latitude = lat,
        Longitude = lng
    };

    private sealed class OptimizeToursRequestBody
    {
        public OptimizeModelBody Model { get; set; } = null!;
    }

    private sealed class OptimizeModelBody
    {
        public List<OptimizeVehicleBody> Vehicles { get; set; } = [];
        public List<OptimizeShipmentBody> Shipments { get; set; } = [];
    }

    private sealed class OptimizeVehicleBody
    {
        public string Label { get; set; } = string.Empty;
        public OptimizeLatLngBody StartLocation { get; set; } = null!;
        public OptimizeLatLngBody EndLocation { get; set; } = null!;
        public Dictionary<string, OptimizeLoadLimitBody>? LoadLimits { get; set; }
    }

    private sealed class OptimizeShipmentBody
    {
        public string Label { get; set; } = string.Empty;
        public List<OptimizeVisitRequestBody> Deliveries { get; set; } = [];
        public Dictionary<string, OptimizeLoadLimitBody>? LoadDemands { get; set; }
    }

    private sealed class OptimizeVisitRequestBody
    {
        public OptimizeLatLngBody ArrivalLocation { get; set; } = null!;
        public string Duration { get; set; } = "300s";
    }

    private sealed class OptimizeLatLngBody
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }

    private sealed class OptimizeLoadLimitBody
    {
        public string? MaxLoad { get; set; }
        public string? Amount { get; set; }
    }

    private sealed class OptimizeToursResponse
    {
        public List<OptimizeRouteResponse>? Routes { get; set; }
    }

    private sealed class OptimizeRouteResponse
    {
        public string? VehicleLabel { get; set; }
        public int? VehicleIndex { get; set; }
        public List<OptimizeVisitResponse>? Visits { get; set; }
    }

    private sealed class OptimizeVisitResponse
    {
        public string? ShipmentLabel { get; set; }
        public int? ShipmentIndex { get; set; }
    }
}
