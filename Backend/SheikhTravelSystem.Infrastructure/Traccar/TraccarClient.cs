using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Features.GpsTracking.Traccar;

namespace SheikhTravelSystem.Infrastructure.Traccar;

public class TraccarClient(
    HttpClient http,
    IOptions<TraccarOptions> options,
    ILogger<TraccarClient> logger) : ITraccarClient
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

    public async Task<TraccarDevice?> GetDeviceByUniqueIdAsync(string uniqueId, CancellationToken ct = default)
    {
        var devices = await GetDevicesAsync(ct);
        return devices.FirstOrDefault(d =>
            string.Equals(d.UniqueId, uniqueId, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<TraccarClientResult<TraccarDevice>> CreateDeviceAsync(TraccarDevicePayload payload, CancellationToken ct = default)
    {
        var uri = ResolveRequestUri("/api/devices");
        if (uri is null)
            return TraccarClientResult<TraccarDevice>.Fail("Traccar is not configured. Set Traccar:BaseUrl and credentials.");

        try
        {
            var body = new
            {
                name = payload.Name,
                uniqueId = payload.UniqueId,
                category = payload.Category,
                phone = payload.Phone,
                model = payload.Model,
                contact = payload.Contact,
                disabled = payload.Disabled
            };
            var response = await http.PostAsJsonAsync(uri, body, ct);
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync(ct);
                logger.LogWarning("Traccar CreateDevice failed: {Status} {Body}", response.StatusCode, err);
                return TraccarClientResult<TraccarDevice>.Fail(
                    $"Traccar device creation failed ({(int)response.StatusCode}).", (int)response.StatusCode);
            }

            var device = await response.Content.ReadFromJsonAsync<TraccarDevice>(JsonOpts, ct);
            return device is null
                ? TraccarClientResult<TraccarDevice>.Fail("Traccar returned an empty response.")
                : TraccarClientResult<TraccarDevice>.Ok(device);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Traccar CreateDevice exception");
            return TraccarClientResult<TraccarDevice>.Fail(ex.Message);
        }
    }

    public async Task<TraccarClientResult<TraccarDevice>> UpdateDeviceAsync(TraccarUpdateDevicePayload payload, CancellationToken ct = default)
    {
        var uri = ResolveRequestUri($"/api/devices/{payload.Id}");
        if (uri is null)
            return TraccarClientResult<TraccarDevice>.Fail("Traccar is not configured. Set Traccar:BaseUrl and credentials.");

        try
        {
            var body = new
            {
                id = payload.Id,
                name = payload.Name,
                uniqueId = payload.UniqueId,
                category = payload.Category,
                phone = payload.Phone,
                model = payload.Model,
                contact = payload.Contact,
                disabled = payload.Disabled
            };
            var response = await http.PutAsJsonAsync(uri, body, ct);
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync(ct);
                logger.LogWarning("Traccar UpdateDevice failed: {Status} {Body}", response.StatusCode, err);
                return TraccarClientResult<TraccarDevice>.Fail(
                    $"Traccar device update failed ({(int)response.StatusCode}).", (int)response.StatusCode);
            }

            var device = await response.Content.ReadFromJsonAsync<TraccarDevice>(JsonOpts, ct);
            return device is null
                ? TraccarClientResult<TraccarDevice>.Ok(new TraccarDevice(
                    payload.Id, payload.Name, payload.UniqueId, "unknown",
                    payload.Category, payload.Phone, payload.Model, payload.Contact, payload.Disabled, null))
                : TraccarClientResult<TraccarDevice>.Ok(device);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Traccar UpdateDevice exception");
            return TraccarClientResult<TraccarDevice>.Fail(ex.Message);
        }
    }

    public async Task<TraccarDevice?> CreateDeviceAsync(string name, string uniqueId, string? category = null, CancellationToken ct = default)
    {
        var result = await CreateDeviceAsync(new TraccarDevicePayload(name, uniqueId, category), ct);
        return result.Success ? result.Value : null;
    }

    public async Task<bool> UpdateDeviceAsync(int deviceId, string name, string uniqueId, bool disabled, CancellationToken ct = default)
    {
        var result = await UpdateDeviceAsync(new TraccarUpdateDevicePayload(deviceId, name, uniqueId, Disabled: disabled), ct);
        return result.Success;
    }

    public async Task<bool> DeleteDeviceAsync(int deviceId, CancellationToken ct = default)
    {
        var uri = ResolveRequestUri($"/api/devices/{deviceId}");
        if (uri is null) return false;

        try
        {
            var response = await http.DeleteAsync(uri, ct);
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
        var uri = ResolveRequestUri("/api/geofences");
        if (uri is null) return null;

        try
        {
            var payload = new { name, area, description };
            var response = await http.PostAsJsonAsync(uri, payload, ct);
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
        var uri = ResolveRequestUri($"/api/geofences/{geofenceId}");
        if (uri is null) return false;

        try
        {
            var payload = new { id = geofenceId, name, area };
            var response = await http.PutAsJsonAsync(uri, payload, ct);
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
        var uri = ResolveRequestUri($"/api/geofences/{geofenceId}");
        if (uri is null) return false;

        try
        {
            var response = await http.DeleteAsync(uri, ct);
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
        var uri = ResolveRequestUri("/api/commands/send");
        if (uri is null) return false;

        try
        {
            var payload = new { deviceId, type };
            var response = await http.PostAsJsonAsync(uri, payload, ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Traccar SendCommand exception");
            return false;
        }
    }

    private Uri? ResolveRequestUri(string path)
    {
        if (http.BaseAddress is not null)
            return new Uri(http.BaseAddress, path.TrimStart('/'));

        if (!options.Value.TryGetBaseUri(out var baseUri) || baseUri is null)
        {
            logger.LogWarning(
                "Traccar request skipped — BaseUrl is not configured. Set Traccar:BaseUrl (e.g. http://20.174.1.230:8082). Path: {Path}",
                path);
            return null;
        }

        return new Uri(baseUri, path.TrimStart('/'));
    }

    private async Task<T?> GetAsync<T>(string path, CancellationToken ct)
    {
        var uri = ResolveRequestUri(path);
        if (uri is null)
            return default;

        try
        {
            var response = await http.GetAsync(uri, ct);
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
        var uri = ResolveRequestUri(path);
        if (uri is null)
            return Array.Empty<T>();

        try
        {
            var response = await http.GetAsync(uri, ct);
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
