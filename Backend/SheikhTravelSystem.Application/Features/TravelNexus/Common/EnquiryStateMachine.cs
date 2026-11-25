namespace SheikhTravelSystem.Application.Features.TravelNexus.Common;

/// <summary>
/// Single owner of enquiry status transitions (audit SG-17: one state machine per entity).
/// New → Contacted → Qualified → Quoted → Won (only via Convert) | Lost (reason required); Lost → Contacted (Reopen).
/// </summary>
public static class EnquiryStateMachine
{
    public static class Actions
    {
        public const string Edit = "Edit";
        public const string LogActivity = "LogActivity";
        public const string MarkContacted = "MarkContacted";
        public const string MarkQualified = "MarkQualified";
        public const string MarkQuoted = "MarkQuoted";
        public const string MarkLost = "MarkLost";
        public const string Reopen = "Reopen";
        public const string Convert = "Convert";
    }

    /// <summary>Actions accepted by the generic status-change command (Lost and Convert have their own commands).</summary>
    public static readonly string[] SimpleStatusActions =
        [Actions.MarkContacted, Actions.MarkQualified, Actions.MarkQuoted, Actions.Reopen];

    public static readonly string[] OpenStatuses = ["New", "Contacted", "Qualified", "Quoted"];

    public static bool IsOpen(string status) => OpenStatuses.Contains(status, StringComparer.Ordinal);

    /// <summary>Target status for an action from <paramref name="status"/>, or null when not allowed.</summary>
    public static string? Target(string status, string action) => (status, action) switch
    {
        ("New", Actions.MarkContacted) => "Contacted",
        ("New" or "Contacted", Actions.MarkQualified) => "Qualified",
        ("New" or "Contacted" or "Qualified", Actions.MarkQuoted) => "Quoted",
        (_, Actions.MarkLost) when IsOpen(status) => "Lost",
        (_, Actions.Convert) when IsOpen(status) => "Won",
        ("Lost", Actions.Reopen) => "Contacted",
        _ => null
    };

    public static IReadOnlyList<string> AllowedActions(string status)
    {
        var actions = new List<string> { Actions.LogActivity };
        if (IsOpen(status)) actions.Add(Actions.Edit);
        foreach (var a in new[] { Actions.MarkContacted, Actions.MarkQualified, Actions.MarkQuoted, Actions.MarkLost, Actions.Reopen, Actions.Convert })
        {
            if (Target(status, a) is not null) actions.Add(a);
        }
        return actions;
    }
}
