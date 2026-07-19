namespace SheikhTravelSystem.Application.Features.Trips;

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
               t.TripType, t.Priority
        FROM Trips t
        LEFT JOIN Bookings b ON t.BookingId = b.Id
        LEFT JOIN Customers c ON t.CustomerId = c.Id
        LEFT JOIN Drivers d ON t.DriverId = d.Id
        LEFT JOIN Vehicles v ON t.VehicleId = v.Id
        LEFT JOIN Routes r ON t.RouteId = r.Id
        """;
}
