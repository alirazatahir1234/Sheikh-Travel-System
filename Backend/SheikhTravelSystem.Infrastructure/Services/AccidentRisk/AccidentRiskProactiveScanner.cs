using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.AccidentRisk.Services;
using SheikhTravelSystem.Application.Features.SmartOps;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Infrastructure.Services.AccidentRisk;

/// <summary>
/// Raises SmartAlerts for high accident-risk drivers and routes (deduped daily per entity).
/// </summary>
public sealed class AccidentRiskProactiveScanner(
    IAccidentRiskRepository accidentRisk,
    ISmartAlertService smartAlerts,
    ILogger<AccidentRiskProactiveScanner> logger)
{
    private static readonly TimeSpan Lookback = TimeSpan.FromDays(90);

    public async Task ScanTenantAsync(int tenantId, CancellationToken cancellationToken)
    {
        var to = DateTime.UtcNow;
        var from = to.Subtract(Lookback);
        var dayKey = to.ToString("yyyy-MM-dd");

        try
        {
            var drivers = await accidentRisk.GetHighRiskDriversAsync(tenantId, from, to, cancellationToken);
            foreach (var row in drivers)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var severity = row.RiskScore >= 80 ? SmartAlertSeverity.Critical : SmartAlertSeverity.Warning;
                await smartAlerts.RaiseAsync(new CreateSmartAlertRequest(
                    tenantId,
                    SmartAlertRuleType.AccidentRisk,
                    severity,
                    AccidentRiskCalculator.FormatProactiveDriverMessage(row),
                    "Driver",
                    row.DriverId,
                    $"AccidentRisk:Driver:{row.DriverId}:{dayKey}"), cancellationToken);
            }

            var routes = await accidentRisk.GetHighRiskRoutesAsync(tenantId, from, to, cancellationToken);
            foreach (var row in routes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var severity = row.RiskScore >= 80 ? SmartAlertSeverity.Critical : SmartAlertSeverity.Warning;
                await smartAlerts.RaiseAsync(new CreateSmartAlertRequest(
                    tenantId,
                    SmartAlertRuleType.AccidentRisk,
                    severity,
                    AccidentRiskCalculator.FormatProactiveRouteMessage(row),
                    "Route",
                    row.RouteId,
                    $"AccidentRisk:Route:{row.RouteId}:{dayKey}"), cancellationToken);
            }
        }
        catch (Exception ex) when (ex.Message.Contains("Invalid object name", StringComparison.OrdinalIgnoreCase)
                                   || ex.Message.Contains("Invalid column name", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogDebug(ex, "Accident risk proactive scan skipped (schema gap) for tenant {TenantId}", tenantId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Accident risk proactive scan failed for tenant {TenantId}", tenantId);
        }
    }
}
