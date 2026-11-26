namespace SheikhTravelSystem.Application.Features.TravelNexus.Bookings;

/// <summary>
/// Booking file: Open → Completed (every non-cancelled service finished, last service in the past) · Completed → Open (Reopen)
/// · Open → Cancelled (reason; every service cancelled). Services change only while the file is Open.
/// </summary>
public static class TravelBookingStateMachine
{
    public static class Actions
    {
        public const string Edit = "Edit";
        public const string Complete = "Complete";
        public const string Reopen = "Reopen";
        public const string Cancel = "Cancel";
        public const string ManageServices = "ManageServices";
    }

    public static readonly string[] TransitionActions = [Actions.Complete, Actions.Reopen, Actions.Cancel];

    public static string? Target(string status, string action) => (status, action) switch
    {
        ("Open", Actions.Complete) => "Completed",
        ("Open", Actions.Cancel) => "Cancelled",
        ("Completed", Actions.Reopen) => "Open",
        _ => null
    };

    public static bool IsOpen(string status) => status == "Open";

    public static IReadOnlyList<string> AllowedActions(string status)
    {
        var actions = TransitionActions.Where(a => Target(status, a) is not null).ToList();
        if (IsOpen(status)) actions.InsertRange(0, [Actions.Edit, Actions.ManageServices]);
        return actions;
    }
}

/// <summary>
/// Flight: Requested → OnHold (PNR + ticketing time limit) → Confirmed → Ticketed; Requested → Confirmed directly;
/// Cancel from any open status. Ticket needs ≥1 segment and every passenger's ticket issued (checked by the handler).
/// </summary>
public static class TravelFlightStateMachine
{
    public static class Actions
    {
        public const string Hold = "Hold";
        public const string Confirm = "Confirm";
        public const string Ticket = "Ticket";
        public const string Cancel = "Cancel";
        public const string Edit = "Edit";
        public const string EditPassengers = "EditPassengers";
        public const string IssueTickets = "IssueTickets";
        public const string CancelPassenger = "CancelPassenger";
        public const string Delete = "Delete";
    }

    public static readonly string[] TransitionActions = [Actions.Hold, Actions.Confirm, Actions.Ticket];

    public static string? Target(string status, string action) => (status, action) switch
    {
        ("Requested", Actions.Hold) => "OnHold",
        ("OnHold", Actions.Hold) => "OnHold", // extend the time limit
        ("Requested" or "OnHold", Actions.Confirm) => "Confirmed",
        ("Confirmed", Actions.Ticket) => "Ticketed",
        _ => null
    };

    public static bool IsOpen(string status) => status != "Cancelled";
    /// <summary>Segments, amounts and notes (schedule changes are allowed after ticketing).</summary>
    public static bool CanEdit(string status) => status != "Cancelled";
    /// <summary>Passengers are fixed once ticketed; changes then go through passenger cancellation.</summary>
    public static bool CanEditPassengers(string status) => status is "Requested" or "OnHold" or "Confirmed";
    public static bool CanIssueTickets(string status) => status is "Confirmed" or "Ticketed";
    public static bool CanCancelPassenger(string status) => status == "Ticketed";

    public static IReadOnlyList<string> AllowedActions(string status, bool fileOpen)
    {
        if (!fileOpen) return [];
        var actions = new List<string>();
        if (CanEdit(status)) actions.Add(Actions.Edit);
        if (CanEditPassengers(status)) actions.Add(Actions.EditPassengers);
        actions.AddRange(TransitionActions.Where(a => Target(status, a) is not null));
        if (CanIssueTickets(status)) actions.Add(Actions.IssueTickets);
        if (CanCancelPassenger(status)) actions.Add(Actions.CancelPassenger);
        if (IsOpen(status)) actions.Add(Actions.Cancel);
        if (status == "Requested") actions.Add(Actions.Delete);
        return actions;
    }
}

/// <summary>
/// Hotel: Requested → Confirmed (confirmation number) → CheckedIn (on/after check-in) → CheckedOut;
/// Confirmed → NoShow (after check-in date); Cancel from Requested / Confirmed.
/// </summary>
public static class TravelHotelStateMachine
{
    public static class Actions
    {
        public const string Confirm = "Confirm";
        public const string CheckIn = "CheckIn";
        public const string CheckOut = "CheckOut";
        public const string NoShow = "NoShow";
        public const string Cancel = "Cancel";
        public const string Edit = "Edit";
        public const string Delete = "Delete";
    }

    public static readonly string[] TransitionActions = [Actions.Confirm, Actions.CheckIn, Actions.CheckOut, Actions.NoShow];

    public static string? Target(string status, string action) => (status, action) switch
    {
        ("Requested", Actions.Confirm) => "Confirmed",
        ("Confirmed", Actions.Confirm) => "Confirmed", // correct the confirmation number
        ("Confirmed", Actions.CheckIn) => "CheckedIn",
        ("CheckedIn", Actions.CheckOut) => "CheckedOut",
        ("Confirmed", Actions.NoShow) => "NoShow",
        _ => null
    };

    public static bool CanEdit(string status) => status is "Requested" or "Confirmed";
    public static bool CanCancel(string status) => status is "Requested" or "Confirmed";
    public static bool IsFinished(string status) => status is "CheckedOut" or "NoShow" or "Cancelled";

    public static IReadOnlyList<string> AllowedActions(string status, bool fileOpen, DateTime checkIn, DateTime today)
    {
        if (!fileOpen) return [];
        var actions = new List<string>();
        if (CanEdit(status)) actions.Add(Actions.Edit);
        foreach (var a in TransitionActions)
        {
            if (Target(status, a) is null) continue;
            if (a is Actions.CheckIn or Actions.NoShow && today < checkIn.Date) continue;
            actions.Add(a);
        }
        if (CanCancel(status)) actions.Add(Actions.Cancel);
        if (status == "Requested") actions.Add(Actions.Delete);
        return actions;
    }
}

/// <summary>Refund tracking after a cancellation (money moves in Phase 6).</summary>
public static class TravelRefundStateMachine
{
    public static IReadOnlyList<string> Next(string status) => status switch
    {
        "Pending" => ["ReceivedFromSupplier", "PaidToCustomer", "Closed"],
        "ReceivedFromSupplier" => ["PaidToCustomer", "Closed"],
        "PaidToCustomer" => ["Closed"],
        _ => []
    };

    public static string Initial(decimal refundFromSupplier, decimal refundToCustomer)
        => refundFromSupplier > 0 || refundToCustomer > 0 ? "Pending" : "NotApplicable";
}
