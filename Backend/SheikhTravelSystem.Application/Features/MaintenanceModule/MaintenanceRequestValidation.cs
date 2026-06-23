namespace SheikhTravelSystem.Application.Features.MaintenanceModule;

public static class MaintenanceRequestValidation
{
    public static readonly string[] AllowedPriorities = ["Low", "Medium", "High", "Critical"];
    public static readonly string[] AllowedRequestTypes = ["Corrective", "Preventive", "Breakdown"];
    public static readonly string[] AllowedIssueCategories =
    [
        "Engine", "Transmission", "Brake", "Tire", "Electrical", "Battery",
        "AC", "Body Damage", "Inspection", "Oil Change", "Breakdown", "Other"
    ];

    public const int DescriptionMinLength = 10;
    public const int DescriptionMaxLength = 2000;

    public static bool IsValidPriority(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        AllowedPriorities.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase);

    public static bool IsValidRequestType(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        AllowedRequestTypes.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase);

    public static bool IsValidIssueCategory(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        AllowedIssueCategories.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> TerminalStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Rejected", "Converted", "Cancelled"
    };

    private static readonly Dictionary<string, HashSet<string>> AllowedTransitions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Open"] = ["PendingApproval", "Approved", "Rejected", "Cancelled", "Converted"],
        ["PendingApproval"] = ["Approved", "Rejected", "Cancelled"],
        ["Approved"] = ["Converted", "InProgress", "Cancelled"],
        ["InProgress"] = ["Converted", "Cancelled"],
        ["Rejected"] = [],
        ["Converted"] = [],
        ["Cancelled"] = []
    };

    public static bool IsTerminal(string status) => TerminalStatuses.Contains(status);

    public static bool CanTransition(string current, string next)
    {
        if (string.Equals(current, next, StringComparison.OrdinalIgnoreCase)) return true;
        return AllowedTransitions.TryGetValue(current, out var allowed) && allowed.Contains(next);
    }

    public static bool CanApprove(string status) =>
        status.Equals("Open", StringComparison.OrdinalIgnoreCase) ||
        status.Equals("PendingApproval", StringComparison.OrdinalIgnoreCase);

    public static bool CanReject(string status) =>
        !IsTerminal(status) && !status.Equals("Converted", StringComparison.OrdinalIgnoreCase);

    public static bool CanConvert(string status) =>
        status.Equals("Open", StringComparison.OrdinalIgnoreCase) ||
        status.Equals("Approved", StringComparison.OrdinalIgnoreCase) ||
        status.Equals("InProgress", StringComparison.OrdinalIgnoreCase);

    public static void ValidateRejectReason(string priority, string? reason)
    {
        if (priority.Equals("Critical", StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(reason))
            throw new Common.Exceptions.ConflictException("Rejection reason is required for critical requests.");
    }
}
