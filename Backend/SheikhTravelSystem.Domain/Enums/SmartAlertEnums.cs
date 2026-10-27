namespace SheikhTravelSystem.Domain.Enums;

public enum SmartAlertRuleType
{
    MaintenanceDue = 1,
    DocumentExpiry = 2,
    UnassignedTrip = 3,
    OverdueInvoice = 4,
    FuelAnomaly = 5,
    AccidentRisk = 6
}

public enum SmartAlertSeverity
{
    Info = 1,
    Warning = 2,
    Critical = 3
}

public enum SmartAlertStatus
{
    New = 1,
    Acknowledged = 2,
    Resolved = 3,
    Dismissed = 4
}
