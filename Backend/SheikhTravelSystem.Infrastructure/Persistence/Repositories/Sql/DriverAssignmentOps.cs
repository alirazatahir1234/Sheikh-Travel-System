using System.Data;
using Dapper;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Features.Drivers;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories.Sql;

/// <summary>
/// Shared assignment validation SQL (formerly Application DriverAssignmentOps).
/// </summary>
internal static class DriverAssignmentOps
{
    public static void OpenConnection(IDbConnection connection)
    {
        if (connection.State != ConnectionState.Open)
            connection.Open();
    }

    public static async Task EnsureDriverAssignableForTripAsync(
        IDbConnection connection,
        int tenantId,
        int driverId,
        CancellationToken cancellationToken,
        IDbTransaction? transaction = null)
    {
        var found = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                @"SELECT CASE WHEN EXISTS(
                    SELECT 1 FROM Drivers WHERE Id = @DriverId AND TenantId = @TenantId AND IsDeleted = 0
                  ) THEN 1 ELSE 0 END",
                new { DriverId = driverId, TenantId = tenantId },
                transaction: transaction,
                cancellationToken: cancellationToken));
        if (!found)
            throw new NotFoundException("Driver", driverId);

        var row = await connection.QuerySingleAsync<(bool IsActive, int Status, string? VerificationStatus, DateTime LicenseExpiry)>(
            new CommandDefinition(
                @"SELECT IsActive, Status, VerificationStatus, LicenseExpiryDate
                  FROM Drivers WHERE Id = @DriverId AND TenantId = @TenantId AND IsDeleted = 0",
                new { DriverId = driverId, TenantId = tenantId },
                transaction: transaction,
                cancellationToken: cancellationToken));

        DriverAssignmentGuard.EnsureAssignableForVehicleTrip(
            row.IsActive,
            (DriverStatus)row.Status,
            row.VerificationStatus,
            row.LicenseExpiry);
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
        // Use live bookings/trips only — Drivers.Status=OnTrip can be stale after incomplete
        // lifecycle updates and falsely blocks fleet reassignment.
        var blockingBooking = await connection.QuerySingleOrDefaultAsync<(int Id, string? BookingNumber)>(
            new CommandDefinition(
                @"SELECT TOP 1 b.Id, b.BookingNumber
                  FROM Bookings b
                  WHERE b.DriverId = @DriverId AND b.TenantId = @TenantId AND b.IsDeleted = 0
                    AND b.Status = @Started
                  ORDER BY b.Id DESC",
                new
                {
                    DriverId = driverId,
                    TenantId = tenantId,
                    Started = (int)BookingStatus.Started
                },
                transaction: transaction,
                cancellationToken: cancellationToken));

        if (blockingBooking.Id > 0)
        {
            var label = string.IsNullOrWhiteSpace(blockingBooking.BookingNumber)
                ? $"#{blockingBooking.Id}"
                : blockingBooking.BookingNumber;
            throw new ConflictException(
                $"Driver is on started booking {label}. Complete or cancel that booking before reassigning.");
        }

        var blockingTrip = await connection.QuerySingleOrDefaultAsync<(int Id, string? TripNumber)>(
            new CommandDefinition(
                @"SELECT TOP 1 t.Id, t.TripNumber
                  FROM Trips t
                  WHERE t.DriverId = @DriverId AND t.TenantId = @TenantId AND t.IsDeleted = 0
                    AND t.Status IN (@TripStarted, @AtPickup, @Enroute, @Delayed)
                  ORDER BY t.Id DESC",
                new
                {
                    DriverId = driverId,
                    TenantId = tenantId,
                    TripStarted = (int)TripStatus.Started,
                    AtPickup = (int)TripStatus.AtPickup,
                    Enroute = (int)TripStatus.Enroute,
                    Delayed = (int)TripStatus.Delayed
                },
                transaction: transaction,
                cancellationToken: cancellationToken));

        if (blockingTrip.Id > 0)
        {
            var label = string.IsNullOrWhiteSpace(blockingTrip.TripNumber)
                ? $"#{blockingTrip.Id}"
                : blockingTrip.TripNumber;
            throw new ConflictException(
                $"Driver is on active trip {label} and cannot be reassigned.");
        }
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
