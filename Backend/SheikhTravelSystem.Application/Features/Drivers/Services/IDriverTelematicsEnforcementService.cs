using System.Data;

namespace SheikhTravelSystem.Application.Features.Drivers.Services;

/// <summary>
/// Creates DriverViolations from telematics GpsAlertEvents and escalates DriverStatus when
/// <see cref="DriverTelematicsEnforcementPolicy"/> thresholds are met.
/// </summary>
public interface IDriverTelematicsEnforcementService
{
    /// <summary>
    /// Idempotent: skips when alert type is not enforced, has no DriverId, or a violation
    /// already exists for this GpsAlertId.
    /// </summary>
    Task ProcessAlertAsync(IDbConnection connection, int gpsAlertId, CancellationToken cancellationToken = default);
}
