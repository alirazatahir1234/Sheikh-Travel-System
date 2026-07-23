using System.Net;
using System.Text.Json;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;

namespace SheikhTravelSystem.API.Middleware;

/// <summary>
/// Catches unhandled exceptions and returns consistent API error responses.
/// </summary>
public class GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
{
    /// <summary>
    /// Invokes the next middleware and centralizes exception handling.
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        logger.LogError(exception, "Unhandled exception: {Message}", exception.Message);

        // Map domain/application exceptions to client-safe status codes/messages.
        var (statusCode, message) = exception switch
        {
            NotFoundException => (HttpStatusCode.NotFound, exception.Message),
            ConflictException => (HttpStatusCode.Conflict, exception.Message),
            ForbiddenException => (HttpStatusCode.Forbidden, exception.Message),
            FluentValidation.ValidationException validationEx => (HttpStatusCode.BadRequest, FormatValidationMessage(validationEx)),
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized, "Unauthorized"),
            Microsoft.Data.SqlClient.SqlException sqlEx when IsSqlConnectivityFailure(sqlEx)
                => (HttpStatusCode.ServiceUnavailable,
                    "Database is unreachable. Check SQL Server connectivity (host/firewall/VPN) and restart the API."),
            _ => (HttpStatusCode.InternalServerError, "An unexpected error occurred.")
        };

        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json";

        var response = ApiResponse<object>.FailResponse(message);
        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        await context.Response.WriteAsync(json);
    }

    private static string FormatValidationMessage(FluentValidation.ValidationException validationEx)
    {
        var fromFailures = string.Join("; ",
            validationEx.Errors.Select(e => e.ErrorMessage).Where(m => !string.IsNullOrWhiteSpace(m)));
        return !string.IsNullOrWhiteSpace(fromFailures) ? fromFailures : validationEx.Message;
    }

    private static bool IsSqlConnectivityFailure(Microsoft.Data.SqlClient.SqlException ex)
    {
        // -2 timeout, 53 network, 40 network, 35 internal SNI (common when host is down)
        return ex.Number is -2 or 53 or 40 or 35
               || ex.Message.Contains("network-related", StringComparison.OrdinalIgnoreCase)
               || ex.Message.Contains("pre-login handshake", StringComparison.OrdinalIgnoreCase)
               || ex.Message.Contains("server was not found", StringComparison.OrdinalIgnoreCase);
    }
}
