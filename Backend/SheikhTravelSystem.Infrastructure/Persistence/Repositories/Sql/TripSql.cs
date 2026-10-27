namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories.Sql;

internal static class TripSql
{
    internal const string ListSelect = """
        SELECT t.Id, t.TripNumber, t.BookingId, b.BookingNumber,
               t.CustomerId, c.FullName AS CustomerName,
               t.DriverId, d.FullName AS DriverName,
               t.VehicleId, v.Name AS VehicleName,
               t.RouteId,
               COALESCE(NULLIF(r.Name, ''), r.Source + ' → ' + r.Destination) AS RouteName,
               t.PickupAddress, t.DestinationAddress,
               t.TripDate, t.PlannedStart, t.PlannedEnd, t.Status,
               CAST(CASE WHEN t.VehicleId IS NOT NULL AND EXISTS (
                   SELECT 1 FROM GpsDevices gd
                   WHERE gd.VehicleId = t.VehicleId AND gd.IsDeleted = 0
                     AND gd.LastSeenAt >= DATEADD(minute, -15, GETUTCDATE())
               ) THEN 1 ELSE 0 END AS BIT) AS GpsOnline,
               t.TripType, t.Priority,
               (SELECT TOP 1 ci.Status FROM CustomerInvoices ci
                WHERE ci.TenantId = t.TenantId AND ci.TripId = t.Id AND ci.IsDeleted = 0
                ORDER BY ci.Id DESC) AS InvoicePaymentStatus,
               CASE
                   WHEN EXISTS (SELECT 1 FROM TripRatings r WHERE r.TenantId = t.TenantId AND r.TripId = t.Id)
                       THEN N'Received'
                   WHEN EXISTS (SELECT 1 FROM TripFeedbackLinks fl WHERE fl.TripId = t.Id AND fl.UsedAt IS NULL)
                       THEN N'Pending'
                   WHEN t.Status = 9 THEN N'Not sent'
                   ELSE NULL
               END AS FeedbackStatus,
               t.SpecialCargoFlags,
               CAST(CASE WHEN EXISTS (
                   SELECT 1 FROM Trips tc
                   WHERE tc.TenantId = t.TenantId AND tc.IsDeleted = 0 AND tc.Id <> t.Id
                     AND tc.Status IN (2, 3, 4, 5, 6, 7, 8)
                     AND (
                         (t.DriverId IS NOT NULL AND tc.DriverId = t.DriverId)
                         OR (t.VehicleId IS NOT NULL AND tc.VehicleId = t.VehicleId)
                     )
                     AND tc.PlannedStart < DATEADD(HOUR, 4, t.PlannedStart)
                     AND DATEADD(HOUR, 4, tc.PlannedStart) > t.PlannedStart
               ) THEN 1 ELSE 0 END AS BIT) AS HasScheduleConflict
        FROM Trips t
        LEFT JOIN Bookings b ON t.BookingId = b.Id
        LEFT JOIN Customers c ON t.CustomerId = c.Id
        LEFT JOIN Drivers d ON t.DriverId = d.Id
        LEFT JOIN Vehicles v ON t.VehicleId = v.Id
        LEFT JOIN Routes r ON t.RouteId = r.Id
        """;
}
