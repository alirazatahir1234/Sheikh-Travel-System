namespace SheikhTravelSystem.Application.Features.MaintenanceModule;

/// <summary>
/// Rule-based predictive maintenance (oil / tires / engine life).
/// Defaults when history is missing — see constants and ComputeAll docs.
/// </summary>
public static class PredictiveMaintenanceHelper
{
    public const decimal DefaultOilIntervalKm = 5_000m;
    public const int DefaultOilIntervalDays = 180;
    public const decimal DefaultTireLifeKm = 40_000m;
    public const decimal DefaultEngineLifeKm = 300_000m;
    public const int DefaultEngineLifeYears = 15;
    public const decimal DefaultAvgDailyKm = 80m;
    public const decimal OilDueSoonKm = 500m;
    public const int OilDueSoonDays = 15;
    public const decimal TireDueSoonKm = 2_000m;
    public const decimal EngineWarnPct = 0.85m;
    public const decimal EngineCriticalPct = 0.95m;

    public const string CategoryOil = "OilChange";
    public const string CategoryTire = "TireWear";
    public const string CategoryEngine = "EngineLife";

    public static IReadOnlyList<PredictiveMaintenanceAlertDto> ComputeAll(
        IEnumerable<PredictiveVehicleInput> vehicles,
        DateTime? asOfUtc = null)
    {
        var today = (asOfUtc ?? DateTime.UtcNow).Date;
        var list = new List<PredictiveMaintenanceAlertDto>();
        foreach (var v in vehicles)
        {
            var oil = ComputeOil(v, today);
            if (oil is not null) list.Add(oil);
            var tire = ComputeTire(v, today);
            if (tire is not null) list.Add(tire);
            var engine = ComputeEngineLife(v, today);
            if (engine is not null) list.Add(engine);
        }

        return list
            .OrderByDescending(a => SeverityRank(a.Severity))
            .ThenBy(a => a.PredictedDueDate ?? DateTime.MaxValue)
            .ToList();
    }

    public static PredictiveMaintenanceAlertDto? ComputeOil(PredictiveVehicleInput v, DateTime today)
    {
        var intervalKm = v.OilIntervalKm ?? v.ScheduleOilIntervalKm ?? DefaultOilIntervalKm;
        var intervalDays = v.OilIntervalDays ?? v.ScheduleOilIntervalDays ?? DefaultOilIntervalDays;

        var lastMileage = v.LastOilChangeMileage
            ?? v.ScheduleLastOilMileage
            ?? (v.CurrentMileage > intervalKm ? v.CurrentMileage - intervalKm : 0m);
        var lastDate = v.LastOilChangeDate?.Date
            ?? v.ScheduleLastOilDate?.Date
            ?? (v.PurchaseDate?.Date ?? today.AddDays(-intervalDays));

        var dueMileage = lastMileage + intervalKm;
        var dueDateByTime = lastDate.AddDays(intervalDays);

        var avgDaily = ResolveAvgDailyKm(v, today);
        var remainingKm = dueMileage - v.CurrentMileage;
        var dueDateByMileage = remainingKm <= 0
            ? today
            : today.AddDays((double)Math.Ceiling(remainingKm / Math.Max(avgDaily, 1m)));

        var predictedDue = dueDateByMileage < dueDateByTime ? dueDateByMileage : dueDateByTime;
        var daysLeft = (predictedDue.Date - today).TotalDays;
        var kmLeft = dueMileage - v.CurrentMileage;

        string status;
        string severity;
        if (kmLeft <= 0 || daysLeft < 0)
        {
            status = "Overdue";
            severity = "Critical";
        }
        else if (kmLeft <= OilDueSoonKm || daysLeft <= OilDueSoonDays)
        {
            status = "DueSoon";
            severity = "Warning";
        }
        else
        {
            // Still show upcoming oil predictions so ops can plan (cap far-out noise later in UI).
            status = "Upcoming";
            severity = "Info";
        }

        // Skip far-future noise (>90 days and >3k km remaining).
        if (status == "Upcoming" && daysLeft > 90 && kmLeft > 3_000m)
            return null;

        var title = status switch
        {
            "Overdue" => "Oil change overdue",
            "DueSoon" => "Oil change due soon",
            _ => "Oil change upcoming"
        };

        var message = kmLeft <= 0
            ? $"{v.VehicleName}: oil overdue by {Math.Abs(kmLeft):N0} km (interval {intervalKm:N0} km / {intervalDays} days)."
            : $"{v.VehicleName}: oil due in ~{Math.Max(0, (int)daysLeft)} days / {kmLeft:N0} km left (due ~{predictedDue:yyyy-MM-dd}).";

        return new PredictiveMaintenanceAlertDto(
            v.VehicleId,
            v.VehicleName,
            v.VehicleRegistration,
            CategoryOil,
            severity,
            status,
            title,
            message,
            predictedDue,
            dueMileage,
            Math.Max(0, (int)Math.Round((double)(100m * (1m - Math.Clamp(kmLeft / intervalKm, 0m, 1m))))),
            Math.Max(0m, kmLeft));
    }

    public static PredictiveMaintenanceAlertDto? ComputeTire(PredictiveVehicleInput v, DateTime today)
    {
        var lifeKm = v.TireExpectedLifeKm ?? DefaultTireLifeKm;
        var installMileage = v.TireInstallMileage
            ?? v.ScheduleTireInstallMileage
            ?? (v.CurrentMileage > lifeKm ? v.CurrentMileage - lifeKm : 0m);
        var installDate = v.TireInstallDate?.Date
            ?? v.ScheduleTireInstallDate?.Date
            ?? v.PurchaseDate?.Date
            ?? today.AddDays(-(double)(lifeKm / Math.Max(ResolveAvgDailyKm(v, today), 1m)));

        var wornKm = Math.Max(0m, v.CurrentMileage - installMileage);
        var remainingKm = lifeKm - wornKm;
        var avgDaily = ResolveAvgDailyKm(v, today);
        var predictedDue = remainingKm <= 0
            ? today
            : today.AddDays((double)Math.Ceiling(remainingKm / Math.Max(avgDaily, 1m)));

        // Prefer installDate + life if we have a calendar estimate closer.
        var lifeDays = lifeKm / Math.Max(avgDaily, 1m);
        var calendarDue = installDate.AddDays((double)lifeDays);
        if (calendarDue < predictedDue)
            predictedDue = calendarDue;

        var daysLeft = (predictedDue.Date - today).TotalDays;

        string status;
        string severity;
        if (remainingKm <= 0 || daysLeft < 0)
        {
            status = "Overdue";
            severity = "Critical";
        }
        else if (remainingKm <= TireDueSoonKm || daysLeft <= 30)
        {
            status = "DueSoon";
            severity = "Warning";
        }
        else
        {
            status = "Upcoming";
            severity = "Info";
        }

        if (status == "Upcoming" && daysLeft > 120 && remainingKm > 8_000m)
            return null;

        var wearPct = (int)Math.Round((double)Math.Clamp(100m * wornKm / lifeKm, 0m, 100m));
        var title = status switch
        {
            "Overdue" => "Tire replacement overdue",
            "DueSoon" => "Tires nearing end of life",
            _ => "Tire replacement upcoming"
        };
        var message = remainingKm <= 0
            ? $"{v.VehicleName}: tires past expected life ({lifeKm:N0} km). Replace ASAP."
            : $"{v.VehicleName}: ~{remainingKm:N0} km / {Math.Max(0, (int)daysLeft)} days left; predicted replace {predictedDue:yyyy-MM-dd} ({wearPct}% worn).";

        return new PredictiveMaintenanceAlertDto(
            v.VehicleId,
            v.VehicleName,
            v.VehicleRegistration,
            CategoryTire,
            severity,
            status,
            title,
            message,
            predictedDue,
            installMileage + lifeKm,
            wearPct,
            Math.Max(0m, remainingKm));
    }

    public static PredictiveMaintenanceAlertDto? ComputeEngineLife(PredictiveVehicleInput v, DateTime today)
    {
        var lifeKm = v.EngineExpectedLifeKm ?? DefaultEngineLifeKm;
        var lifeYears = v.EngineExpectedLifeYears ?? DefaultEngineLifeYears;

        var ageYears = ResolveAgeYears(v, today);
        var mileagePct = lifeKm <= 0 ? 0m : v.CurrentMileage / lifeKm;
        var agePct = lifeYears <= 0 ? 0m : ageYears / lifeYears;
        var lifePct = Math.Max(mileagePct, agePct);

        if (lifePct < EngineWarnPct)
            return null;

        var remainingKm = Math.Max(0m, lifeKm - v.CurrentMileage);
        var remainingYears = Math.Max(0m, lifeYears - ageYears);
        var avgDaily = ResolveAvgDailyKm(v, today);
        DateTime? predictedEnd = remainingKm > 0
            ? today.AddDays((double)Math.Ceiling(remainingKm / Math.Max(avgDaily, 1m)))
            : today;
        if (remainingYears > 0)
        {
            var byAge = today.AddDays((double)(remainingYears * 365m));
            if (predictedEnd is null || byAge < predictedEnd)
                predictedEnd = byAge;
        }

        var critical = lifePct >= EngineCriticalPct;
        var severity = critical ? "Critical" : "Warning";
        var status = critical ? "NearEndOfLife" : "Aging";
        var pct = (int)Math.Round((double)Math.Clamp(lifePct * 100m, 0m, 100m));
        var title = critical ? "Engine near end of life" : "Engine aging — plan replacement";
        var message =
            $"{v.VehicleName}: ~{pct}% of expected life used " +
            $"({v.CurrentMileage:N0}/{lifeKm:N0} km, age {ageYears:0.#}/{lifeYears} yrs)" +
            (predictedEnd.HasValue ? $"; estimate end ~{predictedEnd:yyyy-MM-dd}." : ".");

        return new PredictiveMaintenanceAlertDto(
            v.VehicleId,
            v.VehicleName,
            v.VehicleRegistration,
            CategoryEngine,
            severity,
            status,
            title,
            message,
            predictedEnd,
            lifeKm,
            pct,
            remainingKm);
    }

    public static decimal ResolveAvgDailyKm(PredictiveVehicleInput v, DateTime today)
    {
        if (v.AvgDailyKm is > 0) return v.AvgDailyKm.Value;

        DateTime? yearStart = null;
        if (v.Year is int y && y >= 1990 && y <= today.Year)
            yearStart = new DateTime(y, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var start = v.PurchaseDate?.Date ?? yearStart;

        if (start is null || start >= today || v.CurrentMileage <= 0)
            return DefaultAvgDailyKm;

        var days = Math.Max(1, (today - start.Value).TotalDays);
        var avg = v.CurrentMileage / (decimal)days;
        // Clamp wild averages from bad odometer/year data.
        if (avg < 5m) return DefaultAvgDailyKm;
        if (avg > 500m) return 500m;
        return avg;
    }

    public static decimal ResolveAgeYears(PredictiveVehicleInput v, DateTime today)
    {
        if (v.PurchaseDate is { } pd)
            return Math.Max(0m, (decimal)(today - pd.Date).TotalDays / 365.25m);
        if (v.Year is int y && y >= 1990 && y <= today.Year)
            return Math.Max(0m, today.Year - y + (today.DayOfYear / 365.25m));
        // Unknown age: use mileage-only path by returning 0 so mileagePct dominates.
        return 0m;
    }

    private static int SeverityRank(string severity) => severity switch
    {
        "Critical" => 3,
        "Warning" => 2,
        _ => 1
    };
}

public record PredictiveVehicleInput(
    int VehicleId,
    string VehicleName,
    string? VehicleRegistration,
    decimal CurrentMileage,
    int? Year,
    DateTime? PurchaseDate,
    decimal? OilIntervalKm,
    int? OilIntervalDays,
    DateTime? LastOilChangeDate,
    decimal? LastOilChangeMileage,
    DateTime? TireInstallDate,
    decimal? TireInstallMileage,
    decimal? TireExpectedLifeKm,
    decimal? EngineExpectedLifeKm,
    int? EngineExpectedLifeYears,
    decimal? ScheduleOilIntervalKm = null,
    int? ScheduleOilIntervalDays = null,
    DateTime? ScheduleLastOilDate = null,
    decimal? ScheduleLastOilMileage = null,
    DateTime? ScheduleTireInstallDate = null,
    decimal? ScheduleTireInstallMileage = null,
    decimal? AvgDailyKm = null);
