using System.Globalization;
using SheikhTravelSystem.Application.Common;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Reports;

/// <summary>The three permission groups a report belongs to (each has its own route and permission).</summary>
public enum TravelReportGroup { Operational, Finance, Profit }

public sealed record TravelReportDefinition(string Type, string Title, TravelReportGroup Group, bool Windowed, string WindowLabel, string Description);

/// <summary>The twelve reports of the brief, with the permission group and whether a date window is required.</summary>
public static class TravelReportCatalog
{
    public static readonly IReadOnlyList<TravelReportDefinition> All =
    [
        new("enquiry-performance", "Enquiry performance", TravelReportGroup.Operational, true, "created", "Per agent: enquiries, won, lost, conversion and days to win."),
        new("visa-processing", "Visa processing", TravelReportGroup.Operational, true, "submitted", "Per country and visa type: submitted, approved, rejected, pending and processing days."),
        new("country-applications", "Country-wise applications", TravelReportGroup.Operational, true, "created", "Per destination: cases, applicants and decisions."),
        new("pending-documents", "Pending documents", TravelReportGroup.Operational, true, "intended travel", "Open cases with mandatory documents outstanding."),
        new("group-manifests", "Umrah group manifests", TravelReportGroup.Operational, true, "departure", "Groups departing in the window: pilgrims, visas, tickets (no passport data)."),
        new("bookings", "Flight and hotel bookings", TravelReportGroup.Operational, true, "travel", "Services travelling in the window per supplier and status, with sell and cost per currency."),
        new("agent-branch-performance", "Branch and agent performance", TravelReportGroup.Operational, true, "created / issued", "Per agent or branch: enquiries, won, cases, files, invoiced and collected."),
        new("customer-balances", "Customer balances", TravelReportGroup.Finance, false, "as of today", "Open balances per customer and currency."),
        new("supplier-balances", "Supplier balances", TravelReportGroup.Finance, false, "as of today", "What is owed to each supplier per currency."),
        new("pilgrim-payments", "Pilgrim payments", TravelReportGroup.Finance, true, "departure", "Per group: quoted, invoiced, paid and balance per currency."),
        new("commissions", "Commission report", TravelReportGroup.Profit, true, "issued", "Per beneficiary: accrued, payable and paid out."),
        new("profit-loss", "Profit and loss", TravelReportGroup.Profit, true, "travel", "Per travel month: revenue, cost, commission and margin in the reporting currency."),
    ];

    public static TravelReportDefinition? Find(string? type) => All.FirstOrDefault(d => d.Type == type);
}

/// <summary>Builds the self-describing <see cref="ReportResponseDto"/> and checks every column key exists in every row.</summary>
public sealed class TravelReportBuilder(TravelReportDefinition definition)
{
    private readonly List<ReportColumnDto> _columns = [];
    private readonly List<ReportRowDto> _rows = [];
    private readonly Dictionary<string, object?> _summary = new(StringComparer.Ordinal);

    public TravelReportBuilder Column(string key, string label, string format = "text")
    {
        _columns.Add(new ReportColumnDto(key, label, format));
        return this;
    }

    /// <summary>Adds a row; <paramref name="totalValue"/> is what the row "weighs" (amount or count) for the grand total.</summary>
    public TravelReportBuilder Row(string key, string label, int count, decimal totalValue, IReadOnlyDictionary<string, object?> fields)
    {
        var missing = _columns.Select(c => c.Key).Where(k => !fields.ContainsKey(k)).ToList();
        if (missing.Count > 0) throw new InvalidOperationException($"Report {definition.Type} row {key} lacks fields: {string.Join(", ", missing)}");
        _rows.Add(new ReportRowDto(key, label, count, totalValue, fields));
        return this;
    }

    public TravelReportBuilder Summary(string key, object? value)
    {
        _summary[key] = value;
        return this;
    }

    /// <summary>Sums a numeric column over the rows into the summary under the same key.</summary>
    public TravelReportBuilder SumColumn(string key)
    {
        _summary[key] = _rows.Sum(r => r.Fields.TryGetValue(key, out var v) && v is not null ? Convert.ToDecimal(v, CultureInfo.InvariantCulture) : 0m);
        return this;
    }

    public ReportResponseDto Build(DateTime? windowFrom, DateTime? windowTo, DateTime asOf)
    {
        _summary["rows"] = _rows.Count;
        _summary["windowLabel"] = definition.WindowLabel;
        _summary["windowFrom"] = windowFrom?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        _summary["windowTo"] = windowTo?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        _summary["asOf"] = asOf.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return new ReportResponseDto(definition.Type, definition.Title, _columns, _rows, _rows.Sum(r => r.TotalValue), _summary);
    }

    public static IReadOnlyDictionary<string, object?> Fields(params (string Key, object? Value)[] values)
        => values.ToDictionary(v => v.Key, v => v.Value, StringComparer.Ordinal);
}
