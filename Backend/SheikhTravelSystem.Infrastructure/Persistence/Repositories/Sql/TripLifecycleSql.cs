using System.Data;
using Dapper;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories.Sql;

internal static class TripLifecycleSql
{
    internal static async Task RecordStatusAsync(
        IDbConnection connection,
        IDbTransaction? transaction,
        int tripId,
        TripStatus? from,
        TripStatus to,
        string? changedBy,
        string? note,
        CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO TripStatusHistory (TripId, FromStatus, ToStatus, ChangedAtUtc, ChangedBy, Note)
              VALUES (@TripId, @FromStatus, @ToStatus, GETUTCDATE(), @ChangedBy, @Note)",
            new
            {
                TripId = tripId,
                FromStatus = (int?)from,
                ToStatus = (int)to,
                ChangedBy = changedBy,
                Note = note
            },
            transaction: transaction,
            cancellationToken: ct));
    }

    internal static async Task ReplaceStopsAsync(
        IDbConnection connection,
        IDbTransaction? transaction,
        int tripId,
        IReadOnlyList<(int Sequence, string Location, double? Latitude, double? Longitude, DateTime? Eta)> stops,
        CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE TripStops SET IsDeleted = 1 WHERE TripId = @TripId AND IsDeleted = 0",
            new { TripId = tripId },
            transaction: transaction,
            cancellationToken: ct));

        foreach (var stop in stops.OrderBy(s => s.Sequence))
        {
            await connection.ExecuteAsync(new CommandDefinition(
                @"INSERT INTO TripStops (TripId, Sequence, Location, Latitude, Longitude, Eta, IsDeleted)
                  VALUES (@TripId, @Sequence, @Location, @Latitude, @Longitude, @Eta, 0)",
                new
                {
                    TripId = tripId,
                    stop.Sequence,
                    stop.Location,
                    stop.Latitude,
                    stop.Longitude,
                    stop.Eta
                },
                transaction: transaction,
                cancellationToken: ct));
        }
    }

    internal static async Task SyncResourceStatusAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        int tenantId,
        int? driverId,
        int? vehicleId,
        TripStatus status,
        CancellationToken ct)
    {
        var driverStatus = status switch
        {
            TripStatus.Started or TripStatus.AtPickup or TripStatus.Enroute or TripStatus.Delayed => DriverStatus.OnTrip,
            TripStatus.Completed or TripStatus.Cancelled or TripStatus.Failed => DriverStatus.Available,
            _ => (DriverStatus?)null
        };

        if (driverId is int dId && driverStatus.HasValue)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                @"UPDATE Drivers SET Status = @Status, UpdatedAt = GETUTCDATE()
                  WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
                new { Status = (int)driverStatus.Value, Id = dId, TenantId = tenantId },
                transaction: transaction,
                cancellationToken: ct));
        }

        var vehicleStatus = status switch
        {
            TripStatus.Started or TripStatus.AtPickup or TripStatus.Enroute or TripStatus.Delayed => VehicleStatus.OnTrip,
            TripStatus.Completed or TripStatus.Cancelled or TripStatus.Failed => VehicleStatus.Available,
            _ => (VehicleStatus?)null
        };

        if (vehicleId is int vId && vehicleStatus.HasValue)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                @"UPDATE Vehicles SET Status = @Status, UpdatedAt = GETUTCDATE()
                  WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
                new { Status = (int)vehicleStatus.Value, Id = vId, TenantId = tenantId },
                transaction: transaction,
                cancellationToken: ct));
        }
    }

    internal static async Task EnsureAssignmentHistoryAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        int tenantId,
        int tripId,
        int? bookingId,
        int driverId,
        int vehicleId,
        string? pickup,
        string? drop,
        string? createdBy,
        CancellationToken ct)
    {
        var openExists = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            @"SELECT CASE WHEN EXISTS(
                SELECT 1 FROM AssignmentHistory
                WHERE TenantId = @TenantId AND VehicleId = @VehicleId AND DriverId = @DriverId
                  AND IsDeleted = 0 AND Status IN (N'Active', N'Scheduled')
                  AND (BookingId = @BookingId OR (@BookingId IS NULL AND Notes LIKE @TripMarker))
              ) THEN 1 ELSE 0 END",
            new
            {
                TenantId = tenantId,
                VehicleId = vehicleId,
                DriverId = driverId,
                BookingId = bookingId,
                TripMarker = $"%TripId={tripId}%"
            },
            transaction: transaction,
            cancellationToken: ct));

        if (openExists)
            return;

        var assignmentId = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            @"INSERT INTO AssignmentHistory
                (TenantId, VehicleId, DriverId, BookingId, AssignmentType, Status, StartAt,
                 Purpose, PickupLocation, DropLocation, Notes, CreatedBy, CreatedAt, IsDeleted)
              VALUES
                (@TenantId, @VehicleId, @DriverId, @BookingId, N'Trip', N'Active', GETUTCDATE(),
                 N'Trip dispatch', @Pickup, @Drop, @Notes, @CreatedBy, GETUTCDATE(), 0);
              SELECT CAST(SCOPE_IDENTITY() AS INT);",
            new
            {
                TenantId = tenantId,
                VehicleId = vehicleId,
                DriverId = driverId,
                BookingId = bookingId,
                Pickup = pickup,
                Drop = drop,
                Notes = $"TripId={tripId}",
                CreatedBy = createdBy
            },
            transaction: transaction,
            cancellationToken: ct));

        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE AssignmentHistory SET AssignmentNo = @No WHERE Id = @Id",
            new { No = $"ASN-{assignmentId:D6}", Id = assignmentId },
            transaction: transaction,
            cancellationToken: ct));
    }

    internal static async Task CloseAssignmentHistoryAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        int tenantId,
        int tripId,
        int? bookingId,
        CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            @"UPDATE AssignmentHistory
              SET Status = N'Completed', EndAt = GETUTCDATE()
              WHERE TenantId = @TenantId AND IsDeleted = 0
                AND Status IN (N'Active', N'Scheduled')
                AND (BookingId = @BookingId OR Notes LIKE @TripMarker)",
            new
            {
                TenantId = tenantId,
                BookingId = bookingId,
                TripMarker = $"%TripId={tripId}%"
            },
            transaction: transaction,
            cancellationToken: ct));
    }
}
