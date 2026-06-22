using System.Data;
using Dapper;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.Drivers;

public static class DriverAssignmentValidation
{
    public static void OpenConnection(IDbConnection connection)
    {
        if (connection.State != ConnectionState.Open)
            connection.Open();
    }

    public static Task<int> CompleteActiveAssignmentsAsync(
        IDbConnection connection,
        int tenantId,
        int driverId,
        int? vehicleId,
        IDbTransaction? transaction,
        CancellationToken cancellationToken)
        => connection.ExecuteAsync(new CommandDefinition(
            @"UPDATE AssignmentHistory SET Status = N'Completed', EndAt = GETUTCDATE(), ModifiedAt = GETUTCDATE()
              WHERE TenantId = @TenantId AND Status IN (N'Active', N'Scheduled') AND IsDeleted = 0
                AND (DriverId = @DriverId OR (@VehicleId IS NOT NULL AND VehicleId = @VehicleId))",
            new { TenantId = tenantId, DriverId = driverId, VehicleId = vehicleId },
            transaction: transaction,
            cancellationToken: cancellationToken));

    public static async Task EnsureVehicleAvailableForDriverAsync(
        IDbConnection connection,
        int tenantId,
        int vehicleId,
        int driverId,
        int? excludeAssignmentId,
        CancellationToken cancellationToken,
        IDbTransaction? transaction = null)
    {
        var conflict = await connection.QuerySingleOrDefaultAsync<(int DriverId, string DriverName, string? VehicleCode, string RegistrationNumber)>(
            new CommandDefinition(
                @"SELECT TOP 1 d.Id AS DriverId, d.FullName AS DriverName, v.VehicleCode, v.RegistrationNumber
                  FROM AssignmentHistory ah
                  INNER JOIN Drivers d ON d.Id = ah.DriverId AND d.IsDeleted = 0
                  INNER JOIN Vehicles v ON v.Id = ah.VehicleId AND v.IsDeleted = 0
                  WHERE ah.VehicleId = @VehicleId AND ah.TenantId = @TenantId
                    AND ah.IsDeleted = 0 AND ah.Status IN (N'Active', N'Scheduled')
                    AND ah.DriverId <> @DriverId
                    AND (@ExcludeId IS NULL OR ah.Id <> @ExcludeId)",
                new { VehicleId = vehicleId, TenantId = tenantId, DriverId = driverId, ExcludeId = excludeAssignmentId },
                transaction: transaction,
                cancellationToken: cancellationToken));

        if (conflict.DriverId > 0)
        {
            var vehicleLabel = !string.IsNullOrWhiteSpace(conflict.VehicleCode)
                ? conflict.VehicleCode
                : conflict.RegistrationNumber;
            throw new ConflictException($"Vehicle {vehicleLabel} is already assigned to driver {conflict.DriverName}.");
        }
    }

    public static async Task EnsureDriverNotOnActiveTripAsync(
        IDbConnection connection,
        int tenantId,
        int driverId,
        CancellationToken cancellationToken,
        IDbTransaction? transaction = null)
    {
        var onTrip = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                @"SELECT CASE WHEN EXISTS(
                    SELECT 1 FROM Drivers d
                    WHERE d.Id = @DriverId AND d.TenantId = @TenantId AND d.IsDeleted = 0 AND d.Status = @OnTrip
                  ) OR EXISTS(
                    SELECT 1 FROM Bookings b
                    WHERE b.DriverId = @DriverId AND b.TenantId = @TenantId AND b.IsDeleted = 0
                      AND b.Status = @Started
                  ) THEN 1 ELSE 0 END",
                new
                {
                    DriverId = driverId,
                    TenantId = tenantId,
                    OnTrip = (int)DriverStatus.OnTrip,
                    Started = (int)BookingStatus.Started
                },
                transaction: transaction,
                cancellationToken: cancellationToken));

        if (onTrip)
            throw new ConflictException("Driver is currently on an active trip and cannot be reassigned.");
    }

    public static async Task EnsureVehicleAssignableAsync(
        IDbConnection connection,
        int tenantId,
        int vehicleId,
        CancellationToken cancellationToken,
        IDbTransaction? transaction = null)
    {
        var vehicle = await connection.QuerySingleOrDefaultAsync<(int Id, int Status, DateTime? InsuranceExpiry)>(
            new CommandDefinition(
                @"SELECT Id, Status, InsuranceExpiryDate FROM Vehicles
                  WHERE Id = @VehicleId AND TenantId = @TenantId AND IsDeleted = 0",
                new { VehicleId = vehicleId, TenantId = tenantId },
                transaction: transaction,
                cancellationToken: cancellationToken));

        if (vehicle.Id == 0)
            throw new NotFoundException("Vehicle", vehicleId);

        if (vehicle.Status is (int)VehicleStatus.Draft or (int)VehicleStatus.Retired)
            throw new ConflictException("Vehicle is not active and cannot be assigned.");

        if (vehicle.Status == (int)VehicleStatus.Maintenance)
            throw new ConflictException("Vehicle is under maintenance and cannot be assigned.");

        if (vehicle.InsuranceExpiry.HasValue && vehicle.InsuranceExpiry.Value.Date < DateTime.UtcNow.Date)
            throw new ConflictException("Vehicle insurance has expired.");
    }
}
