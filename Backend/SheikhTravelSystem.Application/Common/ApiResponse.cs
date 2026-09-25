namespace SheikhTravelSystem.Application.Common;

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }
    public List<string>? Errors { get; set; }

    /// <summary>Machine-readable failure code (e.g. WINDOW_CLOSED).</summary>
    public string? Code { get; set; }

    public static ApiResponse<T> SuccessResponse(T data, string message = "Operation successful")
        => new() { Success = true, Message = message, Data = data };

    public static ApiResponse<T> FailResponse(string message, List<string>? errors = null, string? code = null)
        => new() { Success = false, Message = message, Errors = errors, Code = code };
}
