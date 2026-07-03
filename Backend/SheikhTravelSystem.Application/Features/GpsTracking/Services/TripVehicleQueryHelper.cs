using Dapper;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

internal static class TripVehicleQueryHelper
{
    internal static readonly TimeSpan MaxRange = TimeSpan.FromDays(30);
    private static readonly TimeSpan OnlineWindow = TimeSpan.FromMinutes(30);

    internal sealed record VehicleTripSource(
        int VehicleId,
        string? VehicleName,
        string? PlateNumber,
        int? GpsDeviceId,
        string? DeviceName,
        string? UniqueId,
        int? TraccarDeviceId,
        DateTime? LastSeenAt);

    internal sealed record FleetVehicleSource(
        int VehicleId,
        string? VehicleName,
        string? PlateNumber,
        int? GpsDeviceId,
        string? DeviceName,
        int? TraccarDeviceId,
        string? DriverName);

    internal sealed record DriverAssignmentWindow(int VehicleId, DateTime StartAt, DateTime? EndAt);

    internal static ApiResponse<T>? ValidateTripRequest<T>(int? vehicleId, DateTime fromDate, DateTime toDate)
    {
        if (fromDate > toDate)
        {
            return ApiResponse<T>.FailResponse("End Date cannot be earlier than Start Date.");
        }

        if (toDate - fromDate > MaxRange)
        {
            return ApiResponse<T>.FailResponse("Date range cannot exceed 30 days.");
        }

        if (!vehicleId.HasValue)
        {
            return ApiResponse<T>.FailResponse("Select a vehicle to view trips.");
        }

        return null;
    }

    internal static async Task<VehicleTripSource?> ResolveVehicleAsync(
        IDbConnectionFactory dbFactory,
        ITenantContext tenantContext,
        int vehicleId,
        CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        return await connection.QueryFirstOrDefaultAsync<VehicleTripSource>(new CommandDefinition(
            """
            SELECT v.Id AS VehicleId, v.Name AS VehicleName, v.RegistrationNumber AS PlateNumber,
                   d.Id AS GpsDeviceId, d.Name AS DeviceName, d.UniqueId, d.TraccarDeviceId, d.LastSeenAt
            FROM Vehicles v
            LEFT JOIN GpsDevices d ON d.Id = v.GpsDeviceId AND d.IsDeleted = 0
            WHERE v.Id = @VehicleId AND v.TenantId = @TenantId AND v.IsDeleted = 0
            """,
            new { VehicleId = vehicleId, TenantId = tenantId },
            cancellationToken: cancellationToken));
    }

    /// <summary>
    /// Shared fleet-wide vehicle resolution — used by GetGpsTripsQuery's fleet-wide path and the
    /// fleet-wide Stops/Events queries so all three see the same candidate vehicle set. When
    /// <paramref name="driverId"/> is supplied, the filter is deliberately broad (any assignment
    /// overlapping the date window, not "currently assigned") since it's only a candidate-set
    /// optimization — precise per-row attribution still happens via
    /// <see cref="MatchesDriverWindow"/> against each trip/stop/event's own timestamp.
    /// </summary>
    internal static async Task<List<FleetVehicleSource>> ResolveFleetVehiclesAsync(
        System.Data.IDbConnection connection,
        int tenantId,
        int? branchId,
        int? departmentId,
        int? driverId,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken)
    {
        var filters = new List<string> { "v.TenantId = @TenantId", "v.IsDeleted = 0" };
        var parameters = new DynamicParameters();
        parameters.Add("TenantId", tenantId);

        if (branchId.HasValue)
        {
            filters.Add("v.BranchId = @BranchId");
            parameters.Add("BranchId", branchId.Value);
        }

        if (departmentId.HasValue)
        {
            filters.Add("v.DepartmentId = @DepartmentId");
            parameters.Add("DepartmentId", departmentId.Value);
        }

        if (driverId.HasValue)
        {
            filters.Add("""
                EXISTS (
                    SELECT 1 FROM AssignmentHistory a
                    WHERE a.VehicleId = v.Id AND a.DriverId = @DriverId AND a.IsDeleted = 0
                      AND a.StartAt <= @ToDate AND ISNULL(a.EndAt, '9999-12-31') >= @FromDate
                )
                """);
            parameters.Add("DriverId", driverId.Value);
            parameters.Add("FromDate", fromDate);
            parameters.Add("ToDate", toDate);
        }

        var whereClause = string.Join(" AND ", filters);

        var rows = await connection.QueryAsync<FleetVehicleSource>(new CommandDefinition(
            $"""
            SELECT v.Id AS VehicleId, v.Name AS VehicleName, v.RegistrationNumber AS PlateNumber,
                   d.Id AS GpsDeviceId, d.Name AS DeviceName, d.TraccarDeviceId,
                   assignDrv.DriverName
            FROM Vehicles v
            LEFT JOIN GpsDevices d ON d.Id = v.GpsDeviceId AND d.IsDeleted = 0
            OUTER APPLY (
                SELECT TOP 1 a.DriverId, dr.FullName AS DriverName
                FROM AssignmentHistory a
                INNER JOIN Drivers dr ON dr.Id = a.DriverId AND dr.IsDeleted = 0
                WHERE a.VehicleId = v.Id AND a.IsDeleted = 0
                  AND a.Status IN (N'Active', N'Scheduled') AND a.DriverId IS NOT NULL
                ORDER BY CASE WHEN a.Status = N'Active' THEN 0 ELSE 1 END, a.StartAt DESC
            ) assignDrv
            WHERE {whereClause}
            """,
            parameters,
            cancellationToken: cancellationToken));

        return rows.ToList();
    }

    /// <summary>
    /// Assignment windows for a driver across a candidate vehicle set, overlapping the date range —
    /// fetched once and matched per-row via <see cref="MatchesDriverWindow"/> rather than re-queried
    /// per trip/stop/event.
    /// </summary>
    internal static async Task<List<DriverAssignmentWindow>> GetDriverAssignmentWindowsAsync(
        System.Data.IDbConnection connection,
        int driverId,
        IReadOnlyCollection<int> vehicleIds,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken)
    {
        if (vehicleIds.Count == 0)
        {
            return [];
        }

        var rows = await connection.QueryAsync<DriverAssignmentWindow>(new CommandDefinition(
            """
            SELECT VehicleId, StartAt, EndAt
            FROM AssignmentHistory
            WHERE DriverId = @DriverId AND IsDeleted = 0 AND VehicleId IN @VehicleIds
              AND StartAt <= @ToDate AND ISNULL(EndAt, '9999-12-31') >= @FromDate
            """,
            new { DriverId = driverId, VehicleIds = vehicleIds, FromDate = fromDate, ToDate = toDate },
            cancellationToken: cancellationToken));

        return rows.ToList();
    }

    /// <summary>
    /// The time-bounded AssignmentHistory signal used for rows with no Booking link (Traccar-sourced
    /// and locally-detected trips, and all stops/events) — the assignment active at the row's own
    /// timestamp, not "currently assigned."
    /// </summary>
    internal static bool MatchesDriverWindow(IReadOnlyList<DriverAssignmentWindow> windows, int vehicleId, DateTime at)
        => windows.Any(w => w.VehicleId == vehicleId && at >= w.StartAt && at <= (w.EndAt ?? DateTime.MaxValue));

    internal static async Task<TripDeviceContextDto?> BuildContextAsync(
        IDbConnectionFactory dbFactory,
        ITenantContext tenantContext,
        int vehicleId,
        CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        var row = await connection.QueryFirstOrDefaultAsync<dynamic>(new CommandDefinition(
            """
            SELECT v.Id AS VehicleId, v.Name AS VehicleName, v.RegistrationNumber AS PlateNumber,
                   d.Id AS GpsDeviceId, d.Name AS DeviceName, d.UniqueId, d.TraccarDeviceId, d.LastSeenAt,
                   vcl.Latitude AS LastLatitude, vcl.Longitude AS LastLongitude,
                   vcl.Speed AS LastSpeed, vcl.LastUpdate AS LastPositionAt, vcl.Ignition AS LastIgnition
            FROM Vehicles v
            LEFT JOIN GpsDevices d ON d.Id = v.GpsDeviceId AND d.IsDeleted = 0
            LEFT JOIN VehicleCurrentLocation vcl ON vcl.VehicleId = v.Id
            WHERE v.Id = @VehicleId AND v.TenantId = @TenantId AND v.IsDeleted = 0
            """,
            new { VehicleId = vehicleId, TenantId = tenantId },
            cancellationToken: cancellationToken));

        if (row is null) return null;

        DateTime? lastSeen = row.LastSeenAt ?? row.LastPositionAt;
        var isOnline = lastSeen.HasValue && DateTime.UtcNow - lastSeen.Value <= OnlineWindow;

        return new TripDeviceContextDto(
            (int)row.VehicleId,
            (string?)row.VehicleName,
            (string?)row.PlateNumber,
            (int?)row.GpsDeviceId,
            (string?)row.DeviceName,
            (string?)row.UniqueId,
            row.TraccarDeviceId is not null,
            isOnline,
            lastSeen,
            (double?)row.LastLatitude,
            (double?)row.LastLongitude,
            null,
            row.LastSpeed is null ? null : (decimal?)row.LastSpeed,
            (bool?)row.LastIgnition);
    }
}
