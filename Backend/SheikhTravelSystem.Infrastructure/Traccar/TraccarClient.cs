using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Features.GpsTracking.Traccar;

namespace SheikhTravelSystem.Infrastructure.Traccar;

public class TraccarClient(HttpClient http, ILogger<TraccarClient> logger) : ITraccarClient
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<TraccarServer?> GetServerAsync(CancellationToken ct = default)
        => await GetAsync<TraccarServer>("/api/server", ct);

    public async Task<IReadOnlyList<TraccarDevice>> GetDevicesAsync(CancellationToken ct = default)
        => await GetListAsync<TraccarDevice>("/api/devices", ct);

    public async Task<TraccarDevice?> GetDeviceByIdAsync(int deviceId, CancellationToken ct = default)
    {
        var list = await GetListAsync<TraccarDevice>($"/api/devices?id={deviceId}", ct);
        return list.FirstOrDefault();
    }

    public async Task<TraccarDevice?> CreateDeviceAsync(string name, string uniqueId, string? category = null, CancellationToken ct = default)
    {
        try
        {
            var payload = new { name, uniqueId, category, disabled = false };
            var response = await http.PostAsJsonAsync("/api/devices", payload, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Traccar CreateDevice failed: {Status}", response.StatusCode);
                return null;
            }
            return await response.Content.ReadFromJsonAsync<TraccarDevice>(JsonOpts, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Traccar CreateDevice exception");
            return null;
        }
    }

    public async Task<bool> UpdateDeviceAsync(int deviceId, string name, string uniqueId, bool disabled, CancellationToken ct = default)
    {
        try
        {
            var payload = new { id = deviceId, name, uniqueId, disabled };
            var response = await http.PutAsJsonAsync($"/api/devices/{deviceId}", payload, ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Traccar UpdateDevice exception");
            return false;
        }
    }

    public async Task<bool> DeleteDeviceAsync(int deviceId, CancellationToken ct = default)
    {
        try
        {
            var response = await http.DeleteAsync($"/api/devices/{deviceId}", ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Traccar DeleteDevice exception");
            return false;
        }
    }

    public async Task<IReadOnlyList<TraccarPosition>> GetLivePositionsAsync(CancellationToken ct = default)
        => await GetListAsync<TraccarPosition>("/api/positions", ct);

    public async Task<IReadOnlyList<TraccarPosition>> GetPositionsByDeviceAsync(int deviceId, DateTime from, DateTime to, CancellationToken ct = default)
    {
        var f = Uri.EscapeDataString(from.ToString("yyyy-MM-ddTHH:mm:ssZ"));
        var t = Uri.EscapeDataString(to.ToString("yyyy-MM-ddTHH:mm:ssZ"));
        return await GetListAsync<TraccarPosition>($"/api/positions?deviceId={deviceId}&from={f}&to={t}", ct);
    }

    public async Task<IReadOnlyList<TraccarGeofence>> GetGeofencesAsync(CancellationToken ct = default)
        => await GetListAsync<TraccarGeofence>("/api/geofences", ct);

    public async Task<TraccarGeofence?> CreateGeofenceAsync(string name, string area, string? description = null, CancellationToken ct = default)
    {
        try
        {
            var payload = new { name, area, description };
            var response = await http.PostAsJsonAsync("/api/geofences", payload, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Traccar CreateGeofence failed: {Status}", response.StatusCode);
                return null;
            }
            return await response.Content.ReadFromJsonAsync<TraccarGeofence>(JsonOpts, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Traccar CreateGeofence exception");
            return null;
        }
    }

    public async Task<bool> UpdateGeofenceAsync(int geofenceId, string name, string area, CancellationToken ct = default)
    {
        try
        {
            var payload = new { id = geofenceId, name, area };
            var response = await http.PutAsJsonAsync($"/api/geofences/{geofenceId}", payload, ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Traccar UpdateGeofence exception");
            return false;
        }
    }

    public async Task<bool> DeleteGeofenceAsync(int geofenceId, CancellationToken ct = default)
    {
        try
        {
            var response = await http.DeleteAsync($"/api/geofences/{geofenceId}", ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Traccar DeleteGeofence exception");
            return false;
        }
    }

    public async Task<IReadOnlyList<TraccarTrip>> GetTripsAsync(int deviceId, DateTime from, DateTime to, CancellationToken ct = default)
    {
        var f = Uri.EscapeDataString(from.ToString("yyyy-MM-ddTHH:mm:ssZ"));
        var t = Uri.EscapeDataString(to.ToString("yyyy-MM-ddTHH:mm:ssZ"));
        return await GetListAsync<TraccarTrip>($"/api/reports/trips?deviceId={deviceId}&from={f}&to={t}", ct);
    }

    public async Task<IReadOnlyList<TraccarStop>> GetStopsAsync(int deviceId, DateTime from, DateTime to, CancellationToken ct = default)
    {
        var f = Uri.EscapeDataString(from.ToString("yyyy-MM-ddTHH:mm:ssZ"));
        var t = Uri.EscapeDataString(to.ToString("yyyy-MM-ddTHH:mm:ssZ"));
        return await GetListAsync<TraccarStop>($"/api/reports/stops?deviceId={deviceId}&from={f}&to={t}", ct);
    }

    public async Task<IReadOnlyList<TraccarEvent>> GetEventsAsync(int deviceId, DateTime from, DateTime to, string? type = null, CancellationToken ct = default)
    {
        var f = Uri.EscapeDataString(from.ToString("yyyy-MM-ddTHH:mm:ssZ"));
        var t = Uri.EscapeDataString(to.ToString("yyyy-MM-ddTHH:mm:ssZ"));
        var typeParam = type is null ? "" : $"&type={Uri.EscapeDataString(type)}";
        return await GetListAsync<TraccarEvent>($"/api/reports/events?deviceId={deviceId}&from={f}&to={t}{typeParam}", ct);
    }

    public async Task<IReadOnlyList<TraccarSummary>> GetSummaryAsync(int deviceId, DateTime from, DateTime to, CancellationToken ct = default)
    {
        var f = Uri.EscapeDataString(from.ToString("yyyy-MM-ddTHH:mm:ssZ"));
        var t = Uri.EscapeDataString(to.ToString("yyyy-MM-ddTHH:mm:ssZ"));
        return await GetListAsync<TraccarSummary>($"/api/reports/summary?deviceId={deviceId}&from={f}&to={t}", ct);
    }

    public async Task<bool> SendCommandAsync(int deviceId, string type, CancellationToken ct = default)
    {
        try
        {
            var payload = new { deviceId, type };
            var response = await http.PostAsJsonAsync("/api/commands/send", payload, ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Traccar SendCommand exception");
            return false;
        }
    }

    private async Task<T?> GetAsync<T>(string path, CancellationToken ct)
    {
        try
        {
            var response = await http.GetAsync(path, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Traccar GET {Path} returned {Status}", path, response.StatusCode);
                return default;
            }
            return await response.Content.ReadFromJsonAsync<T>(JsonOpts, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Traccar GET {Path} exception", path);
            return default;
        }
    }

    private async Task<IReadOnlyList<T>> GetListAsync<T>(string path, CancellationToken ct)
    {
        try
        {
            var response = await http.GetAsync(path, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Traccar GET {Path} returned {Status}", path, response.StatusCode);
                return Array.Empty<T>();
            }
            return await response.Content.ReadFromJsonAsync<List<T>>(JsonOpts, ct) ?? [];
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Traccar GET {Path} exception", path);
            return Array.Empty<T>();
        }
    }
}
