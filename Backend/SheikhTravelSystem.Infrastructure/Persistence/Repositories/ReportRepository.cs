using Dapper;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Reports.DTOs;
using SheikhTravelSystem.Domain.Enums;
using SheikhTravelSystem.Infrastructure.Persistence.Repositories.Sql;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories;

public sealed class ReportRepository(IDbConnectionFactory dbFactory) : IReportRepository
{
    public async Task<BookingReportDto> GetBookingReportAsync(
        DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.QuerySingleAsync<BookingReportDto>(
            new CommandDefinition(
                @"SELECT
                    COUNT(*) AS TotalBookings,
                    SUM(CASE WHEN Status = @Completed THEN 1 ELSE 0 END) AS Completed,
                    SUM(CASE WHEN Status = @Cancelled THEN 1 ELSE 0 END) AS Cancelled,
                    SUM(CASE WHEN Status = @Pending THEN 1 ELSE 0 END) AS Pending,
                    SUM(CASE WHEN Status = @Started THEN 1 ELSE 0 END) AS Active
                  FROM Bookings
                  WHERE CreatedAt BETWEEN @From AND @To AND IsDeleted = 0",
                new
                {
                    Completed = (int)BookingStatus.Completed,
                    Cancelled = (int)BookingStatus.Cancelled,
                    Pending = (int)BookingStatus.Pending,
                    Started = (int)BookingStatus.Started,
                    From = from, To = to
                },
                cancellationToken: cancellationToken));
    }

    public async Task<RevenueReportDto> GetRevenueReportAsync(
        DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        var totalRevenue = await connection.ExecuteScalarAsync<decimal>(
            new CommandDefinition(
                "SELECT ISNULL(SUM(Amount), 0) FROM Payments WHERE Status = @Paid AND PaymentDate BETWEEN @From AND @To AND IsDeleted = 0",
                new { Paid = (int)PaymentStatus.Paid, From = from, To = to },
                cancellationToken: cancellationToken));

        var fuelExpense = await connection.ExecuteScalarAsync<decimal>(
            new CommandDefinition(
                "SELECT ISNULL(SUM(TotalCost), 0) FROM FuelLogs WHERE FuelDate BETWEEN @From AND @To AND IsDeleted = 0",
                new { From = from, To = to },
                cancellationToken: cancellationToken));

        var maintenanceCost = await connection.ExecuteScalarAsync<decimal>(
            new CommandDefinition(
                "SELECT ISNULL(SUM(Cost), 0) FROM Maintenance WHERE MaintenanceDate BETWEEN @From AND @To AND IsDeleted = 0",
                new { From = from, To = to },
                cancellationToken: cancellationToken));

        return new RevenueReportDto(totalRevenue, fuelExpense, maintenanceCost, totalRevenue - fuelExpense - maintenanceCost);
    }

    public async Task<IReadOnlyList<VehicleProfitDto>> GetVehicleProfitAsync(
        DateTime from, DateTime to, int? vehicleId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var results = await connection.QueryAsync<VehicleProfitDto>(
            new CommandDefinition(
                @"SELECT v.Id AS VehicleId, v.Name AS VehicleName,
                  ISNULL(SUM(p.Amount), 0) AS Revenue,
                  ISNULL((SELECT SUM(fl.TotalCost) FROM FuelLogs fl WHERE fl.VehicleId = v.Id AND fl.FuelDate BETWEEN @From AND @To AND fl.IsDeleted = 0), 0) AS FuelCost,
                  ISNULL((SELECT SUM(m.Cost) FROM Maintenance m WHERE m.VehicleId = v.Id AND m.MaintenanceDate BETWEEN @From AND @To AND m.IsDeleted = 0), 0) AS MaintenanceCost,
                  ISNULL(SUM(p.Amount), 0)
                    - ISNULL((SELECT SUM(fl.TotalCost) FROM FuelLogs fl WHERE fl.VehicleId = v.Id AND fl.FuelDate BETWEEN @From AND @To AND fl.IsDeleted = 0), 0)
                    - ISNULL((SELECT SUM(m.Cost) FROM Maintenance m WHERE m.VehicleId = v.Id AND m.MaintenanceDate BETWEEN @From AND @To AND m.IsDeleted = 0), 0) AS Profit
                  FROM Vehicles v
                  LEFT JOIN Bookings b ON b.VehicleId = v.Id AND b.IsDeleted = 0 AND b.CreatedAt BETWEEN @From AND @To
                  LEFT JOIN Payments p ON p.BookingId = b.Id AND p.Status = @Paid AND p.IsDeleted = 0
                  WHERE v.IsDeleted = 0 AND (@VehicleId IS NULL OR v.Id = @VehicleId)
                  GROUP BY v.Id, v.Name",
                new { From = from, To = to, Paid = (int)PaymentStatus.Paid, VehicleId = vehicleId },
                cancellationToken: cancellationToken));

        return results.ToList();
    }

    public async Task<IReadOnlyList<DriverPerformanceDto>> GetDriverPerformanceAsync(
        DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var results = await connection.QueryAsync<DriverPerformanceDto>(
            new CommandDefinition(
                @"SELECT d.Id AS DriverId, d.FullName AS DriverName,
                  COUNT(b.Id) AS TotalTrips,
                  SUM(CASE WHEN b.Status = @Completed THEN 1 ELSE 0 END) AS CompletedTrips,
                  ISNULL(SUM(CASE WHEN b.Status = @Completed THEN b.TotalAmount ELSE 0 END), 0) AS TotalRevenue
                  FROM Drivers d
                  LEFT JOIN Bookings b ON b.DriverId = d.Id AND b.IsDeleted = 0 AND b.CreatedAt BETWEEN @From AND @To
                  WHERE d.IsDeleted = 0
                  GROUP BY d.Id, d.FullName
                  ORDER BY CompletedTrips DESC",
                new { Completed = (int)BookingStatus.Completed, From = from, To = to },
                cancellationToken: cancellationToken));

        return results.ToList();
    }

    public async Task<HashSet<int>> ResolveVehicleIdsForDriverAsync(
        int tenantId, int driverId, DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var ids = await connection.QueryAsync<int>(new CommandDefinition("""
            SELECT DISTINCT VehicleId FROM AssignmentHistory
            WHERE TenantId = @TenantId AND IsDeleted = 0 AND DriverId = @DriverId
              AND StartAt <= @To AND (EndAt IS NULL OR EndAt >= @From)
            """, new { TenantId = tenantId, DriverId = driverId, From = from, To = to }, cancellationToken: cancellationToken));
        return ids.ToHashSet();
    }

    public async Task<HashSet<int>> GetVehicleIdsByBranchDepartmentAsync(
        int tenantId, int? branchId, int? departmentId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var clauses = new List<string> { "TenantId = @TenantId", "IsDeleted = 0" };
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        if (branchId.HasValue) { clauses.Add("BranchId = @BranchId"); p.Add("BranchId", branchId.Value); }
        if (departmentId.HasValue) { clauses.Add("DepartmentId = @DepartmentId"); p.Add("DepartmentId", departmentId.Value); }

        var matchingIds = await connection.QueryAsync<int>(new CommandDefinition(
            $"SELECT Id FROM Vehicles WHERE {string.Join(" AND ", clauses)}", p, cancellationToken: cancellationToken));
        return matchingIds.ToHashSet();
    }

    public async Task<IReadOnlyList<FleetVehicleReportRow>> GetVehicleReportRowsAsync(
        int tenantId,
        int? branchId,
        int? departmentId,
        DataScopeResult? scope,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var clauses = new List<string> { "v.TenantId = @TenantId", "v.IsDeleted = 0" };
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        FleetReportSql.ApplyEffectiveVehicleScope(p, scope, null, branchId, departmentId, "v", clauses);
        var where = FleetReportSql.BuildWhere(clauses);

        var raw = await connection.QueryAsync<FleetVehicleReportRow>(new CommandDefinition($"""
            SELECT v.Id, v.Name, v.RegistrationNumber AS Plate, v.VehicleCode AS FleetNo,
                v.Make, v.Model, v.Year, v.FuelType, v.CurrentMileage, v.InsuranceExpiryDate, v.Status,
                d.FullName AS DriverName, gd.Name AS TrackerName
            FROM Vehicles v
            OUTER APPLY (
                SELECT TOP 1 ah.DriverId
                FROM AssignmentHistory ah
                WHERE ah.VehicleId = v.Id AND ah.IsDeleted = 0 AND ah.Status = N'Active'
                ORDER BY ah.StartAt DESC
            ) activeAssign
            LEFT JOIN Drivers d ON d.Id = activeAssign.DriverId AND d.IsDeleted = 0
            LEFT JOIN GpsDevices gd ON gd.Id = v.GpsDeviceId AND gd.IsDeleted = 0
            {where}
            ORDER BY v.Name
            """, p, cancellationToken: cancellationToken));

        return raw.ToList();
    }

    public async Task<IReadOnlyList<FleetFuelReportRow>> GetFuelReportRowsAsync(
        int tenantId,
        DateTime from,
        DateTime to,
        int? vehicleId,
        int? branchId,
        DataScopeResult? scope,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var clauses = new List<string> { "f.TenantId = @TenantId", "f.IsDeleted = 0", "f.FuelDate >= @From", "f.FuelDate < @To" };
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        p.Add("From", from);
        p.Add("To", to);
        FleetReportSql.ApplyEffectiveVehicleScope(p, scope, vehicleId, branchId, null, "v", clauses);
        var where = FleetReportSql.BuildWhere(clauses);

        var raw = await connection.QueryAsync<FleetFuelReportRow>(new CommandDefinition($"""
            SELECT f.Id, f.FuelDate, v.Name AS VehicleName, f.FuelType, f.Liters, f.PricePerLiter, f.TotalCost,
                f.OdometerReading,
                f.OdometerReading - LAG(f.OdometerReading) OVER (PARTITION BY f.VehicleId ORDER BY f.FuelDate) AS OdometerDelta
            FROM FuelLogs f
            INNER JOIN Vehicles v ON v.Id = f.VehicleId
            {where}
            ORDER BY v.Name, f.FuelDate
            """, p, cancellationToken: cancellationToken));

        return raw.ToList();
    }

    public async Task<IReadOnlyList<FleetDriverProfileRow>> GetDriverProfilesAsync(
        int tenantId,
        int? branchId,
        int? departmentId,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var clauses = new List<string> { "TenantId = @TenantId", "IsDeleted = 0" };
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        if (branchId.HasValue) { clauses.Add("BranchId = @BranchId"); p.Add("BranchId", branchId.Value); }
        if (departmentId.HasValue) { clauses.Add("DepartmentId = @DepartmentId"); p.Add("DepartmentId", departmentId.Value); }
        var where = FleetReportSql.BuildWhere(clauses);

        var profiles = await connection.QueryAsync<FleetDriverProfileRow>(
            new CommandDefinition($"""
                SELECT Id, FullName, LicenseNumber, Phone, LicenseExpiryDate
                FROM Drivers {where}
                """, p, cancellationToken: cancellationToken));

        return profiles.ToList();
    }

    public async Task<IReadOnlyDictionary<int, string>> GetActiveAssignmentVehicleNamesAsync(
        int tenantId,
        IReadOnlyCollection<int> driverIds,
        CancellationToken cancellationToken = default)
    {
        if (driverIds.Count == 0)
            return new Dictionary<int, string>();

        using var connection = dbFactory.CreateConnection();
        var currentVehicles = (await connection.QueryAsync<(int DriverId, string VehicleName)>(new CommandDefinition("""
            SELECT ah.DriverId, v.Name AS VehicleName
            FROM AssignmentHistory ah
            INNER JOIN Vehicles v ON v.Id = ah.VehicleId
            WHERE ah.TenantId = @TenantId AND ah.IsDeleted = 0 AND ah.Status = N'Active'
              AND ah.DriverId IN @DriverIds
            """, new { TenantId = tenantId, DriverIds = driverIds.ToList() }, cancellationToken: cancellationToken)))
            .GroupBy(x => x.DriverId).ToDictionary(g => g.Key, g => g.First().VehicleName);

        return currentVehicles;
    }

    public async Task<IReadOnlyList<AssignmentWindowRow>> GetAssignmentWindowsAsync(
        int tenantId,
        IReadOnlyCollection<int> vehicleIds,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default)
    {
        if (vehicleIds.Count == 0)
            return [];

        using var connection = dbFactory.CreateConnection();
        var assignments = await connection.QueryAsync<AssignmentWindowRow>(
            new CommandDefinition("""
                SELECT VehicleId, DriverId, StartAt, EndAt FROM AssignmentHistory
                WHERE TenantId = @TenantId AND IsDeleted = 0 AND DriverId IS NOT NULL
                  AND VehicleId IN @VehicleIds AND StartAt <= @To AND (EndAt IS NULL OR EndAt >= @From)
                """, new { TenantId = tenantId, VehicleIds = vehicleIds.ToList(), From = from, To = to },
                cancellationToken: cancellationToken));

        return assignments.ToList();
    }

    public async Task<IReadOnlyDictionary<int, string>> GetDriverNamesAsync(
        int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var driverNames = (await connection.QueryAsync<(int Id, string FullName)>(new CommandDefinition(
            "SELECT Id, FullName FROM Drivers WHERE TenantId = @TenantId AND IsDeleted = 0",
            new { TenantId = tenantId }, cancellationToken: cancellationToken))).ToDictionary(d => d.Id, d => d.FullName);
        return driverNames;
    }

    public async Task<IReadOnlyDictionary<int, decimal>> GetActiveSpeedLimitsByVehicleAsync(
        int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return (await connection.QueryAsync<(int? VehicleId, decimal SpeedLimitKmh)>(new CommandDefinition("""
            SELECT VehicleId, SpeedLimitKmh FROM GpsAlertRules
            WHERE TenantId = @TenantId AND IsDeleted = 0 AND IsActive = 1 AND SpeedLimitKmh IS NOT NULL
            """, new { TenantId = tenantId }, cancellationToken: cancellationToken)))
            .Where(r => r.VehicleId.HasValue)
            .ToDictionary(r => r.VehicleId!.Value, r => r.SpeedLimitKmh);
    }

    public async Task<Dictionary<int, int>> ResolveVehicleToDeviceMapAsync(
        int tenantId,
        IEnumerable<int> vehicleIds,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.QueryAsync<(int VehicleId, int TraccarDeviceId)>(new CommandDefinition(
            """
            SELECT v.Id AS VehicleId, d.TraccarDeviceId
            FROM Vehicles v
            INNER JOIN GpsDevices d ON d.Id = v.GpsDeviceId AND d.IsDeleted = 0
            WHERE v.TenantId = @TenantId AND v.IsDeleted = 0 AND d.TraccarDeviceId IS NOT NULL
              AND v.Id IN @VehicleIds
            """,
            new { TenantId = tenantId, VehicleIds = vehicleIds.Distinct().ToList() },
            cancellationToken: cancellationToken));

        return rows.ToDictionary(r => r.VehicleId, r => r.TraccarDeviceId);
    }

    public async Task<bool> HasNonTraccarVehicleAsync(
        int tenantId,
        IEnumerable<int> vehicleIds,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var count = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            SELECT COUNT(1)
            FROM Vehicles v
            LEFT JOIN GpsDevices d ON d.Id = v.GpsDeviceId AND d.IsDeleted = 0
            WHERE v.TenantId = @TenantId AND v.IsDeleted = 0 AND v.Id IN @VehicleIds
              AND (d.Id IS NULL OR d.TraccarDeviceId IS NULL)
            """,
            new { TenantId = tenantId, VehicleIds = vehicleIds.Distinct().ToList() },
            cancellationToken: cancellationToken));

        return count > 0;
    }
}
