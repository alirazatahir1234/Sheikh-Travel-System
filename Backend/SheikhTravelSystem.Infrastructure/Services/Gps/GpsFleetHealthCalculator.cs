using System.Data;
using Dapper;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.GpsTracking;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;
using SheikhTravelSystem.Application.Features.MaintenanceModule;
using SheikhTravelSystem.Infrastructure.Persistence;

namespace SheikhTravelSystem.Infrastructure.Services.Gps;

/// <summary>
/// Loads real telemetry / maintenance / alert / insurance rows and scores via
/// <see cref="GpsFleetHealthAssessor"/>. Never invents missing factor values.
/// </summary>
public sealed class GpsFleetHealthCalculator(
    IDbConnectionFactory dbFactory,
    ITenantContext tenantContext,
    IOptions<GpsSettings> gpsSettings) : IGpsFleetHealthCalculator
{
    public async Task<FleetHealthSummaryDto> ComputeAsync(CancellationToken cancellationToken = default)
    {
        var settings = gpsSettings.Value;
        var healthOpts = settings.FleetHealth ?? new GpsFleetHealthOptions();
        var staleMinutes = settings.OfflineStaleMinutes <= 0
            ? GpsFleetStatusBuckets.DefaultOfflineStaleMinutes
            : settings.OfflineStaleMinutes;
        var tenantId = tenantContext.GetRequiredTenantId();
        var utcNow = DateTime.UtcNow;

        using var connection = dbFactory.CreateConnection();

        var vehicles = (await connection.QueryAsync<(
            int VehicleId,
            string VehicleName,
            string RegistrationNumber,
            int? GpsDeviceId,
            DateTime? LastUpdate,
            decimal? VclBattery,
            int? VclGsm,
            decimal? DeviceBattery,
            int? DeviceRssi,
            DateTime? InsuranceExpiryDate,
            decimal CurrentMileage)>(
            new CommandDefinition(
                """
                SELECT
                  v.Id AS VehicleId,
                  ISNULL(NULLIF(LTRIM(RTRIM(v.Name)), ''), CONCAT(N'Vehicle #', v.Id)) AS VehicleName,
                  ISNULL(v.RegistrationNumber, N'') AS RegistrationNumber,
                  v.GpsDeviceId,
                  vcl.LastUpdate,
                  vcl.BatteryLevel AS VclBattery,
                  vcl.GsmSignal AS VclGsm,
                  gd.LastBatteryLevel AS DeviceBattery,
                  gd.LastRssi AS DeviceRssi,
                  v.InsuranceExpiryDate,
                  ISNULL(v.CurrentMileage, 0) AS CurrentMileage
                FROM Vehicles v
                LEFT JOIN VehicleCurrentLocation vcl ON vcl.VehicleId = v.Id
                LEFT JOIN GpsDevices gd ON gd.Id = v.GpsDeviceId AND gd.IsDeleted = 0
                WHERE v.TenantId = @TenantId
                  AND v.IsDeleted = 0
                  AND v.Status <> 5
                ORDER BY v.Name
                """,
                new { TenantId = tenantId },
                cancellationToken: cancellationToken))).ToList();

        if (vehicles.Count == 0)
            return GpsFleetHealthAssessor.Summarize([]);

        var vehicleIds = vehicles.Select(v => v.VehicleId).ToList();

        var schedules = (await connection.QueryAsync<(
            int VehicleId, string IntervalType, DateTime? NextDueDate, decimal? NextDueMileage)>(
            new CommandDefinition(
                """
                SELECT VehicleId, IntervalType, NextDueDate, NextDueMileage
                FROM VehicleMaintenanceSchedules
                WHERE TenantId = @TenantId
                  AND IsDeleted = 0
                  AND IsActive = 1
                  AND VehicleId IN @VehicleIds
                """,
                new { TenantId = tenantId, VehicleIds = vehicleIds },
                cancellationToken: cancellationToken))).ToList();

        var criticalAlerts = (await connection.QueryAsync<(int VehicleId, int AlertCount)>(
            new CommandDefinition(
                """
                SELECT e.VehicleId, COUNT(1) AS AlertCount
                FROM GpsAlertEvents e
                INNER JOIN Vehicles v ON v.Id = e.VehicleId AND v.TenantId = @TenantId AND v.IsDeleted = 0
                WHERE e.IsDeleted = 0
                  AND e.IsAcknowledged = 0
                  AND LOWER(COALESCE(e.Severity, 'medium')) = 'critical'
                  AND (e.Status IS NULL OR LOWER(e.Status) IN ('active', 'open'))
                  AND e.VehicleId IN @VehicleIds
                GROUP BY e.VehicleId
                """,
                new { TenantId = tenantId, VehicleIds = vehicleIds },
                cancellationToken: cancellationToken))).ToList();

        var alertByVehicle = criticalAlerts.ToDictionary(a => a.VehicleId, a => a.AlertCount);
        var schedulesByVehicle = schedules.GroupBy(s => s.VehicleId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var assessed = new List<FleetVehicleHealthDto>(vehicles.Count);
        foreach (var v in vehicles)
        {
            var hasDevice = v.GpsDeviceId.HasValue;
            var battery = v.VclBattery ?? v.DeviceBattery;
            var gsm = v.VclGsm ?? v.DeviceRssi;
            var maintStatus = ResolveMaintenanceStatus(
                schedulesByVehicle.GetValueOrDefault(v.VehicleId),
                v.CurrentMileage);
            var insuranceStatus = ExpiryStatusFor(v.InsuranceExpiryDate);
            alertByVehicle.TryGetValue(v.VehicleId, out var criticalCount);

            var input = new GpsFleetHealthAssessor.VehicleHealthInput(
                v.VehicleId,
                v.VehicleName,
                v.RegistrationNumber,
                hasDevice,
                v.LastUpdate,
                battery,
                gsm,
                criticalCount,
                maintStatus,
                insuranceStatus);

            assessed.Add(GpsFleetHealthAssessor.Assess(input, utcNow, healthOpts, staleMinutes));
        }

        return GpsFleetHealthAssessor.Summarize(assessed);
    }

    private static string ResolveMaintenanceStatus(
        List<(int VehicleId, string IntervalType, DateTime? NextDueDate, decimal? NextDueMileage)>? rows,
        decimal currentMileage)
    {
        if (rows is null || rows.Count == 0)
            return "None";

        return rows
            .Select(s => MaintenanceScheduleHelper.ComputeStatus(
                s.IntervalType, s.NextDueDate, s.NextDueMileage, null, currentMileage, null))
            .OrderByDescending(SeverityRank)
            .First();
    }

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
}
