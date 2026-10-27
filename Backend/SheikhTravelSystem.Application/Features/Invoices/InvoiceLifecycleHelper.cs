using SheikhTravelSystem.Application.Features.SmartOps;

namespace SheikhTravelSystem.Application.Features.Invoices;

/// <summary>
/// Customer invoice payment lifecycle (pending / paid / overdue) on top of stored status rows.
/// </summary>
public static class InvoiceLifecycleHelper
{
    public const int DefaultDueDays = 14;

    public static DateTime DefaultDueDateUtc(DateTime issuedAtUtc) =>
        issuedAtUtc.Date.AddDays(DefaultDueDays);

    public static bool IsOpen(string? status) =>
        !string.IsNullOrWhiteSpace(status)
        && !status.Equals("Void", StringComparison.OrdinalIgnoreCase)
        && !status.Equals("Paid", StringComparison.OrdinalIgnoreCase)
        && !status.Equals("Draft", StringComparison.OrdinalIgnoreCase);

    public static bool IsOverdue(
        string? status,
        decimal balance,
        DateTime? dueDateUtc,
        DateTime? issuedAtUtc,
        DateTime asOfUtc) =>
        IsOpen(status)
        && balance > 0
        && (status?.Equals("Overdue", StringComparison.OrdinalIgnoreCase) == true
            || SmartOpsRuleLogic.IsOverdueInvoice(dueDateUtc, issuedAtUtc, balance, asOfUtc));

    /// <summary>UI / API payment status: Pending, Paid, Overdue.</summary>
    public static string ResolvePaymentStatus(
        string? status,
        decimal balance,
        DateTime? dueDateUtc,
        DateTime? issuedAtUtc,
        DateTime asOfUtc)
    {
        if (string.Equals(status, "Paid", StringComparison.OrdinalIgnoreCase) || balance <= 0)
            return "Paid";
        if (IsOverdue(status, balance, dueDateUtc, issuedAtUtc, asOfUtc))
            return "Overdue";
        if (IsOpen(status))
            return "Pending";
        return status ?? "Pending";
    }

    public static string NormalizeStoredStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return "Pending";
        if (status.Equals("Issued", StringComparison.OrdinalIgnoreCase)) return "Pending";
        return status;
    }
}
