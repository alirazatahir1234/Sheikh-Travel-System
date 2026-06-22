namespace SheikhTravelSystem.Application.Features.MaintenanceModule;

public static class MaintenanceScheduleHelper
{
    public const string StatusUpcoming = "Upcoming";
    public const string StatusDueSoon = "DueSoon";
    public const string StatusOverdue = "Overdue";

    public const int DueSoonDays = 15;
    public const decimal DueSoonMileageKm = 500m;
    public const decimal DueSoonEngineHours = 50m;

    public static decimal? ComputeNextDueMileage(string intervalType, int intervalValue, decimal? lastServiceMileage) =>
        IsMileageInterval(intervalType) && lastServiceMileage.HasValue
            ? lastServiceMileage.Value + intervalValue
            : null;

    public static decimal? ComputeNextDueEngineHours(string intervalType, int intervalValue, decimal? lastServiceEngineHours) =>
        IsEngineHoursInterval(intervalType) && lastServiceEngineHours.HasValue
            ? lastServiceEngineHours.Value + intervalValue
            : null;

    public static DateTime? ComputeNextDueDate(string intervalType, int intervalValue, DateTime? lastServiceDate) =>
        IsDateInterval(intervalType)
            ? MaintenanceValidation.ComputeNextDueDate(intervalType, intervalValue, lastServiceDate)
            : null;

    public static (decimal? NextDueMileage, decimal? NextDueEngineHours, DateTime? NextDueDate) RecomputeNextDue(
        string intervalType, int intervalValue,
        DateTime? lastServiceDate, decimal? lastServiceMileage, decimal? lastServiceEngineHours) =>
    (
        ComputeNextDueMileage(intervalType, intervalValue, lastServiceMileage),
        ComputeNextDueEngineHours(intervalType, intervalValue, lastServiceEngineHours),
        ComputeNextDueDate(intervalType, intervalValue, lastServiceDate)
    );

    public static string ComputeStatus(
        string intervalType,
        DateTime? nextDueDate,
        decimal? nextDueMileage,
        decimal? nextDueEngineHours,
        decimal currentMileage,
        decimal? currentEngineHours,
        DateTime? asOfUtc = null)
    {
        var now = (asOfUtc ?? DateTime.UtcNow).Date;
        var worst = StatusUpcoming;

        if (IsDateInterval(intervalType) && nextDueDate.HasValue)
            worst = MaxSeverity(worst, DateStatus(nextDueDate.Value.Date, now));

        if (IsMileageInterval(intervalType) && nextDueMileage.HasValue)
            worst = MaxSeverity(worst, MileageStatus(currentMileage, nextDueMileage.Value));

        if (IsEngineHoursInterval(intervalType) && nextDueEngineHours.HasValue && currentEngineHours.HasValue)
            worst = MaxSeverity(worst, EngineHoursStatus(currentEngineHours.Value, nextDueEngineHours.Value));

        return worst;
    }

    public static string DateStatus(DateTime dueDate, DateTime today)
    {
        if (dueDate < today) return StatusOverdue;
        if (dueDate <= today.AddDays(DueSoonDays)) return StatusDueSoon;
        return StatusUpcoming;
    }

    public static string MileageStatus(decimal currentMileage, decimal nextDueMileage)
    {
        if (currentMileage >= nextDueMileage) return StatusOverdue;
        if (currentMileage >= nextDueMileage - DueSoonMileageKm) return StatusDueSoon;
        return StatusUpcoming;
    }

    public static string EngineHoursStatus(decimal currentHours, decimal nextDueHours)
    {
        if (currentHours >= nextDueHours) return StatusOverdue;
        if (currentHours >= nextDueHours - DueSoonEngineHours) return StatusDueSoon;
        return StatusUpcoming;
    }

    public static bool IsMileageInterval(string intervalType) =>
        intervalType.Equals("Mileage", StringComparison.OrdinalIgnoreCase);

    public static bool IsEngineHoursInterval(string intervalType) =>
        intervalType.Equals("EngineHours", StringComparison.OrdinalIgnoreCase) ||
        intervalType.Equals("Hours", StringComparison.OrdinalIgnoreCase);

    public static bool IsDateInterval(string intervalType) =>
        intervalType.Equals("Days", StringComparison.OrdinalIgnoreCase) ||
        intervalType.Equals("Day", StringComparison.OrdinalIgnoreCase) ||
        intervalType.Equals("Months", StringComparison.OrdinalIgnoreCase) ||
        intervalType.Equals("Month", StringComparison.OrdinalIgnoreCase);

    private static string MaxSeverity(string a, string b)
    {
        int Rank(string s) => s switch
        {
            StatusOverdue => 3,
            StatusDueSoon => 2,
            _ => 1
        };
        return Rank(b) > Rank(a) ? b : a;
    }

    public static IReadOnlyList<MaintenanceScheduleTemplateDto> DefaultTemplates { get; } =
    [
        new("Oil Change", "Mileage", 5000, "Preventive oil and filter service"),
        new("Brake Check", "Mileage", 10000, "Inspect brake pads and fluid"),
        new("Tire Rotation", "Mileage", 15000, "Rotate tires for even wear")
    ];
}

public record MaintenanceScheduleTemplateDto(
    string ServiceTypeName,
    string IntervalType,
    int IntervalValue,
    string Description);
