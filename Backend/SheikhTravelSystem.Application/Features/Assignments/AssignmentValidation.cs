namespace SheikhTravelSystem.Application.Features.Assignments;

public static class AssignmentValidation
{
    public static string ResolveInitialStatus(DateTime startDateUtc, string? assignmentType)
    {
        if (assignmentType is "Temporary" or "Emergency")
            return "PendingApproval";

        if (startDateUtc > DateTime.UtcNow)
            return "Scheduled";

        return "Active";
    }

    public static bool IsOpenStatus(string status)
        => status is "Active" or "Scheduled" or "PendingApproval" or "Assigned";
}
