using System.Data;
using Dapper;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;
using SheikhTravelSystem.Application.Features.MaintenanceModule;
// MaintenanceScheduleHelper lives in MaintenanceModule namespace

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories;

public sealed partial class GpsTrackingRepository
{
    private const int MaxFleetSize = 5000;
    private const int MaxVehicles = 15;
    private const double GridSize = 0.001;
    private const int WeakGsmThreshold = 12;
    private const decimal LowBatteryThreshold = 25m;
    private const int OfflineStaleMinutes = 15;
    private static readonly TimeSpan FleetHistoryMaxRange = TimeSpan.FromDays(90);
    private static readonly TimeSpan FleetUtilizationMaxRange = TimeSpan.FromDays(400);

    private const string AlertSelectColumns = """
            e.Id, e.RuleId, e.VehicleId, v.Name AS VehicleName, e.EventType,
            ISNULL(e.Latitude, 0) AS Latitude, ISNULL(e.Longitude, 0) AS Longitude,
            ISNULL(e.Speed, 0) AS Speed, ISNULL(e.Message, '') AS Message,
            e.Timestamp, e.IsAcknowledged,
            COALESCE(e.Severity, 'medium') AS Severity,
            COALESCE(e.Status, 'active') AS Status,
            e.GeofenceId, g.Name AS GeofenceName,
            e.DriverId, d.FullName AS DriverName,
            e.ReadAt, e.ReadBy, e.AcknowledgedAt, e.AcknowledgedBy,
            e.ResolvedAt, e.ResolvedBy, e.ResolutionNotes, e.ArchivedAt, e.ArchivedBy
        """;

    private static string LabelFor(decimal percent) => percent switch
    {
        >= 80 => "Excellent",
        >= 60 => "Good",
        >= 40 => "Fair",
        _ => "Poor"
    };

    private static int SeverityRank(string status) => status switch
    {
        MaintenanceScheduleHelper.StatusOverdue => 3,
        MaintenanceScheduleHelper.StatusDueSoon => 2,
        _ => 1
    };

    private static string ExpiryStatusFor(DateTime? date)
    {
        if (!date.HasValue) return "Unknown";
        var days = (date.Value.Date - DateTime.UtcNow.Date).TotalDays;
        if (days < 0) return "Expired";
        if (days <= 30) return "ExpiringSoon";
        return "Valid";
    }

    private static (DateTime? From, DateTime? To) ResolveDateRange(string? datePreset, DateTime? from, DateTime? to)
    {
        if (string.IsNullOrWhiteSpace(datePreset))
            return (from, to);

        var today = DateTime.UtcNow.Date;
        return datePreset.Trim().ToLowerInvariant() switch
        {
            "today" => (today, today.AddDays(1).AddTicks(-1)),
            "yesterday" => (today.AddDays(-1), today.AddTicks(-1)),
            "last7" => (today.AddDays(-6), today.AddDays(1).AddTicks(-1)),
            "last30" => (today.AddDays(-29), today.AddDays(1).AddTicks(-1)),
            _ => (from, to)
        };
    }

    private static (double Lat, double Lng) ResolvePickupCoordinates(string? routeSource)
    {
        if (string.IsNullOrWhiteSpace(routeSource))
            return (31.5204, 74.3587);

        var source = routeSource.ToLowerInvariant();
        if (source.Contains("lahore")) return (31.5204, 74.3587);
        if (source.Contains("islamabad")) return (33.6844, 73.0479);
        if (source.Contains("sialkot")) return (32.4945, 74.5229);
        if (source.Contains("karachi")) return (24.8607, 67.0011);
        if (source.Contains("multan")) return (30.1575, 71.5249);
        return (31.5204, 74.3587);
    }

    private static async Task<int> CountVehiclesInsideAsync(
        IDbConnection connection,
        int tenantId,
        CancellationToken cancellationToken)
    {
        var fences = (await connection.QueryAsync<(
            int Id, string AreaType, double CenterLat, double CenterLng, double RadiusMeters, string? GeoJson)>(
            new CommandDefinition(
                @"SELECT Id, AreaType, CenterLat, CenterLng, RadiusMeters, GeoJson
                  FROM Geofences WHERE IsDeleted = 0 AND IsActive = 1",
                cancellationToken: cancellationToken))).ToList();

        if (fences.Count == 0) return 0;

        var positions = (await connection.QueryAsync<(int VehicleId, double Latitude, double Longitude, int? BranchId, int? DepartmentId)>(
            new CommandDefinition(
                @"SELECT c.VehicleId, c.Latitude, c.Longitude, v.BranchId, v.DepartmentId
                  FROM VehicleCurrentLocation c
                  INNER JOIN Vehicles v ON v.Id = c.VehicleId AND v.TenantId = @TenantId AND v.IsDeleted = 0
                  WHERE c.Latitude IS NOT NULL AND c.Longitude IS NOT NULL",
                new { TenantId = tenantId },
                cancellationToken: cancellationToken))).ToList();

        if (positions.Count == 0) return 0;

        var assignments = (await connection.QueryAsync<(int GeofenceId, int? VehicleId, int? BranchId, int? DepartmentId)>(
            new CommandDefinition(
                @"SELECT GeofenceId, VehicleId, BranchId, DepartmentId
                  FROM GeofenceAssignments WHERE IsDeleted = 0",
                cancellationToken: cancellationToken))).ToList();

        var inside = new HashSet<int>();
        foreach (var pos in positions)
        {
            foreach (var f in fences)
            {
                var fenceAssignments = assignments.Where(a => a.GeofenceId == f.Id).ToList();
                if (fenceAssignments.Count == 0)
                    continue;

                var assigned = fenceAssignments.Any(a =>
                    a.VehicleId == pos.VehicleId
                    || (a.BranchId.HasValue && a.BranchId == pos.BranchId)
                    || (a.DepartmentId.HasValue && a.DepartmentId == pos.DepartmentId));
                if (!assigned)
                    continue;

                if (GpsGeoHelper.IsInsideGeofence(
                        pos.Latitude, pos.Longitude, f.AreaType, f.CenterLat, f.CenterLng, f.RadiusMeters, f.GeoJson))
                {
                    inside.Add(pos.VehicleId);
                    break;
                }
            }
        }

        return inside.Count;
    }

}
