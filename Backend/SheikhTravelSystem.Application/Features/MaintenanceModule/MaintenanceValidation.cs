using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.MaintenanceModule;

public static class MaintenanceValidation
{
    private static readonly HashSet<string> OpenWorkOrderStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Draft", "Open", "Assigned", "InProgress", "WaitingParts"
    };

    private static readonly Dictionary<string, HashSet<string>> AllowedTransitions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Draft"] = ["Open", "Cancelled"],
        ["Open"] = ["Assigned", "InProgress", "Cancelled"],
        ["Assigned"] = ["InProgress", "WaitingParts", "Cancelled"],
        ["InProgress"] = ["WaitingParts", "Completed", "Cancelled"],
        ["WaitingParts"] = ["InProgress", "Completed", "Cancelled"],
        ["Completed"] = ["Closed"],
        ["Closed"] = [],
        ["Cancelled"] = []
    };

    public static bool IsOpenWorkOrderStatus(string status) =>
        OpenWorkOrderStatuses.Contains(status);

    public static bool CanTransition(string currentStatus, string newStatus)
    {
        if (string.Equals(currentStatus, newStatus, StringComparison.OrdinalIgnoreCase))
            return true;

        return AllowedTransitions.TryGetValue(currentStatus, out var allowed) &&
               allowed.Contains(newStatus);
    }

    public static bool ShouldSetVehicleMaintenance(string newStatus) =>
        string.Equals(newStatus, "InProgress", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(newStatus, "WaitingParts", StringComparison.OrdinalIgnoreCase);

    public static bool IsTerminalWorkOrderStatus(string status) =>
        string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, "Closed", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, "Cancelled", StringComparison.OrdinalIgnoreCase);

    public static int VehicleMaintenanceStatus => (int)VehicleStatus.Maintenance;
    public static int VehicleAvailableStatus => (int)VehicleStatus.Available;

    public static DateTime? ComputeNextDueDate(string intervalType, int intervalValue, DateTime? lastService)
    {
        var baseDate = lastService ?? DateTime.UtcNow;
        return intervalType.ToLowerInvariant() switch
        {
            "months" or "month" => baseDate.AddMonths(intervalValue),
            "days" or "day" => baseDate.AddDays(intervalValue),
            _ => baseDate.AddMonths(intervalValue)
        };
    }
}
