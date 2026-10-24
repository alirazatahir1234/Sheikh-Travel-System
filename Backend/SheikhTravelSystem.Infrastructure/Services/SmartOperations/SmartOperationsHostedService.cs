using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SheikhTravelSystem.Infrastructure.Services.SmartOperations;

/// <summary>
/// Runs Smart Ops rule engine every 60 minutes. Skips overlapping ticks via a gate.
/// </summary>
public class SmartOperationsHostedService(
    IServiceProvider serviceProvider,
    ILogger<SmartOperationsHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(60);
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(45);
    private int _running;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            try
            {
                await Task.Delay(StartupDelay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                if (Interlocked.CompareExchange(ref _running, 1, 0) == 0)
                {
                    try
                    {
                        using var scope = serviceProvider.CreateScope();
                        var scanner = scope.ServiceProvider.GetRequiredService<SmartOpsRuleScanner>();
                        await scanner.ScanAllTenantsAsync(stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Smart Operations automation scan failed.");
                    }
                    finally
                    {
                        Interlocked.Exchange(ref _running, 0);
                    }
                }
                else
                {
                    logger.LogDebug("Smart Operations scan skipped — previous tick still running.");
                }

                try
                {
                    await Task.Delay(Interval, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Host shutdown
        }
    }
}
