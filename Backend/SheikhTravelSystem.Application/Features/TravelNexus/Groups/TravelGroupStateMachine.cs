namespace SheikhTravelSystem.Application.Features.TravelNexus.Groups;

/// <summary>
/// Single owner of Umrah group status transitions (audit SG-17).
/// <code>
/// Draft → Open → RegistrationClosed → VisaProcessing → Ready → Departed → Returned → Closed
///          ↑__Reopen__↓                        ↑__Unready__↓
/// Draft … Ready → Cancelled (reason)
/// </code>
/// Business guards (package published, visa approvals, rooming…) are checked by the transition handler.
/// </summary>
public static class TravelGroupStateMachine
{
    public static class Actions
    {
        public const string Edit = "Edit";
        public const string Open = "Open";
        public const string CloseRegistration = "CloseRegistration";
        public const string Reopen = "Reopen";
        public const string StartVisaProcessing = "StartVisaProcessing";
        public const string MarkReady = "MarkReady";
        public const string Unready = "Unready";
        public const string Depart = "Depart";
        public const string Return = "Return";
        public const string Close = "Close";
        public const string Cancel = "Cancel";
        public const string RegisterMembers = "RegisterMembers";
        public const string ManageMembers = "ManageMembers";
        public const string ManageStays = "ManageStays";
        public const string ManageRooms = "ManageRooms";
        public const string ManageTransport = "ManageTransport";
    }

    /// <summary>Actions accepted by the generic transition command (StartVisaProcessing has its own handler path).</summary>
    public static readonly string[] TransitionActions =
        [Actions.Open, Actions.CloseRegistration, Actions.Reopen, Actions.StartVisaProcessing, Actions.MarkReady, Actions.Unready,
         Actions.Depart, Actions.Return, Actions.Close, Actions.Cancel];

    private static readonly string[] Cancellable = ["Draft", "Open", "RegistrationClosed", "VisaProcessing", "Ready"];
    public static readonly string[] HeaderEditable = ["Draft", "Open", "RegistrationClosed"];

    public static bool IsTerminal(string status) => status is "Closed" or "Cancelled";

    public static string? Target(string status, string action) => (status, action) switch
    {
        ("Draft", Actions.Open) => "Open",
        ("Open", Actions.CloseRegistration) => "RegistrationClosed",
        ("RegistrationClosed", Actions.Reopen) => "Open",
        ("RegistrationClosed", Actions.StartVisaProcessing) => "VisaProcessing",
        ("VisaProcessing", Actions.MarkReady) => "Ready",
        ("Ready", Actions.Unready) => "VisaProcessing",
        ("Ready", Actions.Depart) => "Departed",
        ("Departed", Actions.Return) => "Returned",
        ("Returned", Actions.Close) => "Closed",
        (_, Actions.Cancel) when Cancellable.Contains(status) => "Cancelled",
        _ => null
    };

    public static bool CanRegister(string status) => status == "Open";
    public static bool CanManageMembers(string status) => Cancellable.Contains(status);
    public static bool CanManageStays(string status) => status is "Draft" or "Open" or "RegistrationClosed" or "VisaProcessing";
    public static bool CanManageRooms(string status) => status is "Open" or "RegistrationClosed" or "VisaProcessing" or "Ready";
    public static bool CanManageTransport(string status) => !IsTerminal(status) && status != "Returned";

    public static IReadOnlyList<string> AllowedActions(string status)
    {
        var actions = new List<string>();
        if (!IsTerminal(status)) actions.Add(Actions.Edit);
        if (CanRegister(status)) actions.Add(Actions.RegisterMembers);
        if (CanManageMembers(status)) actions.Add(Actions.ManageMembers);
        if (CanManageStays(status)) actions.Add(Actions.ManageStays);
        if (CanManageRooms(status)) actions.Add(Actions.ManageRooms);
        if (CanManageTransport(status)) actions.Add(Actions.ManageTransport);
        actions.AddRange(TransitionActions.Where(a => Target(status, a) is not null));
        return actions;
    }
}
