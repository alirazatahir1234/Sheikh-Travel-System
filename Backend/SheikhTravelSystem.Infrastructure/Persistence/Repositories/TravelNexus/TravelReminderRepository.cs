using Dapper;
using Microsoft.Data.SqlClient;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Reminders.DTOs;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories.TravelNexus;

/// <summary>
/// Reminder rules, the dispatch log and one candidate query per rule type. Every statement filters on TenantId. Candidate queries
/// return only entities whose trigger is still open (a paid invoice, a cancelled case or a ticketed flight stops reminding by itself)
/// and never expose passport or CNIC numbers (the passport is masked in the label).
/// </summary>
public sealed class TravelReminderRepository(IDbConnectionFactory dbFactory) : ITravelReminderRepository
{
    /// <summary>Passport number masked to its last three characters, in SQL (mirrors TravelPii.Mask).</summary>
    private const string MaskedPassport = "CASE WHEN t.PassportNumber IS NULL THEN N'' ELSE CONCAT(N'••••', RIGHT(t.PassportNumber, 3)) END";

    // ── Rules ───────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<TravelReminderRuleRow>> GetRulesAsync(int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return (await connection.QueryAsync<TravelReminderRuleRow>(new CommandDefinition("""
            SELECT RuleType, LeadDays, IsEnabled, StaffInApp, StaffEmail, CustomerEmail, CustomerSms, CustomerWhatsApp, CustomerTemplate
            FROM TravelReminderRules WHERE TenantId = @TenantId
            ORDER BY RuleType, LeadDays
            """, new { TenantId = tenantId }, cancellationToken: cancellationToken))).ToList();
    }

    public async Task SaveRuleAsync(int tenantId, string ruleType, SaveTravelReminderRuleDto dto, int? userId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();
        await connection.ExecuteAsync(new CommandDefinition("""
            DELETE FROM TravelReminderRules WHERE TenantId = @TenantId AND RuleType = @RuleType
            """, new { TenantId = tenantId, RuleType = ruleType }, tx, cancellationToken: cancellationToken));
        foreach (var l in dto.Leads)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO TravelReminderRules (TenantId, RuleType, LeadDays, IsEnabled, StaffInApp, StaffEmail, CustomerEmail, CustomerSms, CustomerWhatsApp,
                    CustomerTemplate, CreatedBy)
                VALUES (@TenantId, @RuleType, @LeadDays, @IsEnabled, @StaffInApp, @StaffEmail, @CustomerEmail, @CustomerSms, @CustomerWhatsApp, @Template, @UserId)
                """, new
                {
                    TenantId = tenantId, RuleType = ruleType, l.LeadDays, l.IsEnabled, l.StaffInApp, l.StaffEmail, l.CustomerEmail, l.CustomerSms, l.CustomerWhatsApp,
                    Template = dto.CustomerTemplate, UserId = userId
                }, tx, cancellationToken: cancellationToken));
        }
        tx.Commit();
    }

    // ── Candidates ──────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<TravelReminderCandidate>> GetCandidatesAsync(int tenantId, string ruleType, DateTime fromUtc, DateTime toUtc,
        TravelBranchScope? scope = null, CancellationToken cancellationToken = default)
    {
        var sql = ruleType switch
        {
            "PassportExpiry" => PassportExpiry, "VisaAppointment" => VisaAppointment, "MissingDocuments" => MissingDocuments,
            "VisaDecisionOverdue" => VisaDecisionOverdue, "VisaStatusChanged" => VisaStatusChanged, "TicketingDeadline" => TicketingDeadline,
            "FlightDeparture" => FlightDeparture, "HotelCheckIn" => HotelCheckIn, "InvoiceDue" => InvoiceDue, "InvoiceOverdue" => InvoiceOverdue,
            "EnquiryFollowUp" => EnquiryFollowUp, "GroupDeparture" => GroupDeparture, _ => null
        };
        if (sql is null) return [];
        using var connection = dbFactory.CreateConnection();
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        p.Add("From", fromUtc);
        p.Add("To", toUtc);
        p.Add("Today", DateTime.UtcNow.Date);
        var scopeClauses = new List<string> { "1 = 1" };
        if (scope is not null) TravelSql.ApplyBranchScope(scope, "x.BranchId", scopeClauses, p);
        var rows = await connection.QueryAsync<TravelReminderCandidate>(new CommandDefinition($"""
            SELECT x.* FROM ({sql}) x
            WHERE x.DueAt >= @From AND x.DueAt <= @To AND {string.Join(" AND ", scopeClauses)}
            ORDER BY x.DueAt, x.EntityId
            """, p, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    /// <summary>Customer contact for a customer id (the agent of the customer's profile is the staff fallback).</summary>
    private const string CustomerJoin = """
        LEFT JOIN Customers c ON c.Id = {0} AND c.TenantId = @TenantId
        LEFT JOIN TravelCustomerProfiles cp ON cp.CustomerId = c.Id AND cp.TenantId = @TenantId AND cp.IsDeleted = 0
        """;

    private static readonly string PassportExpiry = $"""
        SELECT N'Traveler' AS EntityType, t.Id AS EntityId, CONCAT(N'Passport ', {MaskedPassport}, N' · ', t.GivenNames, N' ', t.Surname) AS ContextLabel,
               CAST(t.PassportExpiryDate AS DATETIME2) AS DueAt, CAST(NULL AS NVARCHAR(40)) AS Stamp,
               cp.AssignedAgentUserId AS StaffUserId, c.Id AS CustomerId, c.FullName AS CustomerName,
               COALESCE(NULLIF(t.Phone, N''), c.Phone) AS CustomerPhone, COALESCE(NULLIF(t.Email, N''), c.Email) AS CustomerEmail,
               CONCAT(t.GivenNames, N' ', t.Surname) AS Reference, CAST(NULL AS DECIMAL(18,2)) AS Amount, CAST(NULL AS CHAR(3)) AS CurrencyCode,
               CONCAT(N'ending ', RIGHT(t.PassportNumber, 3)) AS Detail, cp.BranchId
        FROM TravelTravelers t
        {string.Format(CustomerJoin, "t.CustomerId")}
        WHERE t.TenantId = @TenantId AND t.IsDeleted = 0 AND t.PassportExpiryDate IS NOT NULL
        """;

    private static readonly string VisaAppointment = $"""
        SELECT N'VisaCase' AS EntityType, v.Id AS EntityId, CONCAT(N'Visa case ', v.CaseNumber) AS ContextLabel, v.AppointmentAt AS DueAt, CAST(NULL AS NVARCHAR(40)) AS Stamp,
               COALESCE(v.AssignedAgentUserId, cp.AssignedAgentUserId) AS StaffUserId, c.Id AS CustomerId, COALESCE(c.FullName, g.Name) AS CustomerName,
               COALESCE(c.Phone, g.LeaderPhone) AS CustomerPhone, c.Email AS CustomerEmail, v.CaseNumber AS Reference,
               CAST(NULL AS DECIMAL(18,2)) AS Amount, CAST(NULL AS CHAR(3)) AS CurrencyCode, CONCAT(co.Name, N' · ', vt.Name) AS Detail, v.BranchId
        FROM TravelVisaCases v
        {string.Format(CustomerJoin, "v.CustomerId")}
        LEFT JOIN TravelGroups g ON g.Id = v.GroupId AND g.TenantId = @TenantId
        INNER JOIN TravelCountries co ON co.Code = v.CountryCode
        INNER JOIN TravelVisaTypes vt ON vt.Id = v.VisaTypeId AND vt.TenantId = @TenantId
        WHERE v.TenantId = @TenantId AND v.IsDeleted = 0 AND v.AppointmentAt IS NOT NULL
          AND v.Status IN (N'Draft', N'DocumentsPending', N'DocumentsVerified', N'Submitted', N'InProcessing', N'OnHold')
        """;

    private static readonly string MissingDocuments = $"""
        SELECT N'VisaCase' AS EntityType, v.Id AS EntityId, CONCAT(N'Visa case ', v.CaseNumber) AS ContextLabel, CAST(v.IntendedTravelDate AS DATETIME2) AS DueAt,
               CAST(NULL AS NVARCHAR(40)) AS Stamp, COALESCE(v.AssignedAgentUserId, cp.AssignedAgentUserId) AS StaffUserId, c.Id AS CustomerId,
               COALESCE(c.FullName, g.Name) AS CustomerName, COALESCE(c.Phone, g.LeaderPhone) AS CustomerPhone, c.Email AS CustomerEmail, v.CaseNumber AS Reference,
               CAST(NULL AS DECIMAL(18,2)) AS Amount, CAST(NULL AS CHAR(3)) AS CurrencyCode, CONCAT(m.Outstanding, N' mandatory document(s) outstanding') AS Detail, v.BranchId
        FROM TravelVisaCases v
        INNER JOIN (SELECT r.VisaCaseId, COUNT(1) AS Outstanding FROM TravelApplicantDocuments r
                    INNER JOIN TravelVisaApplicants a ON a.Id = r.ApplicantId AND a.TenantId = r.TenantId AND a.IsDeleted = 0 AND a.Decision <> N'Withdrawn'
                    WHERE r.TenantId = @TenantId AND r.IsDeleted = 0 AND r.IsMandatory = 1 AND r.Status IN (N'Missing', N'Uploaded', N'Rejected')
                    GROUP BY r.VisaCaseId) m ON m.VisaCaseId = v.Id
        {string.Format(CustomerJoin, "v.CustomerId")}
        LEFT JOIN TravelGroups g ON g.Id = v.GroupId AND g.TenantId = @TenantId
        WHERE v.TenantId = @TenantId AND v.IsDeleted = 0 AND v.IntendedTravelDate IS NOT NULL AND v.Status IN (N'DocumentsPending', N'DocumentsVerified')
        """;

    private static readonly string VisaDecisionOverdue = $"""
        SELECT N'VisaCase' AS EntityType, v.Id AS EntityId, CONCAT(N'Visa case ', v.CaseNumber) AS ContextLabel, CAST(v.ExpectedDecisionDate AS DATETIME2) AS DueAt,
               CAST(NULL AS NVARCHAR(40)) AS Stamp, COALESCE(v.AssignedAgentUserId, cp.AssignedAgentUserId) AS StaffUserId, c.Id AS CustomerId,
               COALESCE(c.FullName, g.Name) AS CustomerName, c.Phone AS CustomerPhone, c.Email AS CustomerEmail, v.CaseNumber AS Reference,
               CAST(NULL AS DECIMAL(18,2)) AS Amount, CAST(NULL AS CHAR(3)) AS CurrencyCode, v.Status AS Detail, v.BranchId
        FROM TravelVisaCases v
        {string.Format(CustomerJoin, "v.CustomerId")}
        LEFT JOIN TravelGroups g ON g.Id = v.GroupId AND g.TenantId = @TenantId
        WHERE v.TenantId = @TenantId AND v.IsDeleted = 0 AND v.ExpectedDecisionDate IS NOT NULL AND v.Status IN (N'Submitted', N'InProcessing')
        """;

    private static readonly string VisaStatusChanged = $"""
        SELECT N'VisaCase' AS EntityType, v.Id AS EntityId, CONCAT(N'Visa case ', v.CaseNumber) AS ContextLabel, h.ChangedAt AS DueAt,
               CONCAT(N'H', h.Id) AS Stamp, COALESCE(v.AssignedAgentUserId, cp.AssignedAgentUserId) AS StaffUserId, c.Id AS CustomerId,
               COALESCE(c.FullName, g.Name) AS CustomerName, COALESCE(c.Phone, g.LeaderPhone) AS CustomerPhone, c.Email AS CustomerEmail, v.CaseNumber AS Reference,
               CAST(NULL AS DECIMAL(18,2)) AS Amount, CAST(NULL AS CHAR(3)) AS CurrencyCode,
               CASE h.ToValue WHEN N'Submitted' THEN N'submitted to the embassy' WHEN N'Approved' THEN N'approved' WHEN N'PartiallyApproved' THEN N'partially approved'
                    WHEN N'Rejected' THEN N'rejected' ELSE h.ToValue END AS Detail, v.BranchId
        FROM TravelVisaStatusHistory h
        INNER JOIN TravelVisaCases v ON v.Id = h.VisaCaseId AND v.TenantId = @TenantId AND v.IsDeleted = 0
        {string.Format(CustomerJoin, "v.CustomerId")}
        LEFT JOIN TravelGroups g ON g.Id = v.GroupId AND g.TenantId = @TenantId
        WHERE h.TenantId = @TenantId AND h.EventType = N'CaseStatus' AND h.ToValue IN (N'Submitted', N'Approved', N'PartiallyApproved', N'Rejected')
        """;

    /// <summary>Booking-service rows share the file's customer, agent and branch.</summary>
    private const string BookingJoin = """
        INNER JOIN TravelBookings b ON b.Id = {0}.BookingId AND b.TenantId = @TenantId AND b.IsDeleted = 0
        LEFT JOIN Customers c ON c.Id = b.CustomerId AND c.TenantId = @TenantId
        LEFT JOIN TravelCustomerProfiles cp ON cp.CustomerId = c.Id AND cp.TenantId = @TenantId AND cp.IsDeleted = 0
        LEFT JOIN TravelGroups g ON g.Id = b.GroupId AND g.TenantId = @TenantId
        """;

    private static readonly string TicketingDeadline = $"""
        SELECT N'FlightBooking' AS EntityType, f.Id AS EntityId, CONCAT(N'Flight', N' ' + f.Pnr, N' · ', b.BookingNumber) AS ContextLabel, f.TicketingTimeLimitUtc AS DueAt,
               CAST(NULL AS NVARCHAR(40)) AS Stamp, COALESCE(b.AssignedAgentUserId, cp.AssignedAgentUserId) AS StaffUserId, c.Id AS CustomerId,
               COALESCE(c.FullName, g.Name) AS CustomerName, c.Phone AS CustomerPhone, c.Email AS CustomerEmail, COALESCE(f.Pnr, b.BookingNumber) AS Reference,
               CAST(NULL AS DECIMAL(18,2)) AS Amount, CAST(NULL AS CHAR(3)) AS CurrencyCode, b.Title AS Detail, b.BranchId
        FROM TravelFlightBookings f
        {string.Format(BookingJoin, "f")}
        WHERE f.TenantId = @TenantId AND f.IsDeleted = 0 AND f.Status = N'OnHold' AND f.TicketingTimeLimitUtc IS NOT NULL
        """;

    private static readonly string FlightDeparture = $"""
        SELECT N'FlightBooking' AS EntityType, f.Id AS EntityId, CONCAT(N'Flight', N' ' + f.Pnr, N' · ', s.FromAirport, N'→', s.ToAirport) AS ContextLabel, s.DepartUtc AS DueAt,
               CAST(NULL AS NVARCHAR(40)) AS Stamp, COALESCE(b.AssignedAgentUserId, cp.AssignedAgentUserId) AS StaffUserId, c.Id AS CustomerId,
               COALESCE(c.FullName, g.Name) AS CustomerName, COALESCE(c.Phone, g.LeaderPhone) AS CustomerPhone, c.Email AS CustomerEmail,
               COALESCE(f.Pnr, b.BookingNumber) AS Reference, CAST(NULL AS DECIMAL(18,2)) AS Amount, CAST(NULL AS CHAR(3)) AS CurrencyCode,
               CONCAT(s.AirlineCode, s.FlightNumber, N' ', s.FromAirport, N'→', s.ToAirport) AS Detail, b.BranchId
        FROM TravelFlightBookings f
        INNER JOIN TravelFlightSegments s ON s.FlightBookingId = f.Id AND s.TenantId = @TenantId AND s.Sequence = 1
        {string.Format(BookingJoin, "f")}
        WHERE f.TenantId = @TenantId AND f.IsDeleted = 0 AND f.Status IN (N'Confirmed', N'Ticketed')
        """;

    private static readonly string HotelCheckIn = $"""
        SELECT N'HotelBooking' AS EntityType, h.Id AS EntityId, CONCAT(N'Hotel ', ht.Name, N' · ', b.BookingNumber) AS ContextLabel, CAST(h.CheckIn AS DATETIME2) AS DueAt,
               CAST(NULL AS NVARCHAR(40)) AS Stamp, COALESCE(b.AssignedAgentUserId, cp.AssignedAgentUserId) AS StaffUserId, c.Id AS CustomerId,
               COALESCE(c.FullName, g.Name) AS CustomerName, COALESCE(c.Phone, g.LeaderPhone) AS CustomerPhone, c.Email AS CustomerEmail,
               COALESCE(h.ConfirmationNumber, b.BookingNumber) AS Reference, CAST(NULL AS DECIMAL(18,2)) AS Amount, CAST(NULL AS CHAR(3)) AS CurrencyCode,
               CONCAT(ht.Name, N', ', ht.City) AS Detail, b.BranchId
        FROM TravelHotelBookings h
        INNER JOIN TravelHotels ht ON ht.Id = h.HotelId AND ht.TenantId = @TenantId
        {string.Format(BookingJoin, "h")}
        WHERE h.TenantId = @TenantId AND h.IsDeleted = 0 AND h.Status = N'Confirmed'
        """;

    /// <summary>Issued invoices with a balance; the trigger is the first installment not yet covered, else the invoice due date.</summary>
    private static readonly string InvoiceBase = $"""
        SELECT i.Id, i.InvoiceNumber, i.CustomerId, i.CurrencyCode, i.AgentUserId, i.BranchId, d.Balance,
               COALESCE((SELECT TOP 1 ins.DueDate FROM (
                            SELECT ins.DueDate, SUM(ins.Amount) OVER (ORDER BY ins.Sequence ROWS UNBOUNDED PRECEDING) AS Cumulative
                            FROM TravelInvoiceInstallments ins WHERE ins.InvoiceId = i.Id AND ins.TenantId = i.TenantId) ins
                         WHERE ins.Cumulative > d.Covered ORDER BY ins.DueDate), i.DueDate) AS NextDue
        FROM TravelInvoices i
        {TravelInvoiceRepository.Money}
        WHERE i.TenantId = @TenantId AND i.IsDeleted = 0 AND i.Status = N'Issued' AND d.Balance > 0
        """;

    private static readonly string InvoiceCandidate = $"""
        SELECT N'Invoice' AS EntityType, x.Id AS EntityId, CONCAT(N'Invoice ', x.InvoiceNumber) AS ContextLabel, CAST(x.NextDue AS DATETIME2) AS DueAt,
               CAST(NULL AS NVARCHAR(40)) AS Stamp, COALESCE(x.AgentUserId, cp.AssignedAgentUserId) AS StaffUserId, c.Id AS CustomerId, c.FullName AS CustomerName,
               c.Phone AS CustomerPhone, c.Email AS CustomerEmail, x.InvoiceNumber AS Reference, x.Balance AS Amount, x.CurrencyCode, CAST(NULL AS NVARCHAR(200)) AS Detail, x.BranchId
        FROM ({InvoiceBase}) x
        {string.Format(CustomerJoin, "x.CustomerId")}
        WHERE x.NextDue IS NOT NULL
        """;

    private static readonly string InvoiceDue = InvoiceCandidate;
    private static readonly string InvoiceOverdue = InvoiceCandidate;

    private static readonly string EnquiryFollowUp = $"""
        SELECT N'Enquiry' AS EntityType, e.Id AS EntityId, CONCAT(N'Enquiry ', e.EnquiryNumber) AS ContextLabel, e.NextFollowUpAt AS DueAt, CAST(NULL AS NVARCHAR(40)) AS Stamp,
               COALESCE(e.AssignedAgentUserId, cp.AssignedAgentUserId) AS StaffUserId, c.Id AS CustomerId, COALESCE(c.FullName, e.LeadName) AS CustomerName,
               COALESCE(c.Phone, e.LeadPhone) AS CustomerPhone, COALESCE(c.Email, e.LeadEmail) AS CustomerEmail, e.EnquiryNumber AS Reference,
               CAST(NULL AS DECIMAL(18,2)) AS Amount, CAST(NULL AS CHAR(3)) AS CurrencyCode, e.ServiceType AS Detail, e.BranchId
        FROM TravelEnquiries e
        {string.Format(CustomerJoin, "e.CustomerId")}
        WHERE e.TenantId = @TenantId AND e.IsDeleted = 0 AND e.NextFollowUpAt IS NOT NULL AND e.Status NOT IN (N'Won', N'Lost')
        """;

    private const string GroupDeparture = """
        SELECT N'Group' AS EntityType, g.Id AS EntityId, CONCAT(N'Group ', g.GroupNumber, N' · ', g.Name) AS ContextLabel, CAST(g.DepartureDate AS DATETIME2) AS DueAt,
               CAST(NULL AS NVARCHAR(40)) AS Stamp, CAST(NULL AS INT) AS StaffUserId, CAST(NULL AS INT) AS CustomerId, g.LeaderName AS CustomerName,
               g.LeaderPhone AS CustomerPhone, CAST(NULL AS NVARCHAR(200)) AS CustomerEmail, g.GroupNumber AS Reference,
               CAST(NULL AS DECIMAL(18,2)) AS Amount, CAST(NULL AS CHAR(3)) AS CurrencyCode,
               CONCAT((SELECT COUNT(1) FROM TravelGroupMembers m WHERE m.GroupId = g.Id AND m.TenantId = @TenantId AND m.IsDeleted = 0 AND m.Status = N'Confirmed'), N' confirmed pilgrims') AS Detail,
               g.BranchId
        FROM TravelGroups g
        WHERE g.TenantId = @TenantId AND g.IsDeleted = 0 AND g.Status IN (N'Open', N'RegistrationClosed', N'VisaProcessing', N'Ready')
        """;

    // ── Dispatch log ────────────────────────────────────────────────────────

    public async Task<long?> TryClaimAsync(int tenantId, TravelReminderClaim claim, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var exists = await connection.ExecuteScalarAsync<long?>(new CommandDefinition(
            "SELECT Id FROM TravelReminders WHERE TenantId = @TenantId AND DedupeKey = @Key",
            new { TenantId = tenantId, Key = claim.DedupeKey }, cancellationToken: cancellationToken));
        if (exists is not null) return null;
        try
        {
            var c = claim.Candidate;
            return await connection.ExecuteScalarAsync<long>(new CommandDefinition("""
                INSERT INTO TravelReminders (TenantId, RuleType, LeadDays, EntityType, EntityId, ContextLabel, DueAt, Audience, DedupeKey, RecipientUserId,
                    RecipientCustomerId, Channels, Status, Title, Body)
                OUTPUT INSERTED.Id
                VALUES (@TenantId, @RuleType, @LeadDays, @EntityType, @EntityId, LEFT(@ContextLabel, 200), @DueAt, @Audience, @Key, @UserId, @CustomerId,
                    @Channels, N'Pending', LEFT(@Title, 200), LEFT(@Body, 2000))
                """, new
                {
                    TenantId = tenantId, claim.RuleType, claim.LeadDays, c.EntityType, c.EntityId, c.ContextLabel, c.DueAt, claim.Audience, Key = claim.DedupeKey,
                    UserId = claim.Audience == "Staff" ? c.StaffUserId : null, CustomerId = claim.Audience == "Customer" ? c.CustomerId : null,
                    Channels = string.Join(' ', claim.Channels), claim.Title, claim.Body
                }, cancellationToken: cancellationToken));
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            return null; // UX_TravelReminders_Tenant_Dedupe: a concurrent pass claimed it
        }
    }

    public async Task MarkAsync(int tenantId, long id, string status, int? notificationId, string? failureReason, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE TravelReminders SET Status = @Status, NotificationId = @NotificationId, FailureReason = LEFT(@FailureReason, 500)
            WHERE Id = @Id AND TenantId = @TenantId
            """, new { Id = id, TenantId = tenantId, Status = status, NotificationId = notificationId, FailureReason = failureReason }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<TravelReminderLogItemDto>> GetLogAsync(int tenantId, TravelReminderLogFilter f, int page, int pageSize,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        var clauses = new List<string> { "r.TenantId = @TenantId" };
        if (f.RuleType is not null) { clauses.Add("r.RuleType = @RuleType"); p.Add("RuleType", f.RuleType); }
        if (f.Status is not null) { clauses.Add("r.Status = @Status"); p.Add("Status", f.Status); }
        if (f.Audience is not null) { clauses.Add("r.Audience = @Audience"); p.Add("Audience", f.Audience); }
        if (f.From.HasValue) { clauses.Add("r.CreatedAt >= @From"); p.Add("From", f.From.Value.Date); }
        if (f.To.HasValue) { clauses.Add("r.CreatedAt < @To"); p.Add("To", f.To.Value.Date.AddDays(1)); }
        var where = string.Join(" AND ", clauses);
        var total = await connection.ExecuteScalarAsync<int>(new CommandDefinition($"SELECT COUNT(1) FROM TravelReminders r WHERE r.TenantId = @TenantId AND {where}", p,
            cancellationToken: cancellationToken));
        p.Add("Offset", (page - 1) * pageSize);
        p.Add("PageSize", pageSize);
        var items = (await connection.QueryAsync<TravelReminderLogItemDto>(new CommandDefinition($"""
            SELECT r.Id, r.RuleType, r.LeadDays, r.EntityType, r.EntityId, r.ContextLabel, r.DueAt, r.Audience, r.RecipientUserId, u.FullName AS RecipientUserName,
                   r.RecipientCustomerId, c.FullName AS RecipientCustomerName, r.Channels, r.Status, r.FailureReason, r.NotificationId, r.Title, r.Body, r.CreatedAt
            FROM TravelReminders r
            LEFT JOIN Users u ON u.Id = r.RecipientUserId AND u.TenantId = r.TenantId
            LEFT JOIN Customers c ON c.Id = r.RecipientCustomerId AND c.TenantId = r.TenantId
            WHERE r.TenantId = @TenantId AND {where}
            ORDER BY r.CreatedAt DESC, r.Id DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
            """, p, cancellationToken: cancellationToken))).ToList();
        return new PagedResult<TravelReminderLogItemDto> { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
    }

    public async Task<IReadOnlyDictionary<string, int>> GetStatusCountsAsync(int tenantId, DateTime sinceUtc, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.QueryAsync<(string Status, int Total)>(new CommandDefinition("""
            SELECT Status, COUNT(1) FROM TravelReminders WHERE TenantId = @TenantId AND CreatedAt >= @Since GROUP BY Status
            """, new { TenantId = tenantId, Since = sinceUtc }, cancellationToken: cancellationToken));
        return rows.ToDictionary(r => r.Status, r => r.Total);
    }

    public async Task<int> CountSkippedNoAgentAsync(int tenantId, DateTime sinceUtc, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT COUNT(1) FROM TravelReminders
            WHERE TenantId = @TenantId AND CreatedAt >= @Since AND Status = N'Skipped' AND Audience = N'Staff' AND FailureReason = @Reason
            """, new { TenantId = tenantId, Since = sinceUtc, Reason = Application.Features.TravelNexus.Reminders.TravelReminderDispatcher.NoAgentReason },
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<int>> GetActiveTenantIdsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return (await connection.QueryAsync<int>(new CommandDefinition("""
            SELECT Id FROM Tenants WHERE IsActive = 1 AND (Status = N'Active' OR Status IS NULL)
            """, cancellationToken: cancellationToken))).ToList();
    }
}
