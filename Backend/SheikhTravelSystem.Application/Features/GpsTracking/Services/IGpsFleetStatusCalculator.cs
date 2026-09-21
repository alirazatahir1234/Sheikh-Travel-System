using System.Data;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Traccar;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

public interface IGpsFleetStatusCalculator
{
    Task<GpsFleetStatusLocalDto> ComputeAsync(
        IDbConnection connection,
        int tenantId,
        IOptions<GpsSettings> gpsSettings,
        IOptions<TraccarOptions> traccarOptions,
        CancellationToken cancellationToken = default,
        DataScopeResult? scope = null);
}
