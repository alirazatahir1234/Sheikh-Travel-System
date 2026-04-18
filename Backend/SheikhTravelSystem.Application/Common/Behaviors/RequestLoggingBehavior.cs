using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace SheikhTravelSystem.Application.Common.Behaviors;

/// <summary>
/// Logs MediatR request execution lifecycle for all commands and queries.
/// </summary>
public class RequestLoggingBehavior<TRequest, TResponse>(ILogger<RequestLoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    /// <summary>
    /// Logs request start, completion duration, and unhandled failures.
    /// </summary>
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var stopwatch = Stopwatch.StartNew();

        logger.LogInformation("Handling request {RequestName}", requestName);

        try
        {
            var response = await next();
            stopwatch.Stop();

            logger.LogInformation("Handled request {RequestName} in {ElapsedMs}ms", requestName, stopwatch.ElapsedMilliseconds);
            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            logger.LogError(ex, "Request {RequestName} failed after {ElapsedMs}ms", requestName, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }
}
