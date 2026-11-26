using SheikhTravelSystem.Application.Features.TravelNexus.Finance.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.Profitability.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.Reminders.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.SupplierFinance.DTOs;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Dashboard.DTOs;

/// <summary>Status counts of one pipeline, every status present (zero-filled); Total = sum.</summary>
public class TravelPipelineDto
{
    public IReadOnlyDictionary<string, int> Counts { get; set; } = new Dictionary<string, int>();
    public int Total { get; set; }
}

public class TravelCurrencyAmountDto
{
    public string CurrencyCode { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

/// <summary>Things needing action, counted as of today (the same candidate queries drive the reminders).</summary>
public class TravelAttentionDto
{
    public int FollowUpsDue { get; set; }
    public int MissingDocumentCases { get; set; }
    public int TicketingDueNext72Hours { get; set; }
    public int PassportsExpiring { get; set; }
    /// <summary>The tenant's passport warning window the count uses.</summary>
    public int PassportWarningDays { get; set; }
    public int AppointmentsNext7Days { get; set; }
    public int DeparturesNext7Days { get; set; }
    public int OverdueInvoices { get; set; }
    public IReadOnlyList<TravelCurrencyAmountDto> OverdueInvoiceAmounts { get; set; } = [];
    public int OverdueSupplierBills { get; set; }
    public int RemindersFailedLast7Days { get; set; }
    public int RemindersSkippedNoAgentLast7Days { get; set; }
}

public class TravelTrendPointDto
{
    public DateTime PeriodStart { get; set; }
    public string Label { get; set; } = string.Empty;
    public int Total { get; set; }
    public int Won { get; set; }
}

public class TravelMoneyTrendPointDto
{
    public DateTime PeriodStart { get; set; }
    public string Label { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = string.Empty;
    public decimal Issued { get; set; }
    public decimal Collected { get; set; }
}

public class TravelDecisionTrendPointDto
{
    public DateTime PeriodStart { get; set; }
    public string Label { get; set; } = string.Empty;
    public int Approved { get; set; }
    public int Rejected { get; set; }
}

public class TravelTrendsDto
{
    /// <summary>Enquiries created per ISO week, last 8 weeks (Won = those won so far).</summary>
    public IReadOnlyList<TravelTrendPointDto> Enquiries { get; set; } = [];
    /// <summary>Invoices issued and payments cleared per month and currency, last 6 months.</summary>
    public IReadOnlyList<TravelMoneyTrendPointDto> Invoicing { get; set; } = [];
    /// <summary>Applicant decisions per month, last 6 months.</summary>
    public IReadOnlyList<TravelDecisionTrendPointDto> VisaDecisions { get; set; } = [];
    public int EnquiryWeeks { get; set; }
    public int Months { get; set; }
}

public class TravelDashboardDto
{
    public DateTime AsOf { get; set; }
    /// <summary>Current counts, all time.</summary>
    public TravelPipelineDto Enquiries { get; set; } = new();
    public TravelPipelineDto VisaCases { get; set; } = new();
    public TravelPipelineDto Groups { get; set; } = new();
    public TravelPipelineDto Bookings { get; set; } = new();
    public TravelAttentionDto Attention { get; set; } = new();
    /// <summary>Per-currency receivables (outstanding all time, overdue as of today, collected this month).</summary>
    public IReadOnlyList<TravelFinanceCurrencySummaryDto> Receivables { get; set; } = [];
    public IReadOnlyList<TravelPayablesCurrencySummaryDto> Payables { get; set; } = [];
    public TravelTrendsDto Trends { get; set; } = new();
}

public class TravelDashboardProfitDto
{
    public string ReportingCurrencyCode { get; set; } = string.Empty;
    /// <summary>Contexts whose travel date falls in the current calendar month.</summary>
    public TravelProfitGroupDto ThisMonth { get; set; } = new();
    public TravelProfitGroupDto LastMonth { get; set; } = new();
    public IReadOnlyList<TravelMissingRateDto> MissingRates { get; set; } = [];
}

public class TravelUpcomingDto
{
    public int Days { get; set; }
    public IReadOnlyList<TravelUpcomingItemDto> Items { get; set; } = [];
}
