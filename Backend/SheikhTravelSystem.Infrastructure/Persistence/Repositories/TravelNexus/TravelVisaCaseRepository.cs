using System.Data;
using Dapper;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.VisaCases.DTOs;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories.TravelNexus;

/// <summary>
/// Visa cases, applicants and status history. Root predicate <c>v.TenantId = @TenantId</c>; joins repeat it.
/// Every status-dependent write checks the expected status and writes history in the same transaction.
/// </summary>
public sealed class TravelVisaCaseRepository(IDbConnectionFactory dbFactory) : ITravelVisaCaseRepository
{
    private const string ListSelect = """
        SELECT v.Id, v.CaseNumber, v.CustomerId, c.FullName AS CustomerName,
               v.CountryCode, co.Name AS CountryName, v.VisaTypeId, vt.Name AS VisaTypeName,
               v.Status, v.Priority,
               (SELECT COUNT(1) FROM TravelVisaApplicants a
                 WHERE a.TenantId = v.TenantId AND a.VisaCaseId = v.Id AND a.IsDeleted = 0 AND a.Decision <> N'Withdrawn') AS ApplicantCount,
               (SELECT COUNT(1) FROM TravelVisaApplicants a
                 WHERE a.TenantId = v.TenantId AND a.VisaCaseId = v.Id AND a.IsDeleted = 0 AND a.Decision = N'Approved') AS ApprovedCount,
               v.IntendedTravelDate, v.AppointmentAt, v.SubmittedAt,
               v.AssignedAgentUserId, u.FullName AS AssignedAgentName, v.CreatedAt
        """;

    private const string FromJoins = """
        FROM TravelVisaCases v
        INNER JOIN Customers c ON c.Id = v.CustomerId AND c.TenantId = v.TenantId
        INNER JOIN TravelCountries co ON co.Code = v.CountryCode
        INNER JOIN TravelVisaTypes vt ON vt.Id = v.VisaTypeId AND vt.TenantId = v.TenantId
        LEFT JOIN Users u ON u.Id = v.AssignedAgentUserId AND u.TenantId = v.TenantId
        """;

    public async Task<PagedResult<TravelVisaCaseListItemDto>> GetPagedAsync(
        int tenantId, TravelBranchScope scope, TravelVisaCaseListFilter f, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        var clauses = new List<string> { "v.TenantId = @TenantId", "v.IsDeleted = 0" };
        TravelSql.ApplyBranchScope(scope, "v.BranchId", clauses, p);
        TravelSql.ApplyDateRange(f.CreatedFrom, f.CreatedTo, "v.CreatedAt", clauses, p);

        if (f.Search is not null)
        {
            clauses.Add("(v.CaseNumber LIKE @Search OR c.FullName LIKE @Search OR v.SubmissionReference LIKE @Search)");
            p.Add("Search", $"%{TravelSql.EscapeLike(f.Search)}%");
        }
        if (f.Status is not null) { clauses.Add("v.Status = @Status"); p.Add("Status", f.Status); }
        if (f.CountryCode is not null) { clauses.Add("v.CountryCode = @CountryCode"); p.Add("CountryCode", f.CountryCode); }
        if (f.VisaTypeId.HasValue) { clauses.Add("v.VisaTypeId = @VisaTypeId"); p.Add("VisaTypeId", f.VisaTypeId); }
        if (f.AgentUserId.HasValue) { clauses.Add("v.AssignedAgentUserId = @AgentId"); p.Add("AgentId", f.AgentUserId); }
        if (f.CustomerId.HasValue) { clauses.Add("v.CustomerId = @CustomerId"); p.Add("CustomerId", f.CustomerId); }

        var where = string.Join(" AND ", clauses);
        var total = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            $"SELECT COUNT(1) {FromJoins} WHERE {where}", p, cancellationToken: cancellationToken));

        p.Add("Offset", (page - 1) * pageSize);
        p.Add("PageSize", pageSize);
        var items = (await connection.QueryAsync<TravelVisaCaseListItemDto>(new CommandDefinition($"""
            {ListSelect}
            {FromJoins}
            WHERE {where}
            ORDER BY v.CreatedAt DESC, v.Id DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
            """, p, cancellationToken: cancellationToken))).ToList();

        return new PagedResult<TravelVisaCaseListItemDto> { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
    }

    public async Task<TravelVisaCaseDto?> GetByIdAsync(int id, int tenantId, TravelBranchScope scope, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        p.Add("Id", id);
        var clauses = new List<string> { "v.TenantId = @TenantId", "v.Id = @Id", "v.IsDeleted = 0" };
        TravelSql.ApplyBranchScope(scope, "v.BranchId", clauses, p);

        return await connection.QuerySingleOrDefaultAsync<TravelVisaCaseDto>(new CommandDefinition($"""
            {ListSelect},
                   v.EnquiryId, e.EnquiryNumber, v.SubmissionReference, v.ExpectedDecisionDate, v.DecidedAt,
                   v.OnHoldFromStatus, v.BranchId, b.Name AS BranchName, v.QuotedServiceFee, v.QuotedEmbassyFee,
                   v.CurrencyCode, v.CancelReason, v.Notes
            {FromJoins}
            LEFT JOIN TravelEnquiries e ON e.Id = v.EnquiryId AND e.TenantId = v.TenantId
            LEFT JOIN Branches b ON b.Id = v.BranchId AND b.TenantId = v.TenantId
            WHERE {string.Join(" AND ", clauses)}
            """, p, cancellationToken: cancellationToken));
    }

    public async Task<TravelVisaCaseState?> GetStateAsync(int id, int tenantId, TravelBranchScope scope, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        p.Add("Id", id);
        var clauses = new List<string> { "v.TenantId = @TenantId", "v.Id = @Id", "v.IsDeleted = 0" };
        TravelSql.ApplyBranchScope(scope, "v.BranchId", clauses, p);

        return await connection.QuerySingleOrDefaultAsync<TravelVisaCaseState>(new CommandDefinition(
            $"SELECT v.Id, v.CustomerId, v.Status, v.OnHoldFromStatus, v.BranchId FROM TravelVisaCases v WHERE {string.Join(" AND ", clauses)}",
            p, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<TravelVisaApplicantDto>> GetApplicantsAsync(int caseId, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return (await connection.QueryAsync<TravelVisaApplicantDto>(new CommandDefinition("""
            SELECT a.Id, a.TravelerId, CONCAT(t.GivenNames, ' ', t.Surname) AS TravelerName, t.Gender, t.DateOfBirth,
                   t.PassportNumber, t.PassportExpiryDate, a.Decision, a.VisaNumber, a.VisaIssueDate, a.VisaValidFrom,
                   a.VisaExpiryDate, a.RejectionReason, a.DecidedAt, a.Notes
            FROM TravelVisaApplicants a
            INNER JOIN TravelTravelers t ON t.Id = a.TravelerId AND t.TenantId = a.TenantId
            WHERE a.VisaCaseId = @CaseId AND a.TenantId = @TenantId AND a.IsDeleted = 0
            ORDER BY a.Id
            """, new { CaseId = caseId, TenantId = tenantId }, cancellationToken: cancellationToken))).ToList();
    }

    public async Task<IReadOnlyList<TravelVisaHistoryDto>> GetHistoryAsync(int caseId, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return (await connection.QueryAsync<TravelVisaHistoryDto>(new CommandDefinition("""
            SELECT h.Id, h.EventType, h.ApplicantId, CONCAT(t.GivenNames, ' ', t.Surname) AS ApplicantName,
                   h.FromValue, h.ToValue, h.Note, u.FullName AS ChangedByName, h.ChangedAt
            FROM TravelVisaStatusHistory h
            LEFT JOIN TravelVisaApplicants a ON a.Id = h.ApplicantId AND a.TenantId = h.TenantId
            LEFT JOIN TravelTravelers t ON t.Id = a.TravelerId AND t.TenantId = h.TenantId
            LEFT JOIN Users u ON u.Id = h.ChangedBy AND u.TenantId = h.TenantId
            WHERE h.VisaCaseId = @CaseId AND h.TenantId = @TenantId
            ORDER BY h.ChangedAt DESC, h.Id DESC
            """, new { CaseId = caseId, TenantId = tenantId }, cancellationToken: cancellationToken))).ToList();
    }

    public async Task<IReadOnlyDictionary<string, int>> GetStatusCountsAsync(int tenantId, TravelBranchScope scope, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        var scopeClauses = new List<string> { "1 = 1" };
        TravelSql.ApplyBranchScope(scope, "v.BranchId", scopeClauses, p);

        var rows = await connection.QueryAsync<(string Status, int Total)>(new CommandDefinition($"""
            SELECT v.Status, COUNT(1) AS Total FROM TravelVisaCases v
            WHERE v.TenantId = @TenantId AND v.IsDeleted = 0 AND {string.Join(" AND ", scopeClauses)}
            GROUP BY v.Status
            """, p, cancellationToken: cancellationToken));
        return rows.ToDictionary(r => r.Status, r => r.Total);
    }

    public async Task<IReadOnlyList<TravelVisaCaseListItemDto>> GetByCustomerAsync(int customerId, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return (await connection.QueryAsync<TravelVisaCaseListItemDto>(new CommandDefinition($"""
            {ListSelect}
            {FromJoins}
            WHERE v.TenantId = @TenantId AND v.CustomerId = @CustomerId AND v.IsDeleted = 0
            ORDER BY v.CreatedAt DESC
            """, new { TenantId = tenantId, CustomerId = customerId }, cancellationToken: cancellationToken))).ToList();
    }

    public async Task<int> CreateAsync(int tenantId, CreateTravelVisaCaseDto dto, string countryCode, int? branchId, int? userId,
        DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();

        var number = await TravelNumberAllocator.NextAsync(connection, tx, tenantId, TravelNumberAllocator.Kind.VisaCase, nowUtc, cancellationToken);
        var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            INSERT INTO TravelVisaCases (TenantId, CaseNumber, CustomerId, EnquiryId, CountryCode, VisaTypeId, IntendedTravelDate,
                AppointmentAt, ExpectedDecisionDate, Status, Priority, AssignedAgentUserId, BranchId,
                QuotedServiceFee, QuotedEmbassyFee, CurrencyCode, Notes, CreatedBy)
            VALUES (@TenantId, @Number, @CustomerId, @EnquiryId, @CountryCode, @VisaTypeId, @IntendedTravelDate,
                @AppointmentAt, @ExpectedDecisionDate, N'Draft', @Priority, @AssignedAgentUserId, @BranchId,
                @QuotedServiceFee, @QuotedEmbassyFee, @CurrencyCode, @Notes, @UserId);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """,
            new
            {
                TenantId = tenantId, Number = number, dto.CustomerId, dto.EnquiryId, CountryCode = countryCode, dto.VisaTypeId,
                dto.IntendedTravelDate, dto.AppointmentAt, dto.ExpectedDecisionDate, dto.Priority, dto.AssignedAgentUserId,
                BranchId = branchId, dto.QuotedServiceFee, dto.QuotedEmbassyFee, dto.CurrencyCode, dto.Notes, UserId = userId
            }, tx, cancellationToken: cancellationToken));

        await InsertHistoryAsync(connection, tx, tenantId, id, null, "CaseStatus", null, "Draft", "Case opened", userId, cancellationToken);
        tx.Commit();
        return id;
    }

    public async Task<bool> UpdateAsync(int id, int tenantId, UpdateTravelVisaCaseDto dto, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE TravelVisaCases SET
                IntendedTravelDate = @IntendedTravelDate, AppointmentAt = @AppointmentAt,
                ExpectedDecisionDate = @ExpectedDecisionDate, Priority = @Priority,
                AssignedAgentUserId = @AssignedAgentUserId, QuotedServiceFee = @QuotedServiceFee,
                QuotedEmbassyFee = @QuotedEmbassyFee, CurrencyCode = @CurrencyCode, Notes = @Notes,
                UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0 AND Status NOT IN @Terminal
            """,
            new
            {
                Id = id, TenantId = tenantId, dto.IntendedTravelDate, dto.AppointmentAt, dto.ExpectedDecisionDate, dto.Priority,
                dto.AssignedAgentUserId, dto.QuotedServiceFee, dto.QuotedEmbassyFee, dto.CurrencyCode, dto.Notes,
                Terminal = VisaCaseStateMachine.TerminalStatuses
            }, cancellationToken: cancellationToken)) > 0;
    }

    public async Task<bool> ChangeStatusAsync(int tenantId, TravelVisaCaseStatusChange change, int? userId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();

        var rows = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE TravelVisaCases SET
                Status = @NewStatus,
                OnHoldFromStatus = @OnHoldFrom,
                SubmittedAt = COALESCE(@SubmittedAt, SubmittedAt),
                SubmissionReference = COALESCE(@SubmissionReference, SubmissionReference),
                ExpectedDecisionDate = COALESCE(@ExpectedDecisionDate, ExpectedDecisionDate),
                CancelReason = COALESCE(@CancelReason, CancelReason),
                UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0 AND Status = @Expected
            """,
            new
            {
                Id = change.CaseId, TenantId = tenantId, Expected = change.ExpectedStatus, change.NewStatus,
                OnHoldFrom = change.NewOnHoldFromStatus, change.SubmittedAt, change.SubmissionReference,
                change.ExpectedDecisionDate, change.CancelReason
            }, tx, cancellationToken: cancellationToken));
        if (rows == 0)
        {
            tx.Rollback();
            return false;
        }

        await InsertHistoryAsync(connection, tx, tenantId, change.CaseId, null, "CaseStatus",
            change.ExpectedStatus, change.NewStatus, change.Note, userId, cancellationToken);
        tx.Commit();
        return true;
    }

    public async Task<int?> AddApplicantsAsync(int tenantId, int caseId, string expectedStatus, IReadOnlyList<int> travelerIds, int? userId,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();

        var customerId = await LockCaseAsync(connection, tx, tenantId, caseId, expectedStatus, cancellationToken);
        if (customerId is null)
        {
            tx.Rollback();
            return null;
        }

        // Only travelers of the case's own customer, not already active applicants.
        var added = (await connection.QueryAsync<(int ApplicantId, int TravelerId)>(new CommandDefinition("""
            INSERT INTO TravelVisaApplicants (TenantId, VisaCaseId, TravelerId, CreatedBy)
            OUTPUT inserted.Id, inserted.TravelerId
            SELECT @TenantId, @CaseId, t.Id, @UserId
            FROM TravelTravelers t
            WHERE t.TenantId = @TenantId AND t.CustomerId = @CustomerId AND t.IsDeleted = 0 AND t.Id IN @TravelerIds
              AND NOT EXISTS (
                  SELECT 1 FROM TravelVisaApplicants x
                  WHERE x.TenantId = @TenantId AND x.VisaCaseId = @CaseId AND x.TravelerId = t.Id AND x.IsDeleted = 0)
            """,
            new { TenantId = tenantId, CaseId = caseId, CustomerId = customerId, TravelerIds = travelerIds.ToArray(), UserId = userId },
            tx, cancellationToken: cancellationToken))).ToList();

        foreach (var (applicantId, _) in added)
        {
            await InsertHistoryAsync(connection, tx, tenantId, caseId, applicantId, "ApplicantAdded", null, "Pending", null, userId, cancellationToken);
        }

        tx.Commit();
        return added.Count;
    }

    public async Task<bool> RemoveApplicantAsync(int tenantId, int caseId, int applicantId, string expectedStatus, int? userId,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();

        if (await LockCaseAsync(connection, tx, tenantId, caseId, expectedStatus, cancellationToken) is null)
        {
            tx.Rollback();
            return false;
        }

        var rows = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE TravelVisaApplicants SET IsDeleted = 1, UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @ApplicantId AND VisaCaseId = @CaseId AND TenantId = @TenantId AND IsDeleted = 0
            """, new { ApplicantId = applicantId, CaseId = caseId, TenantId = tenantId }, tx, cancellationToken: cancellationToken));
        if (rows == 0)
        {
            tx.Rollback();
            return false;
        }

        await InsertHistoryAsync(connection, tx, tenantId, caseId, applicantId, "ApplicantRemoved", "Pending", null, null, userId, cancellationToken);
        tx.Commit();
        return true;
    }

    public async Task<TravelVisaDecisionResult> RecordDecisionAsync(int tenantId, int caseId, int applicantId, string expectedCaseStatus,
        RecordTravelVisaDecisionDto d, int? userId, DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();

        if (await LockCaseAsync(connection, tx, tenantId, caseId, expectedCaseStatus, cancellationToken) is null)
        {
            tx.Rollback();
            return new TravelVisaDecisionResult(false, null);
        }

        var previous = await connection.ExecuteScalarAsync<string?>(new CommandDefinition("""
            SELECT Decision FROM TravelVisaApplicants
            WHERE Id = @ApplicantId AND VisaCaseId = @CaseId AND TenantId = @TenantId AND IsDeleted = 0
            """, new { ApplicantId = applicantId, CaseId = caseId, TenantId = tenantId }, tx, cancellationToken: cancellationToken));
        if (previous is null)
        {
            tx.Rollback();
            return new TravelVisaDecisionResult(false, null);
        }

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE TravelVisaApplicants SET
                Decision = @Decision, VisaNumber = @VisaNumber, VisaIssueDate = @VisaIssueDate, VisaValidFrom = @VisaValidFrom,
                VisaExpiryDate = @VisaExpiryDate, RejectionReason = @RejectionReason, Notes = COALESCE(@Notes, Notes),
                DecidedAt = @Now, UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @ApplicantId AND VisaCaseId = @CaseId AND TenantId = @TenantId AND IsDeleted = 0
            """,
            new
            {
                ApplicantId = applicantId, CaseId = caseId, TenantId = tenantId, d.Decision, d.VisaNumber, d.VisaIssueDate,
                d.VisaValidFrom, d.VisaExpiryDate, d.RejectionReason, d.Notes, Now = nowUtc
            }, tx, cancellationToken: cancellationToken));

        await InsertHistoryAsync(connection, tx, tenantId, caseId, applicantId, "ApplicantDecision", previous, d.Decision,
            d.Decision == "Rejected" ? d.RejectionReason : d.VisaNumber is null ? null : $"Visa {d.VisaNumber}", userId, cancellationToken);

        // Derive the case outcome once every active applicant is decided.
        var decisions = await connection.QueryAsync<string>(new CommandDefinition("""
            SELECT Decision FROM TravelVisaApplicants WHERE VisaCaseId = @CaseId AND TenantId = @TenantId AND IsDeleted = 0
            """, new { CaseId = caseId, TenantId = tenantId }, tx, cancellationToken: cancellationToken));
        var outcome = VisaCaseStateMachine.DeriveOutcome(decisions);

        if (outcome is not null)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE TravelVisaCases SET Status = @Outcome, DecidedAt = @Now, UpdatedAt = SYSUTCDATETIME()
                WHERE Id = @CaseId AND TenantId = @TenantId AND Status = @Expected
                """, new { Outcome = outcome, Now = nowUtc, CaseId = caseId, TenantId = tenantId, Expected = expectedCaseStatus },
                tx, cancellationToken: cancellationToken));
            await InsertHistoryAsync(connection, tx, tenantId, caseId, null, "CaseStatus", expectedCaseStatus, outcome,
                "All applicants decided", userId, cancellationToken);
        }

        tx.Commit();
        return new TravelVisaDecisionResult(true, outcome);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    /// <summary>Locks the case row in the expected status for the transaction; returns its CustomerId or null.</summary>
    private static Task<int?> LockCaseAsync(IDbConnection connection, IDbTransaction tx, int tenantId, int caseId, string expectedStatus, CancellationToken ct)
        => connection.ExecuteScalarAsync<int?>(new CommandDefinition("""
            SELECT CustomerId FROM TravelVisaCases WITH (UPDLOCK, ROWLOCK)
            WHERE Id = @CaseId AND TenantId = @TenantId AND IsDeleted = 0 AND Status = @Expected
            """, new { CaseId = caseId, TenantId = tenantId, Expected = expectedStatus }, tx, cancellationToken: ct));

    private static Task InsertHistoryAsync(IDbConnection connection, IDbTransaction tx, int tenantId, int caseId, int? applicantId,
        string eventType, string? from, string? to, string? note, int? userId, CancellationToken ct)
        => connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO TravelVisaStatusHistory (TenantId, VisaCaseId, ApplicantId, EventType, FromValue, ToValue, Note, ChangedBy)
            VALUES (@TenantId, @CaseId, @ApplicantId, @EventType, @From, @To, @Note, @UserId)
            """,
            new { TenantId = tenantId, CaseId = caseId, ApplicantId = applicantId, EventType = eventType, From = from, To = to, Note = note, UserId = userId },
            tx, cancellationToken: ct));
}
