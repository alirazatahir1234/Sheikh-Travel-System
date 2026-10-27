using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.SosDispatch;

namespace SheikhTravelSystem.Infrastructure.Services.Sos;

public sealed class SosAutoDispatchService(ISosDispatchRepository repository) : ISosAutoDispatchService
{
    public async Task<SosDispatchPlan> BuildPlanAsync(
        int tenantId,
        int driverId,
        int? vehicleId,
        double? latitude,
        double? longitude,
        string driverName,
        CancellationToken cancellationToken = default)
    {
        var (speed, routeJson, routeSummary) =
            await repository.GetLiveTelemetryAndRouteAsync(tenantId, driverId, vehicleId, cancellationToken);

        var region = latitude is double lat && longitude is double lng
            ? GeoDistance.InferRegionCode(lat, lng)
            : "PK";

        var partnerId = await repository.GetPrimaryTowingPartnerIdAsync(tenantId, region, cancellationToken);
        var partners = await repository.ListPartnersAsync(tenantId, cancellationToken);
        var partnerName = partners.FirstOrDefault(p => p.Id == partnerId)?.Name;

        VerifiedTeamCandidateRow? nearest = null;
        double? nearestKm = null;

        if (latitude is double sosLat && longitude is double sosLng)
        {
            var candidates = await repository.GetVerifiedTeamCandidatesAsync(tenantId, driverId, cancellationToken);
            foreach (var c in candidates)
            {
                if (c.Latitude is not double cLat || c.Longitude is not double cLng) continue;
                var km = GeoDistance.HaversineKm(sosLat, sosLng, cLat, cLng);
                if (km is null) continue;
                if (nearestKm is null || km < nearestKm)
                {
                    nearestKm = km;
                    nearest = c;
                }
            }
        }

        string dispatchMode;
        int? assignedDriverId = null;
        int? assignedUserId = null;
        string? teamName = null;
        int? eta = null;
        decimal? distanceKm = nearestKm.HasValue ? (decimal)Math.Round(nearestKm.Value, 2) : null;
        string confirmation;

        if (nearest is not null && nearestKm is <= GeoDistance.AutoAssignMaxKm)
        {
            dispatchMode = "AutoAssigned";
            assignedDriverId = nearest.DriverId;
            assignedUserId = nearest.UserId;
            teamName = nearest.FullName;
            eta = GeoDistance.EtaMinutesFromKm(nearestKm);
            confirmation =
                $"Help is on the way. {nearest.FullName} was auto-assigned (~{nearestKm:F1} km, ETA ~{eta} min).";
        }
        else if (nearest is not null)
        {
            dispatchMode = "Suggested";
            teamName = nearest.FullName;
            eta = GeoDistance.EtaMinutesFromKm(nearestKm);
            confirmation =
                $"Alert sent to dispatch. Fastest verified team: {nearest.FullName} (~{nearestKm:F1} km, ETA ~{eta} min). Tap dispatch in ERP to assign.";
        }
        else
        {
            dispatchMode = "DispatchOnly";
            confirmation = partnerName is not null
                ? $"Alert sent to admin and fleet managers. Recovery partner on file: {partnerName} ({region})."
                : $"Alert sent to admin and fleet managers for {driverName}.";
        }

        if (partnerName is not null && dispatchMode != "AutoAssigned")
            confirmation += $" Partner backup: {partnerName}.";

        return new SosDispatchPlan(
            speed,
            routeJson,
            routeSummary,
            region,
            assignedDriverId,
            assignedUserId,
            teamName,
            dispatchMode,
            distanceKm,
            eta,
            partnerId,
            partnerName,
            confirmation);
    }
}
