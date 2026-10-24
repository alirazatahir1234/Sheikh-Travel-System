namespace SheikhTravelSystem.Application.Features.SmartOps;

/// <summary>
/// Pure deterministic thresholds for Smart Operations rules (unit-testable; no I/O).
/// </summary>
public static class SmartOpsRuleLogic
{
    public const decimal MaintenanceDueKm = 500m;
    public const int MaintenanceDueDays = 10;
    public const int DocumentExpiryLeadDays = 30;
    public const int UnassignedTripLeadHours = 24;
    public const int OverdueInvoiceGraceHours = 48;
    public const int FuelDuplicateWindowHours = 1;

    public static bool IsMaintenanceDueByMileage(decimal currentMileage, decimal? nextDueMileage)
    {
        if (!nextDueMileage.HasValue) return false;
        var remaining = nextDueMileage.Value - currentMileage;
        return remaining <= MaintenanceDueKm;
    }

    public static bool IsMaintenanceDueByDate(DateTime? nextDueDate, DateTime asOfUtc)
    {
        if (!nextDueDate.HasValue) return false;
        var days = (nextDueDate.Value.Date - asOfUtc.Date).TotalDays;
        return days <= MaintenanceDueDays;
    }

    public static bool IsMaintenanceDue(
        decimal currentMileage,
        decimal? nextDueMileage,
        DateTime? nextDueDate,
        DateTime asOfUtc) =>
        IsMaintenanceDueByMileage(currentMileage, nextDueMileage)
        || IsMaintenanceDueByDate(nextDueDate, asOfUtc);

    public static bool IsMaintenanceOverdue(
        decimal currentMileage,
        decimal? nextDueMileage,
        DateTime? nextDueDate,
        DateTime asOfUtc)
    {
        if (nextDueMileage.HasValue && currentMileage >= nextDueMileage.Value) return true;
        if (nextDueDate.HasValue && nextDueDate.Value.Date < asOfUtc.Date) return true;
        return false;
    }

    public static bool IsDocumentExpiringWithinLead(DateTime? expiryDate, DateTime asOfUtc, int leadDays = DocumentExpiryLeadDays)
    {
        if (!expiryDate.HasValue) return false;
        var days = (expiryDate.Value.Date - asOfUtc.Date).TotalDays;
        return days <= leadDays;
    }

    public static bool IsUnassignedTripImminent(DateTime pickupTimeUtc, DateTime asOfUtc, bool hasDriver, bool hasVehicle)
    {
        if (hasDriver && hasVehicle) return false;
        var hoursUntil = (pickupTimeUtc - asOfUtc).TotalHours;
        return hoursUntil >= 0 && hoursUntil <= UnassignedTripLeadHours;
    }

    /// <summary>
    /// Unpaid issued invoice is overdue when due moment + grace has passed.
    /// </summary>
    public static bool IsOverdueInvoice(DateTime? dueDateUtc, DateTime? issuedAtUtc, decimal balance, DateTime asOfUtc)
    {
        if (balance <= 0) return false;
        var due = dueDateUtc ?? issuedAtUtc;
        if (!due.HasValue) return false;
        return asOfUtc > due.Value.AddHours(OverdueInvoiceGraceHours);
    }

    public static bool IsFuelOverCapacity(decimal liters, decimal? tankCapacityLiters) =>
        tankCapacityLiters is > 0 && liters > tankCapacityLiters.Value;

    public static bool AreFuelEntriesTooClose(DateTime firstUtc, DateTime secondUtc) =>
        Math.Abs((secondUtc - firstUtc).TotalHours) < FuelDuplicateWindowHours;

    public static string BuildDailyDedupeKey(string ruleType, string entityType, int entityId, DateTime asOfUtc) =>
        $"{ruleType}:{entityType}:{entityId}:{asOfUtc:yyyy-MM-dd}";

    public static string BuildFuelAnomalyDedupeKey(int vehicleId, int fuelLogId, string kind, DateTime asOfUtc) =>
        $"{SmartAlertRuleTypeNames.FuelAnomaly}:{kind}:{vehicleId}:{fuelLogId}:{asOfUtc:yyyy-MM-dd}";
}
