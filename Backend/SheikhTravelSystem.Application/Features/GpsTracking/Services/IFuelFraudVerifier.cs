using System.Data;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

/// <summary>
/// After a driver logs liters at the pump, compares OBD fuel-level increase when the vehicle leaves the station.
/// </summary>
public interface IFuelFraudVerifier
{
    Task EvaluateAsync(
        IDbConnection connection,
        IngestPositionDto dto,
        DateTime timestamp,
        CancellationToken cancellationToken = default);
}
