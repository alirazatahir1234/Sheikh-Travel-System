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

    /// <summary>
    /// Completes active assignments for the given driver and/or vehicle within the tenant.
    /// </summary>
    public static Task<int> CompleteActiveAssignmentsAsync(
        IDbConnection connection,
        int tenantId,
        int driverId,
        int? vehicleId,
        IDbTransaction? transaction,
        CancellationToken cancellationToken)
        => connection.ExecuteAsync(new CommandDefinition(
            @"UPDATE AssignmentHistory SET Status = N'Completed', EndAt = GETUTCDATE()
              WHERE TenantId = @TenantId AND Status = N'Active' AND IsDeleted = 0
                AND (DriverId = @DriverId OR (@VehicleId IS NOT NULL AND VehicleId = @VehicleId))",
            new { TenantId = tenantId, DriverId = driverId, VehicleId = vehicleId },
            transaction: transaction,
            cancellationToken: cancellationToken));

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
}
