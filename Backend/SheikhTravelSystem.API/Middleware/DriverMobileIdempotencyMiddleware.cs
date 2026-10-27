using System.Text;
using System.Text.Json;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;

namespace SheikhTravelSystem.API.Middleware;

/// <summary>
/// Replays stored JSON responses for driver-app POSTs when X-Client-Op-Id / Idempotency-Key is present.
/// </summary>
public sealed class DriverMobileIdempotencyMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext context,
        IDriverMobileSyncRepository syncRepository,
        ICurrentUserService currentUser,
        ITenantContext tenantContext)
    {
        if (!ShouldIntercept(context))
        {
            await next(context);
            return;
        }

        var clientOpId = context.Request.Headers["X-Client-Op-Id"].FirstOrDefault()
            ?? context.Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(clientOpId))
        {
            await next(context);
            return;
        }

        var driverId = currentUser.DriverId;
        if (!driverId.HasValue)
        {
            await next(context);
            return;
        }

        int tenantId;
        try
        {
            tenantId = tenantContext.GetRequiredTenantId();
        }
        catch
        {
            await next(context);
            return;
        }

        var existing = await syncRepository.GetIdempotencyAsync(tenantId, clientOpId, context.RequestAborted);
        if (existing != null)
        {
            context.Response.StatusCode = existing.HttpStatus;
            context.Response.ContentType = "application/json; charset=utf-8";
            await context.Response.WriteAsync(existing.ResponseJson, context.RequestAborted);
            return;
        }

        var originalBody = context.Response.Body;
        await using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        await next(context);

        buffer.Seek(0, SeekOrigin.Begin);
        var responseText = await new StreamReader(buffer, Encoding.UTF8).ReadToEndAsync(context.RequestAborted);
        var status = context.Response.StatusCode;

        if (ShouldPersist(status) && !string.IsNullOrWhiteSpace(responseText))
        {
            var opType = ResolveOpType(context.Request.Path);
            await syncRepository.SaveIdempotencyAsync(
                tenantId,
                driverId.Value,
                clientOpId,
                opType,
                responseText,
                status,
                context.RequestAborted);
        }

        buffer.Seek(0, SeekOrigin.Begin);
        context.Response.Body = originalBody;
        await buffer.CopyToAsync(originalBody, context.RequestAborted);
    }

    private static bool ShouldIntercept(HttpContext context)
    {
        if (!HttpMethods.IsPost(context.Request.Method)) return false;
        var path = context.Request.Path.Value ?? "";
        if (!path.StartsWith("/api/driver-app", StringComparison.OrdinalIgnoreCase)) return false;
        if (path.Contains("/auth/", StringComparison.OrdinalIgnoreCase)) return false;
        if (path.Contains("/sync/batch", StringComparison.OrdinalIgnoreCase)) return false;
        if (path.Contains("/voice/", StringComparison.OrdinalIgnoreCase)) return false;
        return context.User.Identity?.IsAuthenticated == true;
    }

    private static bool ShouldPersist(int status) =>
        status is >= 200 and < 300 or 409;

    private static string ResolveOpType(PathString path)
    {
        var p = path.Value ?? "unknown";
        if (p.Contains("check-in", StringComparison.OrdinalIgnoreCase)) return "attendanceCheckIn";
        if (p.Contains("check-out", StringComparison.OrdinalIgnoreCase)) return "attendanceCheckOut";
        if (p.Contains("collect-payment", StringComparison.OrdinalIgnoreCase)) return "paymentCollect";
        if (p.Contains("/sos", StringComparison.OrdinalIgnoreCase)) return "sos";
        if (p.Contains("/fuel-receipts", StringComparison.OrdinalIgnoreCase)) return "fuelSubmit";
        if (p.Contains("/inspection", StringComparison.OrdinalIgnoreCase)) return "inspectionSubmit";
        if (p.Contains("/documents/upload", StringComparison.OrdinalIgnoreCase)) return "documentUpload";
        if (p.Contains("/trips/", StringComparison.OrdinalIgnoreCase)) return "tripAdvance";
        return "driverAppPost";
    }
}
