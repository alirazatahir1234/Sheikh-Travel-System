namespace SheikhTravelSystem.Domain.Enums;

public enum UserRole
{
    Admin = 1,
    Dispatcher = 2,
    Driver = 3,
    Accountant = 4
}

public enum VehicleStatus
{
    Available = 1,
    OnTrip = 2,
    Maintenance = 3,
    Retired = 4
}

public enum BookingStatus
{
    Pending = 1,
    Confirmed = 2,
    Started = 3,
    Completed = 4,
    Cancelled = 5
}

public enum PaymentStatus
{
    Pending = 1,
    PartiallyPaid = 2,
    Paid = 3,
    Refunded = 4
}

public enum DriverStatus
{
    Available = 1,
    OnTrip = 2,
    OffDuty = 3,
    Suspended = 4
}

public enum NotificationType
{
    BookingCreated = 1,
    TripDelayed = 2,
    VehicleOffline = 3,
    PaymentReceived = 4
}

public enum MaintenanceStatus
{
    Scheduled = 1,
    InProgress = 2,
    Completed = 3
}

public enum FuelType
{
    Petrol = 1,
    Diesel = 2,
    CNG = 3
}
