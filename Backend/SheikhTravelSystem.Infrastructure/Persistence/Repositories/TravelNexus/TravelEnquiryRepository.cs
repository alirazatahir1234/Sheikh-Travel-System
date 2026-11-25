using Dapper;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Enquiries.DTOs;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories.TravelNexus;

/// <summary>
/// Enquiries + activities. Root predicate <c>e.TenantId = @TenantId</c>; every join repeats the tenant predicate.
/// Status writes compare the expected status and write a StatusChange activity in the same transaction.
/// </summary>
public sealed class TravelEnquiryRepository(IDbConnectionFactory dbFactory) : ITravelEnquiryRepository
{
    private const string ListSelect = """
        SELECT e.Id, e.EnquiryNumber, e.CustomerId,
               COALESCE(c.FullName, e.LeadName) AS DisplayName, COALESCE(c.Phone, e.LeadPhone) AS DisplayPhone,
               e.ServiceType, e.DestinationCountryCode, dc.Name AS DestinationCountryName,
               e.Status, e.Priority, e.AssignedAgentUserId, u.FullName AS AssignedAgentName,
               e.NextFollowUpAt, e.CreatedAt
        """;

    private const string FromJoins = """
        FROM TravelEnquiries e
        LEFT JOIN Customers c ON c.Id = e.CustomerId AND c.TenantId = e.TenantId
        LEFT JOIN TravelCountries dc ON dc.Code = e.DestinationCountryCode
        LEFT JOIN Users u ON u.Id = e.AssignedAgentUserId AND u.TenantId = e.TenantId
        """;

    public async Task<PagedResult<TravelEnquiryListItemDto>> GetPagedAsync(
        int tenantId, TravelBranchScope scope, TravelEnquiryListFilter f, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        var clauses = new List<string> { "e.TenantId = @TenantId", "e.IsDeleted = 0" };
        TravelSql.ApplyBranchScope(scope, "e.BranchId", clauses, p);
        TravelSql.ApplyDateRange(f.CreatedFrom, f.CreatedTo, "e.CreatedAt", clauses, p);

        if (f.Search is not null)
        {
            clauses.Add("(e.EnquiryNumber LIKE @Search OR e.LeadName LIKE @Search OR e.LeadPhone LIKE @Search OR c.FullName LIKE @Search OR c.Phone LIKE @Search)");
            p.Add("Search", $"%{TravelSql.EscapeLike(f.Search)}%");
        }
        if (f.Status is not null) { clauses.Add("e.Status = @Status"); p.Add("Status", f.Status); }
        if (f.ServiceType is not null) { clauses.Add("e.ServiceType = @ServiceType"); p.Add("ServiceType", f.ServiceType); }
        if (f.AgentUserId.HasValue) { clauses.Add("e.AssignedAgentUserId = @AgentId"); p.Add("AgentId", f.AgentUserId); }
        if (f.BranchId.HasValue) { clauses.Add("e.BranchId = @FilterBranchId"); p.Add("FilterBranchId", f.BranchId); }
        if (f.CustomerId.HasValue) { clauses.Add("e.CustomerId = @CustomerId"); p.Add("CustomerId", f.CustomerId); }
        if (f.FollowUpDue)
        {
            clauses.Add("e.NextFollowUpAt <= @Now AND e.Status IN @OpenStatuses");
            p.Add("Now", f.NowUtc);
            p.Add("OpenStatuses", EnquiryStateMachine.OpenStatuses);
        }

        var where = string.Join(" AND ", clauses);
        var total = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            $"SELECT COUNT(1) {FromJoins} WHERE {where}", p, cancellationToken: cancellationToken));

        p.Add("Offset", (page - 1) * pageSize);
        p.Add("PageSize", pageSize);
        var order = f.FollowUpDue ? "e.NextFollowUpAt ASC, e.Id" : "e.CreatedAt DESC, e.Id DESC";
        var items = (await connection.QueryAsync<TravelEnquiryListItemDto>(new CommandDefinition($"""
            {ListSelect}
            {FromJoins}
            WHERE {where}
            ORDER BY {order}
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
            """, p, cancellationToken: cancellationToken))).ToList();

        return new PagedResult<TravelEnquiryListItemDto> { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
    }

    public async Task<TravelEnquiryDto?> GetByIdAsync(int id, int tenantId, TravelBranchScope scope, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        p.Add("Id", id);
        var clauses = new List<string> { "e.TenantId = @TenantId", "e.Id = @Id", "e.IsDeleted = 0" };
        TravelSql.ApplyBranchScope(scope, "e.BranchId", clauses, p);

        return await connection.QuerySingleOrDefaultAsync<TravelEnquiryDto>(new CommandDefinition($"""
            {ListSelect},
                   e.LeadName, e.LeadPhone, e.LeadEmail, e.VisaTypeId, vt.Name AS VisaTypeName,
                   e.TravelDateFrom, e.TravelDateTo, e.Adults, e.Children, e.Infants, e.Budget, e.CurrencyCode,
                   e.LeadSourceId, ls.Name AS LeadSourceName, e.BranchId, b.Name AS BranchName,
                   e.LostReasonId, lr.Name AS LostReasonName, e.LostNotes,
                   e.ConvertedVisaCaseId, vc.CaseNumber AS ConvertedVisaCaseNumber, e.ClosedAt, e.Notes
            {FromJoins}
            LEFT JOIN TravelVisaTypes vt ON vt.Id = e.VisaTypeId AND vt.TenantId = e.TenantId
            LEFT JOIN TravelLeadSources ls ON ls.Id = e.LeadSourceId AND ls.TenantId = e.TenantId
            LEFT JOIN Branches b ON b.Id = e.BranchId AND b.TenantId = e.TenantId
            LEFT JOIN TravelLostReasons lr ON lr.Id = e.LostReasonId AND lr.TenantId = e.TenantId
            LEFT JOIN TravelVisaCases vc ON vc.Id = e.ConvertedVisaCaseId AND vc.TenantId = e.TenantId
            WHERE {string.Join(" AND ", clauses)}
            """, p, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<TravelEnquiryActivityDto>> GetActivitiesAsync(int enquiryId, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return (await connection.QueryAsync<TravelEnquiryActivityDto>(new CommandDefinition("""
            SELECT a.Id, a.ActivityType, a.Direction, a.Summary, a.Outcome, a.OccurredAt, a.NextFollowUpAt,
                   u.FullName AS CreatedByName
            FROM TravelEnquiryActivities a
            LEFT JOIN Users u ON u.Id = a.CreatedBy AND u.TenantId = a.TenantId
            WHERE a.EnquiryId = @EnquiryId AND a.TenantId = @TenantId
            ORDER BY a.OccurredAt DESC, a.Id DESC
            """, new { EnquiryId = enquiryId, TenantId = tenantId }, cancellationToken: cancellationToken))).ToList();
    }

    public async Task<(IReadOnlyDictionary<string, int> Counts, int FollowUpsDue)> GetSummaryAsync(
        int tenantId, TravelBranchScope scope, DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        p.Add("Now", nowUtc);
        p.Add("OpenStatuses", EnquiryStateMachine.OpenStatuses);
        var scopeClauses = new List<string> { "1 = 1" };
        TravelSql.ApplyBranchScope(scope, "e.BranchId", scopeClauses, p);
        var scopeSql = string.Join(" AND ", scopeClauses);

        using var grid = await connection.QueryMultipleAsync(new CommandDefinition($"""
            SELECT e.Status, COUNT(1) AS Total FROM TravelEnquiries e
            WHERE e.TenantId = @TenantId AND e.IsDeleted = 0 AND {scopeSql}
            GROUP BY e.Status;
            SELECT COUNT(1) FROM TravelEnquiries e
            WHERE e.TenantId = @TenantId AND e.IsDeleted = 0 AND {scopeSql}
              AND e.Status IN @OpenStatuses AND e.NextFollowUpAt <= @Now;
            """, p, cancellationToken: cancellationToken));
        var counts = (await grid.ReadAsync<(string Status, int Total)>()).ToDictionary(r => r.Status, r => r.Total);
        var due = await grid.ReadSingleAsync<int>();
        return (counts, due);
    }

    public async Task<IReadOnlyList<TravelEnquiryListItemDto>> GetByCustomerAsync(int customerId, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return (await connection.QueryAsync<TravelEnquiryListItemDto>(new CommandDefinition($"""
            {ListSelect}
            {FromJoins}
            WHERE e.TenantId = @TenantId AND e.CustomerId = @CustomerId AND e.IsDeleted = 0
            ORDER BY e.CreatedAt DESC
            """, new { TenantId = tenantId, CustomerId = customerId }, cancellationToken: cancellationToken))).ToList();
    }

    public async Task<int> CreateAsync(int tenantId, SaveTravelEnquiryDto dto, int? userId, DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();

        var number = await TravelNumberAllocator.NextAsync(connection, tx, tenantId, TravelNumberAllocator.Kind.Enquiry, nowUtc, cancellationToken);
        var p = Params(tenantId, dto);
        p.Add("Number", number);
        p.Add("UserId", userId);
        var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            INSERT INTO TravelEnquiries (TenantId, EnquiryNumber, CustomerId, LeadName, LeadPhone, LeadEmail, ServiceType,
                DestinationCountryCode, VisaTypeId, TravelDateFrom, TravelDateTo, Adults, Children, Infants, Budget, CurrencyCode,
                LeadSourceId, AssignedAgentUserId, BranchId, Status, Priority, NextFollowUpAt, Notes, CreatedBy)
            VALUES (@TenantId, @Number, @CustomerId, @LeadName, @LeadPhone, @LeadEmail, @ServiceType,
                @DestinationCountryCode, @VisaTypeId, @TravelDateFrom, @TravelDateTo, @Adults, @Children, @Infants, @Budget, @CurrencyCode,
                @LeadSourceId, @AssignedAgentUserId, @BranchId, N'New', @Priority, @NextFollowUpAt, @Notes, @UserId);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """, p, tx, cancellationToken: cancellationToken));

        tx.Commit();
        return id;
    }

    public async Task<bool> UpdateAsync(int id, int tenantId, SaveTravelEnquiryDto dto, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var p = Params(tenantId, dto);
        p.Add("Id", id);
        p.Add("OpenStatuses", EnquiryStateMachine.OpenStatuses);
        return await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE TravelEnquiries SET
                CustomerId = @CustomerId, LeadName = @LeadName, LeadPhone = @LeadPhone, LeadEmail = @LeadEmail,
                ServiceType = @ServiceType, DestinationCountryCode = @DestinationCountryCode, VisaTypeId = @VisaTypeId,
                TravelDateFrom = @TravelDateFrom, TravelDateTo = @TravelDateTo, Adults = @Adults, Children = @Children,
                Infants = @Infants, Budget = @Budget, CurrencyCode = @CurrencyCode, LeadSourceId = @LeadSourceId,
                AssignedAgentUserId = @AssignedAgentUserId, BranchId = @BranchId, Priority = @Priority,
                NextFollowUpAt = @NextFollowUpAt, Notes = @Notes, UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0 AND Status IN @OpenStatuses
            """, p, cancellationToken: cancellationToken)) > 0;
    }

    public async Task<bool> ChangeStatusAsync(int tenantId, TravelEnquiryStatusChange change, int? userId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();

        var rows = await ApplyStatusAsync(connection, tx, tenantId, change, cancellationToken);
        if (rows == 0)
        {
            tx.Rollback();
            return false;
        }

        await InsertActivityAsync(connection, tx, tenantId, change.EnquiryId, "StatusChange", "Internal", change.ActivitySummary,
            null, DateTime.UtcNow, null, userId, cancellationToken);
        tx.Commit();
        return true;
    }

    public async Task<bool> LogActivityAsync(
        int tenantId, int enquiryId, LogTravelEnquiryActivityDto activity, DateTime occurredAt, bool promoteNewToContacted,
        int? userId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();

        // Next follow-up comes from the latest activity (null clears it).
        var touched = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE TravelEnquiries SET NextFollowUpAt = @NextFollowUpAt, UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0
            """, new { Id = enquiryId, TenantId = tenantId, activity.NextFollowUpAt }, tx, cancellationToken: cancellationToken));
        if (touched == 0)
        {
            tx.Rollback();
            return false;
        }

        await InsertActivityAsync(connection, tx, tenantId, enquiryId, activity.ActivityType, activity.Direction, activity.Summary,
            activity.Outcome, occurredAt, activity.NextFollowUpAt, userId, cancellationToken);

        if (promoteNewToContacted)
        {
            var promoted = await ApplyStatusAsync(connection, tx, tenantId,
                new TravelEnquiryStatusChange(enquiryId, "New", "Contacted", "Status New → Contacted (first contact logged)"), cancellationToken);
            if (promoted > 0)
            {
                await InsertActivityAsync(connection, tx, tenantId, enquiryId, "StatusChange", "Internal",
                    "Status New → Contacted (first contact logged)", null, DateTime.UtcNow, null, userId, cancellationToken);
            }
        }

        tx.Commit();
        return true;
    }

    public async Task<TravelEnquiryConversionResult?> ConvertAsync(
        int tenantId, TravelEnquiryConversionPlan plan, int? userId, DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();

        // Lock the enquiry row in its expected status first so a concurrent conversion fails cleanly.
        var locked = await connection.ExecuteScalarAsync<int?>(new CommandDefinition("""
            SELECT Id FROM TravelEnquiries WITH (UPDLOCK, ROWLOCK)
            WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0 AND Status = @Expected
            """, new { Id = plan.EnquiryId, TenantId = tenantId, Expected = plan.ExpectedStatus }, tx, cancellationToken: cancellationToken));
        if (locked is null)
        {
            tx.Rollback();
            return null;
        }

        int customerId;
        if (plan.CustomerId is int existing)
        {
            customerId = existing;
        }
        else
        {
            var n = plan.NewCustomer ?? throw new InvalidOperationException("Conversion plan has no customer.");
            customerId = await TravelCustomerSql.InsertCustomerAsync(connection, tx, tenantId, n.FullName, n.Phone, n.Email, null, null, cancellationToken);
            await TravelCustomerSql.UpsertProfileAsync(connection, tx, tenantId, customerId,
                new TravelProfileValues(n.Kind, n.FullName, null, null, null, null, n.AgentUserId, n.BranchId, n.LeadSourceId, null),
                userId, cancellationToken);
        }

        int? caseId = null;
        string? caseNumber = null;
        if (plan.VisaCase is { } vc)
        {
            caseNumber = await TravelNumberAllocator.NextAsync(connection, tx, tenantId, TravelNumberAllocator.Kind.VisaCase, nowUtc, cancellationToken);
            caseId = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
                INSERT INTO TravelVisaCases (TenantId, CaseNumber, CustomerId, EnquiryId, CountryCode, VisaTypeId,
                    IntendedTravelDate, Status, Priority, AssignedAgentUserId, BranchId, CreatedBy)
                VALUES (@TenantId, @Number, @CustomerId, @EnquiryId, @CountryCode, @VisaTypeId,
                    @IntendedTravelDate, N'Draft', N'Normal', @AgentId, @BranchId, @UserId);
                SELECT CAST(SCOPE_IDENTITY() AS INT);
                """,
                new
                {
                    TenantId = tenantId, Number = caseNumber, CustomerId = customerId, EnquiryId = plan.EnquiryId,
                    vc.CountryCode, vc.VisaTypeId, vc.IntendedTravelDate, AgentId = vc.AgentUserId, vc.BranchId, UserId = userId
                }, tx, cancellationToken: cancellationToken));

            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO TravelVisaStatusHistory (TenantId, VisaCaseId, EventType, FromValue, ToValue, Note, ChangedBy)
                VALUES (@TenantId, @CaseId, N'CaseStatus', NULL, N'Draft', @Note, @UserId)
                """, new { TenantId = tenantId, CaseId = caseId, Note = "Opened from enquiry conversion", UserId = userId },
                tx, cancellationToken: cancellationToken));
        }

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE TravelEnquiries SET
                Status = N'Won', CustomerId = @CustomerId, ConvertedVisaCaseId = @CaseId,
                ClosedAt = SYSUTCDATETIME(), NextFollowUpAt = NULL, UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id AND TenantId = @TenantId
            """, new { Id = plan.EnquiryId, TenantId = tenantId, CustomerId = customerId, CaseId = caseId },
            tx, cancellationToken: cancellationToken));

        var summary = $"Status {plan.ExpectedStatus} → Won" + (caseNumber is null ? "" : $"; visa case {caseNumber} opened");
        await InsertActivityAsync(connection, tx, tenantId, plan.EnquiryId, "StatusChange", "Internal", summary,
            null, DateTime.UtcNow, null, userId, cancellationToken);

        tx.Commit();
        return new TravelEnquiryConversionResult(customerId, caseId, caseNumber);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static Task<int> ApplyStatusAsync(
        System.Data.IDbConnection connection, System.Data.IDbTransaction tx, int tenantId, TravelEnquiryStatusChange c, CancellationToken ct)
    {
        var closing = c.NewStatus is "Lost" or "Won";
        return connection.ExecuteAsync(new CommandDefinition("""
            UPDATE TravelEnquiries SET
                Status = @NewStatus,
                LostReasonId = @LostReasonId,
                LostNotes = @LostNotes,
                ClosedAt = CASE WHEN @Closing = 1 THEN SYSUTCDATETIME() ELSE NULL END,
                NextFollowUpAt = CASE WHEN @Closing = 1 THEN NULL ELSE NextFollowUpAt END,
                UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0 AND Status = @Expected
            """,
            new
            {
                Id = c.EnquiryId, TenantId = tenantId, Expected = c.ExpectedStatus, c.NewStatus,
                LostReasonId = c.NewStatus == "Lost" ? c.LostReasonId : null,
                LostNotes = c.NewStatus == "Lost" ? c.LostNotes : null,
                Closing = closing ? 1 : 0
            }, tx, cancellationToken: ct));
    }

    private static Task InsertActivityAsync(
        System.Data.IDbConnection connection, System.Data.IDbTransaction tx, int tenantId, int enquiryId,
        string type, string direction, string summary, string? outcome, DateTime occurredAt, DateTime? nextFollowUpAt,
        int? userId, CancellationToken ct)
        => connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO TravelEnquiryActivities (TenantId, EnquiryId, ActivityType, Direction, Summary, Outcome,
                OccurredAt, NextFollowUpAt, CreatedBy)
            VALUES (@TenantId, @EnquiryId, @Type, @Direction, @Summary, @Outcome, @OccurredAt, @NextFollowUpAt, @UserId)
            """,
            new
            {
                TenantId = tenantId, EnquiryId = enquiryId, Type = type, Direction = direction,
                Summary = summary.Length > 1000 ? summary[..1000] : summary,
                Outcome = outcome, OccurredAt = occurredAt, NextFollowUpAt = nextFollowUpAt, UserId = userId
            }, tx, cancellationToken: ct));

    private static DynamicParameters Params(int tenantId, SaveTravelEnquiryDto d)
    {
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        p.Add("CustomerId", d.CustomerId);
        p.Add("LeadName", d.LeadName);
        p.Add("LeadPhone", d.LeadPhone);
        p.Add("LeadEmail", d.LeadEmail);
        p.Add("ServiceType", d.ServiceType);
        p.Add("DestinationCountryCode", d.DestinationCountryCode);
        p.Add("VisaTypeId", d.VisaTypeId);
        p.Add("TravelDateFrom", d.TravelDateFrom?.Date);
        p.Add("TravelDateTo", d.TravelDateTo?.Date);
        p.Add("Adults", d.Adults);
        p.Add("Children", d.Children);
        p.Add("Infants", d.Infants);
        p.Add("Budget", d.Budget);
        p.Add("CurrencyCode", d.CurrencyCode);
        p.Add("LeadSourceId", d.LeadSourceId);
        p.Add("AssignedAgentUserId", d.AssignedAgentUserId);
        p.Add("BranchId", d.BranchId);
        p.Add("Priority", d.Priority);
        p.Add("NextFollowUpAt", d.NextFollowUpAt);
        p.Add("Notes", d.Notes);
        return p;
    }
}
