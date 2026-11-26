using Dapper;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories.TravelNexus;

/// <summary>
/// Report facts. Every statement filters on TenantId; the branch clamp is applied to the row that owns the data (enquiry, case, group,
/// booking file, invoice). Date windows are inclusive of both days. No passport or CNIC data is ever selected.
/// </summary>
public sealed class TravelReportRepository(IDbConnectionFactory dbFactory) : ITravelReportRepository
{
    private static DynamicParameters Params(int tenantId, TravelReportFilter f)
    {
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        p.Add("From", f.From.Date);
        p.Add("ToExclusive", f.To.Date.AddDays(1));
        p.Add("To", f.To.Date);
        p.Add("BranchId", f.BranchId);
        p.Add("AgentUserId", f.AgentUserId);
        p.Add("CountryCode", f.CountryCode);
        p.Add("Today", DateTime.UtcNow.Date);
        return p;
    }

    public async Task<IReadOnlyList<TravelEnquiryPerformanceRow>> GetEnquiryPerformanceAsync(int tenantId, TravelBranchScope scope, TravelReportFilter f, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        var p = Params(tenantId, f);
        var clauses = new List<string> { "e.TenantId = @TenantId", "e.IsDeleted = 0", "e.CreatedAt >= @From", "e.CreatedAt < @ToExclusive",
            "(@BranchId IS NULL OR e.BranchId = @BranchId)", "(@AgentUserId IS NULL OR e.AssignedAgentUserId = @AgentUserId)" };
        TravelSql.ApplyBranchScope(scope, "e.BranchId", clauses, p);
        return (await connection.QueryAsync<TravelEnquiryPerformanceRow>(new CommandDefinition($"""
            SELECT e.AssignedAgentUserId AS AgentUserId, u.FullName AS AgentName, br.Name AS BranchName, COUNT(1) AS Enquiries,
                   SUM(CASE WHEN e.Status = N'Won' THEN 1 ELSE 0 END) AS Won, SUM(CASE WHEN e.Status = N'Lost' THEN 1 ELSE 0 END) AS Lost,
                   SUM(CASE WHEN e.Status NOT IN (N'Won', N'Lost') THEN 1 ELSE 0 END) AS [Open],
                   AVG(CASE WHEN e.Status = N'Won' AND e.ClosedAt IS NOT NULL THEN CAST(DATEDIFF(HOUR, e.CreatedAt, e.ClosedAt) AS DECIMAL(18,2)) / 24 END) AS AvgDaysToWin
            FROM TravelEnquiries e
            LEFT JOIN Users u ON u.Id = e.AssignedAgentUserId AND u.TenantId = e.TenantId
            LEFT JOIN Branches br ON br.Id = e.BranchId AND br.TenantId = e.TenantId
            WHERE e.TenantId = @TenantId AND {string.Join(" AND ", clauses)}
            GROUP BY e.AssignedAgentUserId, u.FullName, br.Name
            ORDER BY Won DESC, Enquiries DESC
            """, p, cancellationToken: ct))).ToList();
    }

    public async Task<IReadOnlyList<TravelVisaProcessingRow>> GetVisaProcessingAsync(int tenantId, TravelBranchScope scope, TravelReportFilter f, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        var p = Params(tenantId, f);
        var clauses = new List<string> { "v.TenantId = @TenantId", "v.IsDeleted = 0", "v.SubmittedAt >= @From", "v.SubmittedAt < @ToExclusive",
            "(@BranchId IS NULL OR v.BranchId = @BranchId)", "(@AgentUserId IS NULL OR v.AssignedAgentUserId = @AgentUserId)", "(@CountryCode IS NULL OR v.CountryCode = @CountryCode)" };
        TravelSql.ApplyBranchScope(scope, "v.BranchId", clauses, p);
        return (await connection.QueryAsync<TravelVisaProcessingRow>(new CommandDefinition($"""
            SELECT v.CountryCode, co.Name AS CountryName, vt.Name AS VisaTypeName, COUNT(1) AS Submitted,
                   SUM(CASE WHEN v.Status IN (N'Approved', N'PartiallyApproved') THEN 1 ELSE 0 END) AS Approved,
                   SUM(CASE WHEN v.Status = N'Rejected' THEN 1 ELSE 0 END) AS Rejected,
                   SUM(CASE WHEN v.Status IN (N'Submitted', N'InProcessing', N'OnHold') THEN 1 ELSE 0 END) AS Pending,
                   AVG(CASE WHEN v.DecidedAt IS NOT NULL THEN CAST(DATEDIFF(HOUR, v.SubmittedAt, v.DecidedAt) AS DECIMAL(18,2)) / 24 END) AS AvgProcessingDays
            FROM TravelVisaCases v
            INNER JOIN TravelCountries co ON co.Code = v.CountryCode
            INNER JOIN TravelVisaTypes vt ON vt.Id = v.VisaTypeId AND vt.TenantId = v.TenantId
            WHERE v.TenantId = @TenantId AND {string.Join(" AND ", clauses)}
            GROUP BY v.CountryCode, co.Name, vt.Name
            ORDER BY co.Name, vt.Name
            """, p, cancellationToken: ct))).ToList();
    }

    public async Task<IReadOnlyList<TravelCountryApplicationsRow>> GetCountryApplicationsAsync(int tenantId, TravelBranchScope scope, TravelReportFilter f, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        var p = Params(tenantId, f);
        var clauses = new List<string> { "v.TenantId = @TenantId", "v.IsDeleted = 0", "v.CreatedAt >= @From", "v.CreatedAt < @ToExclusive",
            "(@BranchId IS NULL OR v.BranchId = @BranchId)", "(@AgentUserId IS NULL OR v.AssignedAgentUserId = @AgentUserId)", "(@CountryCode IS NULL OR v.CountryCode = @CountryCode)" };
        TravelSql.ApplyBranchScope(scope, "v.BranchId", clauses, p);
        return (await connection.QueryAsync<TravelCountryApplicationsRow>(new CommandDefinition($"""
            SELECT v.CountryCode, co.Name AS CountryName, COUNT(DISTINCT v.Id) AS Cases, COUNT(a.Id) AS Applicants,
                   SUM(CASE WHEN a.Decision = N'Approved' THEN 1 ELSE 0 END) AS Approved, SUM(CASE WHEN a.Decision = N'Rejected' THEN 1 ELSE 0 END) AS Rejected,
                   SUM(CASE WHEN a.Decision = N'Pending' THEN 1 ELSE 0 END) AS Pending
            FROM TravelVisaCases v
            INNER JOIN TravelCountries co ON co.Code = v.CountryCode
            LEFT JOIN TravelVisaApplicants a ON a.VisaCaseId = v.Id AND a.TenantId = v.TenantId AND a.IsDeleted = 0
            WHERE v.TenantId = @TenantId AND {string.Join(" AND ", clauses)}
            GROUP BY v.CountryCode, co.Name
            ORDER BY Cases DESC, co.Name
            """, p, cancellationToken: ct))).ToList();
    }

    public async Task<IReadOnlyList<TravelPendingDocumentsRow>> GetPendingDocumentsAsync(int tenantId, TravelBranchScope scope, TravelReportFilter f, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        var p = Params(tenantId, f);
        var clauses = new List<string> { "v.TenantId = @TenantId", "v.IsDeleted = 0", "v.Status IN (N'DocumentsPending', N'DocumentsVerified')",
            "(v.IntendedTravelDate IS NULL OR (v.IntendedTravelDate >= @From AND v.IntendedTravelDate <= @To))",
            "(@BranchId IS NULL OR v.BranchId = @BranchId)", "(@AgentUserId IS NULL OR v.AssignedAgentUserId = @AgentUserId)", "(@CountryCode IS NULL OR v.CountryCode = @CountryCode)" };
        TravelSql.ApplyBranchScope(scope, "v.BranchId", clauses, p);
        return (await connection.QueryAsync<TravelPendingDocumentsRow>(new CommandDefinition($"""
            SELECT v.Id AS VisaCaseId, v.CaseNumber, COALESCE(c.FullName, g.Name) AS CustomerName, co.Name AS CountryName, vt.Name AS VisaTypeName, v.Status,
                   v.IntendedTravelDate, m.Outstanding, u.FullName AS AgentName
            FROM TravelVisaCases v
            INNER JOIN (SELECT r.VisaCaseId, COUNT(1) AS Outstanding FROM TravelApplicantDocuments r
                        INNER JOIN TravelVisaApplicants a ON a.Id = r.ApplicantId AND a.TenantId = r.TenantId AND a.IsDeleted = 0 AND a.Decision <> N'Withdrawn'
                        WHERE r.TenantId = @TenantId AND r.IsDeleted = 0 AND r.IsMandatory = 1 AND r.Status IN (N'Missing', N'Uploaded', N'Rejected')
                        GROUP BY r.VisaCaseId) m ON m.VisaCaseId = v.Id
            INNER JOIN TravelCountries co ON co.Code = v.CountryCode
            INNER JOIN TravelVisaTypes vt ON vt.Id = v.VisaTypeId AND vt.TenantId = v.TenantId
            LEFT JOIN Customers c ON c.Id = v.CustomerId AND c.TenantId = v.TenantId
            LEFT JOIN TravelGroups g ON g.Id = v.GroupId AND g.TenantId = v.TenantId
            LEFT JOIN Users u ON u.Id = v.AssignedAgentUserId AND u.TenantId = v.TenantId
            WHERE v.TenantId = @TenantId AND {string.Join(" AND ", clauses)}
            ORDER BY CASE WHEN v.IntendedTravelDate IS NULL THEN 1 ELSE 0 END, v.IntendedTravelDate, v.CaseNumber
            """, p, cancellationToken: ct))).ToList();
    }

    public async Task<IReadOnlyList<TravelGroupManifestRow>> GetGroupManifestsAsync(int tenantId, TravelBranchScope scope, TravelReportFilter f, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        var p = Params(tenantId, f);
        var clauses = new List<string> { "g.TenantId = @TenantId", "g.IsDeleted = 0", "g.DepartureDate >= @From", "g.DepartureDate <= @To", "(@BranchId IS NULL OR g.BranchId = @BranchId)" };
        TravelSql.ApplyBranchScope(scope, "g.BranchId", clauses, p);
        return (await connection.QueryAsync<TravelGroupManifestRow>(new CommandDefinition($"""
            SELECT g.Id AS GroupId, g.GroupNumber, g.Name, g.DepartureDate, g.ReturnDate, g.Capacity,
                   (SELECT COUNT(1) FROM TravelGroupMembers m WHERE m.GroupId = g.Id AND m.TenantId = g.TenantId AND m.IsDeleted = 0 AND m.Status <> N'Cancelled') AS Registered,
                   (SELECT COUNT(1) FROM TravelGroupMembers m WHERE m.GroupId = g.Id AND m.TenantId = g.TenantId AND m.IsDeleted = 0 AND m.Status = N'Confirmed') AS Confirmed,
                   (SELECT COUNT(1) FROM TravelVisaApplicants a WHERE a.VisaCaseId = g.VisaCaseId AND a.TenantId = g.TenantId AND a.IsDeleted = 0 AND a.Decision = N'Approved') AS VisasApproved,
                   (SELECT COUNT(DISTINCT fp.GroupMemberId) FROM TravelFlightPassengers fp
                      INNER JOIN TravelGroupMembers m ON m.Id = fp.GroupMemberId AND m.TenantId = fp.TenantId
                      WHERE fp.TenantId = g.TenantId AND m.GroupId = g.Id AND fp.TicketStatus = N'Issued') AS Ticketed,
                   g.Status
            FROM TravelGroups g
            WHERE g.TenantId = @TenantId AND {string.Join(" AND ", clauses)}
            ORDER BY g.DepartureDate, g.GroupNumber
            """, p, cancellationToken: ct))).ToList();
    }

    public async Task<IReadOnlyList<TravelBookingsReportRow>> GetBookingsAsync(int tenantId, TravelBranchScope scope, TravelReportFilter f, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        var p = Params(tenantId, f);
        var clauses = new List<string> { "b.TenantId = @TenantId", "b.IsDeleted = 0", "(@BranchId IS NULL OR b.BranchId = @BranchId)", "(@AgentUserId IS NULL OR b.AssignedAgentUserId = @AgentUserId)" };
        TravelSql.ApplyBranchScope(scope, "b.BranchId", clauses, p);
        var where = string.Join(" AND ", clauses);
        return (await connection.QueryAsync<TravelBookingsReportRow>(new CommandDefinition($"""
            SELECT x.ServiceType, x.SupplierName, x.Status, x.CurrencyCode, COUNT(1) AS Services, COALESCE(SUM(x.Sell), 0) AS Sell, COALESCE(SUM(x.Cost), 0) AS Cost
            FROM (
                SELECT N'Flight' AS ServiceType, COALESCE(s.Name, N'(no supplier)') AS SupplierName, fl.Status, COALESCE(fl.CurrencyCode, N'—') AS CurrencyCode,
                       fl.SellAmount AS Sell, fl.CostAmount AS Cost
                FROM TravelFlightBookings fl
                INNER JOIN TravelBookings b ON b.Id = fl.BookingId AND b.TenantId = fl.TenantId
                LEFT JOIN TravelSuppliers s ON s.Id = fl.SupplierId AND s.TenantId = fl.TenantId
                WHERE fl.TenantId = @TenantId AND fl.IsDeleted = 0 AND {where}
                  AND EXISTS (SELECT 1 FROM TravelFlightSegments sg WHERE sg.FlightBookingId = fl.Id AND sg.TenantId = fl.TenantId AND sg.Sequence = 1
                              AND sg.DepartUtc >= @From AND sg.DepartUtc < @ToExclusive)
                UNION ALL
                SELECT N'Hotel', COALESCE(s.Name, N'(no supplier)'), h.Status, COALESCE(h.CurrencyCode, N'—'), h.SellAmount, h.CostAmount
                FROM TravelHotelBookings h
                INNER JOIN TravelBookings b ON b.Id = h.BookingId AND b.TenantId = h.TenantId
                LEFT JOIN TravelSuppliers s ON s.Id = h.SupplierId AND s.TenantId = h.TenantId
                WHERE h.TenantId = @TenantId AND h.IsDeleted = 0 AND {where} AND h.CheckIn >= @From AND h.CheckIn <= @To
            ) x
            GROUP BY x.ServiceType, x.SupplierName, x.Status, x.CurrencyCode
            ORDER BY x.ServiceType, x.SupplierName, x.Status, x.CurrencyCode
            """, p, cancellationToken: ct))).ToList();
    }

    public async Task<IReadOnlyList<TravelPerformanceRow>> GetPerformanceAsync(int tenantId, TravelBranchScope scope, TravelReportFilter f, bool byBranch, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        var p = Params(tenantId, f);
        // Grouping column per source table: agent user id or branch id.
        string Key(string alias, string agentCol) => byBranch ? $"{alias}.BranchId" : $"{alias}.{agentCol}";
        var scopeE = new List<string> { "1 = 1" }; TravelSql.ApplyBranchScope(scope, "e.BranchId", scopeE, p);
        var scopeV = new List<string> { "1 = 1" }; TravelSql.ApplyBranchScope(scope, "v.BranchId", scopeV, p);
        var scopeB = new List<string> { "1 = 1" }; TravelSql.ApplyBranchScope(scope, "b.BranchId", scopeB, p);
        var scopeI = new List<string> { "1 = 1" }; TravelSql.ApplyBranchScope(scope, "i.BranchId", scopeI, p);
        var labelJoin = byBranch ? "LEFT JOIN Branches lb ON lb.Id = k.KeyId AND lb.TenantId = @TenantId" : "LEFT JOIN Users lu ON lu.Id = k.KeyId AND lu.TenantId = @TenantId";
        var label = byBranch ? "COALESCE(lb.Name, N'No branch')" : "COALESCE(lu.FullName, N'Unassigned')";
        return (await connection.QueryAsync<TravelPerformanceRow>(new CommandDefinition($"""
            WITH enq AS (
                SELECT {Key("e", "AssignedAgentUserId")} AS KeyId, COUNT(1) AS Enquiries, SUM(CASE WHEN e.Status = N'Won' THEN 1 ELSE 0 END) AS Won
                FROM TravelEnquiries e WHERE e.TenantId = @TenantId AND e.IsDeleted = 0 AND e.CreatedAt >= @From AND e.CreatedAt < @ToExclusive
                  AND (@BranchId IS NULL OR e.BranchId = @BranchId) AND (@AgentUserId IS NULL OR e.AssignedAgentUserId = @AgentUserId) AND {string.Join(" AND ", scopeE)}
                GROUP BY {Key("e", "AssignedAgentUserId")}),
            vc AS (
                SELECT {Key("v", "AssignedAgentUserId")} AS KeyId, COUNT(1) AS VisaCases
                FROM TravelVisaCases v WHERE v.TenantId = @TenantId AND v.IsDeleted = 0 AND v.CreatedAt >= @From AND v.CreatedAt < @ToExclusive
                  AND (@BranchId IS NULL OR v.BranchId = @BranchId) AND (@AgentUserId IS NULL OR v.AssignedAgentUserId = @AgentUserId) AND {string.Join(" AND ", scopeV)}
                GROUP BY {Key("v", "AssignedAgentUserId")}),
            bk AS (
                SELECT {Key("b", "AssignedAgentUserId")} AS KeyId, COUNT(1) AS BookingFiles
                FROM TravelBookings b WHERE b.TenantId = @TenantId AND b.IsDeleted = 0 AND b.CreatedAt >= @From AND b.CreatedAt < @ToExclusive
                  AND (@BranchId IS NULL OR b.BranchId = @BranchId) AND (@AgentUserId IS NULL OR b.AssignedAgentUserId = @AgentUserId) AND {string.Join(" AND ", scopeB)}
                GROUP BY {Key("b", "AssignedAgentUserId")}),
            inv AS (
                SELECT {Key("i", "AgentUserId")} AS KeyId, i.CurrencyCode, SUM(i.Total) AS Invoiced, SUM(m.Paid - m.Refunded) AS Collected
                FROM TravelInvoices i
                {TravelInvoiceRepository.Money}
                WHERE i.TenantId = @TenantId AND i.IsDeleted = 0 AND i.Status = N'Issued' AND i.IssueDate >= @From AND i.IssueDate <= @To
                  AND (@BranchId IS NULL OR i.BranchId = @BranchId) AND (@AgentUserId IS NULL OR i.AgentUserId = @AgentUserId) AND {string.Join(" AND ", scopeI)}
                GROUP BY {Key("i", "AgentUserId")}, i.CurrencyCode),
            keys AS (
                SELECT KeyId, CurrencyCode FROM inv
                UNION SELECT KeyId, NULL FROM enq WHERE NOT EXISTS (SELECT 1 FROM inv WHERE inv.KeyId = enq.KeyId OR (inv.KeyId IS NULL AND enq.KeyId IS NULL))
                UNION SELECT KeyId, NULL FROM vc WHERE NOT EXISTS (SELECT 1 FROM inv WHERE inv.KeyId = vc.KeyId OR (inv.KeyId IS NULL AND vc.KeyId IS NULL))
                UNION SELECT KeyId, NULL FROM bk WHERE NOT EXISTS (SELECT 1 FROM inv WHERE inv.KeyId = bk.KeyId OR (inv.KeyId IS NULL AND bk.KeyId IS NULL)))
            SELECT COALESCE(CAST(k.KeyId AS NVARCHAR(20)), N'none') AS [Key], {label} AS Label,
                   COALESCE(enq.Enquiries, 0) AS Enquiries, COALESCE(enq.Won, 0) AS Won, COALESCE(vc.VisaCases, 0) AS VisaCases, COALESCE(bk.BookingFiles, 0) AS BookingFiles,
                   k.CurrencyCode, COALESCE(inv.Invoiced, 0) AS Invoiced, COALESCE(inv.Collected, 0) AS Collected
            FROM keys k
            {labelJoin}
            LEFT JOIN enq ON enq.KeyId = k.KeyId OR (enq.KeyId IS NULL AND k.KeyId IS NULL)
            LEFT JOIN vc ON vc.KeyId = k.KeyId OR (vc.KeyId IS NULL AND k.KeyId IS NULL)
            LEFT JOIN bk ON bk.KeyId = k.KeyId OR (bk.KeyId IS NULL AND k.KeyId IS NULL)
            LEFT JOIN inv ON (inv.KeyId = k.KeyId OR (inv.KeyId IS NULL AND k.KeyId IS NULL)) AND inv.CurrencyCode = k.CurrencyCode
            ORDER BY Label, k.CurrencyCode
            """, p, cancellationToken: ct))).ToList();
    }

    public async Task<IReadOnlyList<TravelPilgrimPaymentsRow>> GetPilgrimPaymentsAsync(int tenantId, TravelBranchScope scope, TravelReportFilter f, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        var p = Params(tenantId, f);
        var clauses = new List<string> { "g.TenantId = @TenantId", "g.IsDeleted = 0", "g.DepartureDate >= @From", "g.DepartureDate <= @To", "(@BranchId IS NULL OR g.BranchId = @BranchId)" };
        TravelSql.ApplyBranchScope(scope, "g.BranchId", clauses, p);
        return (await connection.QueryAsync<TravelPilgrimPaymentsRow>(new CommandDefinition($"""
            SELECT g.Id AS GroupId, g.GroupNumber, g.Name AS GroupName, g.DepartureDate, q.CurrencyCode, q.Pilgrims, q.Quoted,
                   COALESCE(i.Invoiced, 0) AS Invoiced, COALESCE(i.Paid, 0) AS Paid, COALESCE(i.Balance, 0) AS Balance
            FROM TravelGroups g
            INNER JOIN TravelPackages pk ON pk.Id = g.PackageId AND pk.TenantId = g.TenantId
            INNER JOIN (SELECT m.GroupId, COALESCE(m.CurrencyCode, pk2.CurrencyCode) AS CurrencyCode, COUNT(1) AS Pilgrims, COALESCE(SUM(m.QuotedPrice), 0) AS Quoted
                        FROM TravelGroupMembers m
                        INNER JOIN TravelGroups g2 ON g2.Id = m.GroupId AND g2.TenantId = m.TenantId
                        INNER JOIN TravelPackages pk2 ON pk2.Id = g2.PackageId AND pk2.TenantId = g2.TenantId
                        WHERE m.TenantId = @TenantId AND m.IsDeleted = 0 AND m.Status <> N'Cancelled'
                        GROUP BY m.GroupId, COALESCE(m.CurrencyCode, pk2.CurrencyCode)) q ON q.GroupId = g.Id
            LEFT JOIN (SELECT i.GroupId, i.CurrencyCode, SUM(i.Total) AS Invoiced, SUM(m.Paid - m.Refunded) AS Paid, SUM(d.Balance) AS Balance
                       FROM TravelInvoices i
                       {TravelInvoiceRepository.Money}
                       WHERE i.TenantId = @TenantId AND i.IsDeleted = 0 AND i.Status = N'Issued' AND i.GroupId IS NOT NULL
                       GROUP BY i.GroupId, i.CurrencyCode) i ON i.GroupId = g.Id AND i.CurrencyCode = q.CurrencyCode
            WHERE g.TenantId = @TenantId AND {string.Join(" AND ", clauses)}
            ORDER BY g.DepartureDate, g.GroupNumber, q.CurrencyCode
            """, p, cancellationToken: ct))).ToList();
    }
}
