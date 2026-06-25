namespace SheikhTravelSystem.Application.Features.MaintenanceModule;

public static class WorkOrderValidation
{
    public static readonly string[] AllowedPriorities = ["Low", "Medium", "High", "Critical"];
    public static readonly string[] AllowedMaintenanceTypes = ["Preventive", "Corrective", "Emergency"];

    public const int NotesMaxLength = 2000;
    public const decimal MaxCost = 9_999_999.99m;

    public static bool IsValidPriority(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        AllowedPriorities.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase);

    public static bool IsValidMaintenanceType(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        AllowedMaintenanceTypes.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase);

    public static int? NormalizeOptionalId(int? value) =>
        value is > 0 ? value : null;

    public static bool IsValidDateRange(DateTime? startDate, DateTime? estimatedCompletionDate)
    {
        if (startDate is null || estimatedCompletionDate is null) return true;
        return estimatedCompletionDate.Value.Date >= startDate.Value.Date;
    }

    public static DateTime? NormalizeDate(DateTime? value)
    {
        if (value is null) return null;
        var dt = value.Value;
        return dt.Kind switch
        {
            DateTimeKind.Utc => dt,
            DateTimeKind.Unspecified => DateTime.SpecifyKind(dt, DateTimeKind.Utc),
            _ => dt.ToUniversalTime()
        };
    }

    public static string NormalizeMaintenanceType(string? value) =>
        IsValidMaintenanceType(value) ? value!.Trim() : "Preventive";
}
