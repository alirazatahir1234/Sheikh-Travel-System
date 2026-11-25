namespace SheikhTravelSystem.Application.Features.TravelNexus.Common;

/// <summary>
/// Single owner of visa case status transitions (audit SG-17).
/// <code>
/// Draft → DocumentsPending → DocumentsVerified → Submitted → InProcessing
///                  ↑________________↓ (ReopenDocuments)
/// any open status ↔ OnHold (Resume returns to the status it was held from)
/// any open status → Cancelled (reason required)
/// Approved | PartiallyApproved | Rejected are derived from applicant decisions, never set directly.
/// </code>
/// Phase 3 will additionally gate VerifyDocuments on a complete checklist.
/// </summary>
public static class VisaCaseStateMachine
{
    public static class Actions
    {
        public const string Edit = "Edit";
        public const string StartDocuments = "StartDocuments";
        public const string VerifyDocuments = "VerifyDocuments";
        public const string ReopenDocuments = "ReopenDocuments";
        public const string Submit = "Submit";
        public const string StartProcessing = "StartProcessing";
        public const string Hold = "Hold";
        public const string Resume = "Resume";
        public const string Cancel = "Cancel";
        public const string ManageApplicants = "ManageApplicants";
        public const string RecordDecision = "RecordDecision";
    }

    /// <summary>Actions accepted by the generic transition command (Submit and decisions have their own commands).</summary>
    public static readonly string[] TransitionActions =
    [
        Actions.StartDocuments, Actions.VerifyDocuments, Actions.ReopenDocuments,
        Actions.StartProcessing, Actions.Hold, Actions.Resume, Actions.Cancel
    ];

    public static readonly string[] TerminalStatuses = ["Approved", "PartiallyApproved", "Rejected", "Cancelled"];
    public static readonly string[] PreSubmissionStatuses = ["Draft", "DocumentsPending", "DocumentsVerified"];
    public static readonly string[] DecisionStatuses = ["Submitted", "InProcessing"];

    public static bool IsTerminal(string status) => TerminalStatuses.Contains(status, StringComparer.Ordinal);
    public static bool IsPreSubmission(string status) => PreSubmissionStatuses.Contains(status, StringComparer.Ordinal);
    public static bool AcceptsDecisions(string status) => DecisionStatuses.Contains(status, StringComparer.Ordinal);

    /// <summary>
    /// Target status for <paramref name="action"/>, or null when not allowed.
    /// <paramref name="onHoldFromStatus"/> is required to resolve <see cref="Actions.Resume"/>.
    /// </summary>
    public static string? Target(string status, string action, string? onHoldFromStatus = null) => (status, action) switch
    {
        ("Draft", Actions.StartDocuments) => "DocumentsPending",
        ("DocumentsPending", Actions.VerifyDocuments) => "DocumentsVerified",
        ("DocumentsVerified", Actions.ReopenDocuments) => "DocumentsPending",
        ("DocumentsVerified", Actions.Submit) => "Submitted",
        ("Submitted", Actions.StartProcessing) => "InProcessing",
        ("OnHold", Actions.Resume) => onHoldFromStatus,
        ("OnHold", Actions.Cancel) => "Cancelled",
        (_, Actions.Hold) when !IsTerminal(status) && status != "OnHold" => "OnHold",
        (_, Actions.Cancel) when !IsTerminal(status) => "Cancelled",
        _ => null
    };

    public static IReadOnlyList<string> AllowedActions(string status, string? onHoldFromStatus = null)
    {
        var actions = new List<string>();
        if (!IsTerminal(status)) actions.Add(Actions.Edit);
        if (IsPreSubmission(status)) actions.Add(Actions.ManageApplicants);
        if (AcceptsDecisions(status)) actions.Add(Actions.RecordDecision);
        foreach (var a in new[] { Actions.StartDocuments, Actions.VerifyDocuments, Actions.ReopenDocuments, Actions.Submit,
                     Actions.StartProcessing, Actions.Hold, Actions.Resume, Actions.Cancel })
        {
            if (Target(status, a, onHoldFromStatus) is not null) actions.Add(a);
        }
        return actions;
    }

    /// <summary>
    /// Case outcome once every non-withdrawn applicant is decided: all approved → Approved,
    /// all rejected → Rejected, otherwise PartiallyApproved. Null while any is pending or none remain.
    /// </summary>
    public static string? DeriveOutcome(IEnumerable<string> applicantDecisions)
    {
        var active = applicantDecisions.Where(d => d != "Withdrawn").ToList();
        if (active.Count == 0 || active.Any(d => d == "Pending")) return null;
        if (active.All(d => d == "Approved")) return "Approved";
        if (active.All(d => d == "Rejected")) return "Rejected";
        return "PartiallyApproved";
    }
}
