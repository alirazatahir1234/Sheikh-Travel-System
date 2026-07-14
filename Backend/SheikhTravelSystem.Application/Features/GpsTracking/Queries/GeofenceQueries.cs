using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Queries;

public record GetGeofencesQuery(
    string? Search = null,
    string? AreaType = null,
    bool? IsActive = null,
    int? VehicleId = null) : IRequest<ApiResponse<List<GeofenceDto>>>;

public class GetGeofencesQueryHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<GetGeofencesQuery, ApiResponse<List<GeofenceDto>>>
{
    public async Task<ApiResponse<List<GeofenceDto>>> Handle(GetGeofencesQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var sql = """
            SELECT g.Id, g.Name, g.AreaType, g.CenterLat, g.CenterLng, g.RadiusMeters, g.GeoJson, g.IsActive,
                   g.Color, g.Category, g.Description,
                   (SELECT COUNT(1) FROM GeofenceAssignments a
                    WHERE a.GeofenceId = g.Id AND a.IsDeleted = 0 AND a.VehicleId IS NOT NULL) AS AssignedVehicleCount
            FROM Geofences g
            WHERE g.IsDeleted = 0
            """;

        if (!string.IsNullOrWhiteSpace(request.Search))
            sql += " AND (g.Name LIKE @Search OR ISNULL(g.Category,'') LIKE @Search OR ISNULL(g.Description,'') LIKE @Search)";
        if (!string.IsNullOrWhiteSpace(request.AreaType))
            sql += " AND LOWER(g.AreaType) = LOWER(@AreaType)";
        if (request.IsActive.HasValue)
            sql += " AND g.IsActive = @IsActive";
        if (request.VehicleId.HasValue)
        {
            sql += """
                 AND EXISTS (
                   SELECT 1 FROM GeofenceAssignments a
                   WHERE a.GeofenceId = g.Id AND a.IsDeleted = 0 AND a.VehicleId = @VehicleId
                 )
                """;
        }

        sql += " ORDER BY g.Name";

        var rows = await connection.QueryAsync<GeofenceDto>(new CommandDefinition(
            sql,
            new
            {
                Search = $"%{request.Search?.Trim()}%",
                request.AreaType,
                request.IsActive,
                request.VehicleId
            },
            cancellationToken: cancellationToken));

        return ApiResponse<List<GeofenceDto>>.SuccessResponse(rows.ToList());
    }
}

public record GetGeofenceAssignmentsQuery(int GeofenceId) : IRequest<ApiResponse<List<GeofenceAssignmentDto>>>;

public class GetGeofenceAssignmentsQueryHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<GetGeofenceAssignmentsQuery, ApiResponse<List<GeofenceAssignmentDto>>>
{
    public async Task<ApiResponse<List<GeofenceAssignmentDto>>> Handle(GetGeofenceAssignmentsQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.QueryAsync<GeofenceAssignmentDto>(new CommandDefinition(
            """
            SELECT a.Id, a.GeofenceId, a.VehicleId, v.Name AS VehicleName, v.RegistrationNumber,
                   a.BranchId, b.Name AS BranchName, a.DepartmentId, d.Name AS DepartmentName
            FROM GeofenceAssignments a
            LEFT JOIN Vehicles v ON v.Id = a.VehicleId
            LEFT JOIN Branches b ON b.Id = a.BranchId
            LEFT JOIN Departments d ON d.Id = a.DepartmentId
            WHERE a.GeofenceId = @GeofenceId AND a.IsDeleted = 0
            ORDER BY v.Name, b.Name, d.Name
            """,
            new { request.GeofenceId },
            cancellationToken: cancellationToken));

        return ApiResponse<List<GeofenceAssignmentDto>>.SuccessResponse(rows.ToList());
    }
}

public record GetGeofenceStatsQuery : IRequest<ApiResponse<GeofenceStatsDto>>;

public class GetGeofenceStatsQueryHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<GetGeofenceStatsQuery, ApiResponse<GeofenceStatsDto>>
{
    public async Task<ApiResponse<GeofenceStatsDto>> Handle(GetGeofenceStatsQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();

        var total = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM Geofences WHERE IsDeleted = 0",
            cancellationToken: cancellationToken));
        var active = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM Geofences WHERE IsDeleted = 0 AND IsActive = 1",
            cancellationToken: cancellationToken));
        var todayEntries = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            @"SELECT COUNT(1) FROM GpsAlertEvents
              WHERE EventType = 'geofence_enter' AND IsDeleted = 0
              AND Timestamp >= CAST(GETUTCDATE() AS DATE)",
            cancellationToken: cancellationToken));
        var todayExits = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            @"SELECT COUNT(1) FROM GpsAlertEvents
              WHERE EventType = 'geofence_exit' AND IsDeleted = 0
              AND Timestamp >= CAST(GETUTCDATE() AS DATE)",
            cancellationToken: cancellationToken));
        var unacked = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            @"SELECT COUNT(*) FROM GpsAlertEvents
              WHERE EventType IN ('geofence_enter', 'geofence_exit') AND IsAcknowledged = 0 AND IsDeleted = 0
              AND Timestamp > DATEADD(HOUR, -24, GETUTCDATE())",
            cancellationToken: cancellationToken));

        var vehiclesInside = await CountVehiclesInsideAsync(connection, cancellationToken);

        return ApiResponse<GeofenceStatsDto>.SuccessResponse(new GeofenceStatsDto(
            total, active, vehiclesInside, todayEntries, todayExits, unacked));
    }

    internal static async Task<int> CountVehiclesInsideAsync(
        System.Data.IDbConnection connection,
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
                  INNER JOIN Vehicles v ON v.Id = c.VehicleId AND v.IsDeleted = 0
                  WHERE c.Latitude IS NOT NULL AND c.Longitude IS NOT NULL",
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

public record GetGeofenceEventsQuery(int GeofenceId, DateTime? From = null, DateTime? To = null)
    : IRequest<ApiResponse<List<GpsAlertEventDto>>>;

public class GetGeofenceEventsQueryHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<GetGeofenceEventsQuery, ApiResponse<List<GpsAlertEventDto>>>
{
    public async Task<ApiResponse<List<GpsAlertEventDto>>> Handle(GetGeofenceEventsQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var from = request.From ?? DateTime.UtcNow.Date.AddDays(-7);
        var to = request.To ?? DateTime.UtcNow.AddDays(1);

        var rows = await connection.QueryAsync<GpsAlertEventDto>(new CommandDefinition(
            """
            SELECT TOP 200 e.Id, e.RuleId, e.VehicleId, v.Name AS VehicleName, e.EventType,
                   e.Latitude, e.Longitude, e.Speed, e.Message, e.Timestamp, e.IsAcknowledged,
                   e.GeofenceId, g.Name AS GeofenceName
            FROM GpsAlertEvents e
            LEFT JOIN Vehicles v ON v.Id = e.VehicleId
            LEFT JOIN Geofences g ON g.Id = e.GeofenceId
            WHERE e.IsDeleted = 0 AND e.GeofenceId = @GeofenceId
              AND e.EventType IN ('geofence_enter', 'geofence_exit')
              AND e.Timestamp >= @From AND e.Timestamp < @To
            ORDER BY e.Timestamp DESC
            """,
            new { request.GeofenceId, From = from, To = to },
            cancellationToken: cancellationToken));

        return ApiResponse<List<GpsAlertEventDto>>.SuccessResponse(rows.ToList());
    }
}
