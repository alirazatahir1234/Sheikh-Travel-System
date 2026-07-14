using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.GpsTracking;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;
using SheikhTravelSystem.Application.Features.GpsTracking.Traccar;

namespace SheikhTravelSystem.Infrastructure.Services;

/// <summary>
/// Two distinct, asymmetric buckets per tick, both scoped to Traccar-linked devices only —
/// pending commands for non-Traccar devices are presumed owned by the commands/pending polling
/// path instead (unverified assumption; no device/relay firmware code exists in this repo to
/// confirm the two delivery paths are mutually exclusive).
///
/// 1. Pending dispatch failures past their backoff window are retried (capped at MaxRetries, then
///    marked Failed).
/// 2. Sent-but-unconfirmed commands past the ack timeout are marked Timeout — NOT auto-resent.
///    Traccar's 2xx only means "accepted for delivery," not "device executed it," and blindly
///    resending a stateful command (e.g. a relay toggle) could reverse it rather than confirm it.
///    An operator must explicitly hit Retry.
/// </summary>
public class GpsCommandRetryHostedService(
    IServiceProvider serviceProvider,
    IOptions<GpsSettings> gpsSettings,
    ILogger<GpsCommandRetryHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ScanAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "GPS command retry scan failed.");
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(gpsSettings.Value.CommandRetryIntervalSeconds), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    private async Task ScanAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
        var traccar = scope.ServiceProvider.GetRequiredService<ITraccarClient>();
        using var connection = dbFactory.CreateConnection();

        await RetryPendingAsync(connection, traccar, cancellationToken);
        await TimeoutUnconfirmedAsync(connection, cancellationToken);
    }

    private async Task RetryPendingAsync(System.Data.IDbConnection connection, ITraccarClient traccar, CancellationToken cancellationToken)
    {
        var candidates = (await connection.QueryAsync<(int Id, string CommandType, int TraccarDeviceId, string? Attributes, int RetryCount, int MaxRetries)>(
            new CommandDefinition(
                """
                SELECT c.Id, c.CommandType, d.TraccarDeviceId, c.Attributes, c.RetryCount, c.MaxRetries
                FROM GpsDeviceCommands c
                INNER JOIN GpsDevices d ON d.Id = c.GpsDeviceId
                WHERE c.Status = 'pending' AND c.IsDeleted = 0
                  AND c.NextRetryAt IS NOT NULL AND c.NextRetryAt <= GETUTCDATE()
                  AND c.RetryCount < c.MaxRetries
                  AND d.TraccarDeviceId IS NOT NULL
                """,
                cancellationToken: cancellationToken))).ToList();

        foreach (var c in candidates)
        {
            var definition = GpsCommandCatalog.Find(c.CommandType);
            if (definition?.TraccarType is null)
                continue;

            var attributes = string.IsNullOrWhiteSpace(c.Attributes)
                ? null
                : System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(c.Attributes);

            var sent = await traccar.SendCommandAsync(c.TraccarDeviceId, definition.TraccarType, attributes, cancellationToken);
            var newRetryCount = c.RetryCount + 1;

            if (sent)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    "UPDATE GpsDeviceCommands SET Status = 'sent', RetryCount = @RetryCount, ErrorMessage = NULL, NextRetryAt = NULL, UpdatedAt = GETUTCDATE() WHERE Id = @Id",
                    new { c.Id, RetryCount = newRetryCount },
                    cancellationToken: cancellationToken));
            }
            else if (newRetryCount >= c.MaxRetries)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    "UPDATE GpsDeviceCommands SET Status = 'failed', RetryCount = @RetryCount, ErrorMessage = 'Max retries exceeded', UpdatedAt = GETUTCDATE() WHERE Id = @Id",
                    new { c.Id, RetryCount = newRetryCount },
                    cancellationToken: cancellationToken));
            }
            else
            {
                // Linear backoff: wait longer each additional retry.
                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    UPDATE GpsDeviceCommands
                    SET RetryCount = @RetryCount, ErrorMessage = 'Traccar dispatch failed',
                        NextRetryAt = DATEADD(SECOND, @BackoffSeconds, GETUTCDATE()), UpdatedAt = GETUTCDATE()
                    WHERE Id = @Id
                    """,
                    new { c.Id, RetryCount = newRetryCount, BackoffSeconds = newRetryCount * gpsSettings.Value.CommandRetryIntervalSeconds },
                    cancellationToken: cancellationToken));
            }
        }

        if (candidates.Count > 0)
            logger.LogInformation("GPS command retry: processed {Count} pending command(s).", candidates.Count);
    }

    private async Task TimeoutUnconfirmedAsync(System.Data.IDbConnection connection, CancellationToken cancellationToken)
    {
        var timedOut = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE c
            SET c.Status = 'timeout', c.UpdatedAt = GETUTCDATE()
            FROM GpsDeviceCommands c
            INNER JOIN GpsDevices d ON d.Id = c.GpsDeviceId
            WHERE c.Status = 'sent' AND c.IsDeleted = 0
              AND d.TraccarDeviceId IS NOT NULL
              AND c.RequestedAt < DATEADD(MINUTE, -@TimeoutMinutes, GETUTCDATE())
            """,
            new { TimeoutMinutes = gpsSettings.Value.CommandAckTimeoutMinutes },
            cancellationToken: cancellationToken));

        if (timedOut > 0)
            logger.LogInformation("GPS command retry: marked {Count} unconfirmed command(s) as timeout.", timedOut);
    }
}
