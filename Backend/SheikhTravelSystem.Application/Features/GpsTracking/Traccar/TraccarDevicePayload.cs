namespace SheikhTravelSystem.Application.Features.GpsTracking.Traccar;

public record TraccarDevicePayload(
    string Name,
    string UniqueId,
    string? Category = null,
    string? Phone = null,
    string? Model = null,
    string? Contact = null,
    bool Disabled = false);

public record TraccarUpdateDevicePayload(
    int Id,
    string Name,
    string UniqueId,
    string? Category = null,
    string? Phone = null,
    string? Model = null,
    string? Contact = null,
    bool Disabled = false);

public record TraccarClientResult<T>(bool Success, T? Value, string? ErrorMessage, int? StatusCode = null)
{
    public static TraccarClientResult<T> Ok(T value) => new(true, value, null);
    public static TraccarClientResult<T> Fail(string message, int? statusCode = null) => new(false, default, message, statusCode);
}
