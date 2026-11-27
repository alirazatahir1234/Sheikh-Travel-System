using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Commissions.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.Finance.DTOs;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories.TravelNexus;

/// <summary>Sub-agents, agent commission rules, payouts and invoice attribution. Root predicate on the row's TenantId; joins repeat it.</summary>
public sealed class TravelCommissionRepository(IDbConnectionFactory dbFactory) : ITravelCommissionRepository
{
    private const string RuleSelect = """
        SELECT r.Id, r.BeneficiaryType, r.UserId, r.SubAgentId, COALESCE(u.FullName, sa.Name) AS BeneficiaryName, r.Scope, r.Basis, r.Rate,
               r.ValidFrom, r.ValidTo, r.IsActive
        FROM TravelCommissionRules r
        LEFT JOIN Users u ON u.Id = r.UserId AND u.TenantId = r.TenantId
        LEFT JOIN TravelSubAgents sa ON sa.Id = r.SubAgentId AND sa.TenantId = r.TenantId
        """;

    private const string PayoutSelect = """
        SELECT po.Id, po.PayoutNumber, po.BeneficiaryType, COALESCE(po.UserId, po.SubAgentId) AS BeneficiaryId,
               COALESCE(u.FullName, sa.Name, N'') AS BeneficiaryName, po.PaidOn, po.Method, po.Reference, po.Amount, po.CurrencyCode, po.Notes
        FROM TravelCommissionPayouts po
        LEFT JOIN Users u ON u.Id = po.UserId AND u.TenantId = po.TenantId
        LEFT JOIN TravelSubAgents sa ON sa.Id = po.SubAgentId AND sa.TenantId = po.TenantId
        """;

    // ── Sub-agents ──────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<TravelSubAgentDto>> GetSubAgentsAsync(int tenantId, bool? isActive, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return (await connection.QueryAsync<TravelSubAgentDto>(new CommandDefinition("""
            SELECT sa.Id, sa.Name, sa.Phone, sa.Email, sa.Notes, sa.IsActive,
                   (SELECT COUNT(1) FROM TravelCustomerProfiles cp WHERE cp.SubAgentId = sa.Id AND cp.TenantId = sa.TenantId AND cp.IsDeleted = 0) AS ReferredCustomers
            FROM TravelSubAgents sa
            WHERE sa.TenantId = @TenantId AND sa.IsDeleted = 0 AND (@IsActive IS NULL OR sa.IsActive = @IsActive)
            ORDER BY sa.Name
            """, new { TenantId = tenantId, IsActive = isActive }, cancellationToken: cancellationToken))).ToList();
    }

    public async Task<bool> SubAgentNameExistsAsync(int tenantId, string name, int? excludeId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT COUNT(1) FROM TravelSubAgents WHERE TenantId = @TenantId AND Name = @Name AND IsDeleted = 0 AND (@ExcludeId IS NULL OR Id <> @ExcludeId)
            """, new { TenantId = tenantId, Name = name, ExcludeId = excludeId }, cancellationToken: cancellationToken)) > 0;
    }

    public async Task<int> SaveSubAgentAsync(int tenantId, int? id, SaveTravelSubAgentDto dto, int? userId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var args = new { TenantId = tenantId, Id = id, dto.Name, dto.Phone, dto.Email, dto.Notes, dto.IsActive, UserId = userId };
        try
        {
            if (id is null)
                return await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
                    INSERT INTO TravelSubAgents (TenantId, Name, Phone, Email, Notes, IsActive, CreatedBy)
                    VALUES (@TenantId, @Name, @Phone, @Email, @Notes, @IsActive, @UserId);
                    SELECT CAST(SCOPE_IDENTITY() AS INT);
                    """, args, cancellationToken: cancellationToken));
            var rows = await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE TravelSubAgents SET Name = @Name, Phone = @Phone, Email = @Email, Notes = @Notes, IsActive = @IsActive,
                    UpdatedAt = SYSUTCDATETIME()
                WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0
                """, args, cancellationToken: cancellationToken));
            return rows == 0 ? 0 : id.Value;
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            // UX_TravelSubAgents_Tenant_Name: the handler checked, a concurrent save won.
            throw new InvalidOperationException("A sub-agent with this name already exists.", ex);
        }
    }

    public async Task<bool> DeleteSubAgentAsync(int tenantId, int id, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE TravelSubAgents SET IsDeleted = 1, IsActive = 0, UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0
            """, new { Id = id, TenantId = tenantId }, cancellationToken: cancellationToken)) > 0;
    }

    public async Task<bool> SubAgentExistsAsync(int tenantId, int id, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM TravelSubAgents WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
            new { Id = id, TenantId = tenantId }, cancellationToken: cancellationToken)) > 0;
    }

    public async Task<bool> UserExistsAsync(int tenantId, int userId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM Users WHERE Id = @Id AND TenantId = @TenantId",
            new { Id = userId, TenantId = tenantId }, cancellationToken: cancellationToken)) > 0;
    }

    // ── Rules ───────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<TravelCommissionRuleDto>> GetRulesAsync(int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return (await connection.QueryAsync<TravelCommissionRuleDto>(new CommandDefinition($"""
            {RuleSelect}
            WHERE r.TenantId = @TenantId AND r.IsDeleted = 0
            ORDER BY r.BeneficiaryType, CASE WHEN r.UserId IS NULL AND r.SubAgentId IS NULL THEN 0 ELSE 1 END, r.ValidFrom DESC, r.Id DESC
            """, new { TenantId = tenantId }, cancellationToken: cancellationToken))).ToList();
    }

    public async Task<int> SaveRuleAsync(int tenantId, int? id, SaveTravelCommissionRuleDto dto, int? userId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var args = new
        {
            TenantId = tenantId, Id = id, dto.BeneficiaryType, dto.UserId, dto.SubAgentId, dto.Scope, dto.Basis, dto.Rate,
            ValidFrom = dto.ValidFrom.Date, ValidTo = dto.ValidTo?.Date, dto.IsActive, Actor = userId
        };
        if (id is null)
            return await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
                INSERT INTO TravelCommissionRules (TenantId, BeneficiaryType, UserId, SubAgentId, Scope, Basis, Rate, ValidFrom, ValidTo, IsActive, CreatedBy)
                VALUES (@TenantId, @BeneficiaryType, @UserId, @SubAgentId, @Scope, @Basis, @Rate, @ValidFrom, @ValidTo, @IsActive, @Actor);
                SELECT CAST(SCOPE_IDENTITY() AS INT);
                """, args, cancellationToken: cancellationToken));
        var rows = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE TravelCommissionRules SET BeneficiaryType = @BeneficiaryType, UserId = @UserId, SubAgentId = @SubAgentId, Scope = @Scope, Basis = @Basis,
                Rate = @Rate, ValidFrom = @ValidFrom, ValidTo = @ValidTo, IsActive = @IsActive, UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0
            """, args, cancellationToken: cancellationToken));
        return rows == 0 ? 0 : id.Value;
    }

    public async Task<bool> DeleteRuleAsync(int tenantId, int id, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE TravelCommissionRules SET IsDeleted = 1, UpdatedAt = SYSUTCDATETIME() WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0
            """, new { Id = id, TenantId = tenantId }, cancellationToken: cancellationToken)) > 0;
    }

    // ── Attributed invoices, payouts ────────────────────────────────────────

    public async Task<IReadOnlyList<TravelAttributedInvoiceFact>> GetAttributedInvoicesAsync(int tenantId, string? beneficiaryType, int? beneficiaryId,
        DateTime? issuedFrom, DateTime? issuedTo, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        p.Add("Today", DateTime.UtcNow.Date);
        var clauses = new List<string> { "i.TenantId = @TenantId", "i.IsDeleted = 0", "i.Status = N'Issued'" };
        switch (beneficiaryType)
        {
            case "Staff":
                clauses.Add(beneficiaryId is null ? "i.AgentUserId IS NOT NULL" : "i.AgentUserId = @BeneficiaryId");
                break;
            case "SubAgent":
                clauses.Add(beneficiaryId is null ? "i.SubAgentId IS NOT NULL" : "i.SubAgentId = @BeneficiaryId");
                break;
            default:
                clauses.Add("(i.AgentUserId IS NOT NULL OR i.SubAgentId IS NOT NULL)");
                break;
        }
        p.Add("BeneficiaryId", beneficiaryId);
        if (issuedFrom.HasValue) { clauses.Add("i.IssueDate >= @From"); p.Add("From", issuedFrom.Value.Date); }
        if (issuedTo.HasValue) { clauses.Add("i.IssueDate <= @To"); p.Add("To", issuedTo.Value.Date); }
        return (await connection.QueryAsync<TravelAttributedInvoiceFact>(new CommandDefinition($"""
            SELECT i.Id AS InvoiceId, i.InvoiceNumber, i.IssueDate, i.CustomerId, c.FullName AS CustomerName, i.CurrencyCode, d.Balance,
                   i.AgentUserId, u.FullName AS AgentName, i.SubAgentId, sa.Name AS SubAgentName
            FROM TravelInvoices i
            INNER JOIN Customers c ON c.Id = i.CustomerId AND c.TenantId = i.TenantId
            LEFT JOIN Users u ON u.Id = i.AgentUserId AND u.TenantId = i.TenantId
            LEFT JOIN TravelSubAgents sa ON sa.Id = i.SubAgentId AND sa.TenantId = i.TenantId
            {TravelInvoiceRepository.Money}
            WHERE i.TenantId = @TenantId AND {string.Join(" AND ", clauses)}
            ORDER BY i.IssueDate DESC, i.Id DESC
            """, p, cancellationToken: cancellationToken))).ToList();
    }

    public async Task<IReadOnlyList<TravelAttributedInvoiceFact>> GetAttributedInvoicesByIdsAsync(int tenantId, IReadOnlyCollection<int> invoiceIds,
        CancellationToken cancellationToken = default)
    {
        if (invoiceIds.Count == 0) return [];
        using var connection = dbFactory.CreateConnection();
        var result = new List<TravelAttributedInvoiceFact>();
        foreach (var chunk in invoiceIds.Distinct().Chunk(1000))
        {
            result.AddRange(await connection.QueryAsync<TravelAttributedInvoiceFact>(new CommandDefinition($"""
                SELECT i.Id AS InvoiceId, i.InvoiceNumber, i.IssueDate, i.CustomerId, c.FullName AS CustomerName, i.CurrencyCode, d.Balance,
                       i.AgentUserId, u.FullName AS AgentName, i.SubAgentId, sa.Name AS SubAgentName
                FROM TravelInvoices i
                INNER JOIN Customers c ON c.Id = i.CustomerId AND c.TenantId = i.TenantId
                LEFT JOIN Users u ON u.Id = i.AgentUserId AND u.TenantId = i.TenantId
                LEFT JOIN TravelSubAgents sa ON sa.Id = i.SubAgentId AND sa.TenantId = i.TenantId
                {TravelInvoiceRepository.Money}
                WHERE i.TenantId = @TenantId AND i.IsDeleted = 0 AND i.Status = N'Issued'
                  AND (i.AgentUserId IS NOT NULL OR i.SubAgentId IS NOT NULL) AND i.Id IN @Ids
                ORDER BY i.IssueDate DESC, i.Id DESC
                """, new { TenantId = tenantId, Today = DateTime.UtcNow.Date, Ids = chunk }, cancellationToken: cancellationToken)));
        }
        return result;
    }

    public async Task<IReadOnlyList<TravelCommissionPayoutDto>> GetPayoutsAsync(int tenantId, string? beneficiaryType, int? beneficiaryId,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return (await connection.QueryAsync<TravelCommissionPayoutDto>(new CommandDefinition($"""
            {PayoutSelect}
            WHERE po.TenantId = @TenantId
              AND (@Type IS NULL OR po.BeneficiaryType = @Type)
              AND (@BeneficiaryId IS NULL OR COALESCE(po.UserId, po.SubAgentId) = @BeneficiaryId)
            ORDER BY po.PaidOn DESC, po.Id DESC
            """, new { TenantId = tenantId, Type = beneficiaryType, BeneficiaryId = beneficiaryId }, cancellationToken: cancellationToken))).ToList();
    }

    public async Task<int?> FindPayoutByIdempotencyKeyAsync(int tenantId, Guid key, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            "SELECT Id FROM TravelCommissionPayouts WHERE TenantId = @TenantId AND IdempotencyKey = @Key",
            new { TenantId = tenantId, Key = key }, cancellationToken: cancellationToken));
    }

    public async Task<TravelFinanceWriteResult> RecordPayoutAsync(int tenantId, RecordTravelCommissionPayoutDto dto, decimal payableCap, int? userId, DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();
        try
        {
            var existing = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
                "SELECT Id FROM TravelCommissionPayouts WITH (UPDLOCK, HOLDLOCK) WHERE TenantId = @TenantId AND IdempotencyKey = @Key",
                new { TenantId = tenantId, Key = dto.IdempotencyKey }, tx, cancellationToken: cancellationToken));
            if (existing is int replay)
            {
                tx.Commit();
                return new TravelFinanceWriteResult(replay, null, Replayed: true);
            }

            // One payout at a time per beneficiary: the balance check in the handler runs outside this transaction, so the
            // cap is re-checked here against the payouts already committed, under an application lock scoped to the transaction.
            var locked = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
                DECLARE @Result INT;
                EXEC @Result = sp_getapplock @Resource = @Resource, @LockMode = N'Exclusive', @LockOwner = N'Transaction', @LockTimeout = 5000;
                SELECT @Result;
                """, new { Resource = $"TravelPayout:{tenantId}:{dto.BeneficiaryType}:{dto.BeneficiaryId}" }, tx, cancellationToken: cancellationToken));
            if (locked < 0)
            {
                tx.Rollback();
                return TravelFinanceWriteResult.Fail("This payout is being recorded already. Reload the list.");
            }
            var paid = await connection.ExecuteScalarAsync<decimal>(new CommandDefinition("""
                SELECT ISNULL(SUM(Amount), 0) FROM TravelCommissionPayouts
                WHERE TenantId = @TenantId AND BeneficiaryType = @Type AND COALESCE(UserId, SubAgentId) = @BeneficiaryId AND CurrencyCode = @Currency
                """, new { TenantId = tenantId, Type = dto.BeneficiaryType, dto.BeneficiaryId, Currency = dto.CurrencyCode }, tx, cancellationToken: cancellationToken));
            var remaining = payableCap - paid;
            if (dto.Amount > remaining)
            {
                tx.Rollback();
                return TravelFinanceWriteResult.Fail(remaining > 0
                    ? $"Only {remaining:0.##} {dto.CurrencyCode} is payable to this beneficiary."
                    : $"Nothing is payable to this beneficiary in {dto.CurrencyCode}.");
            }

            var number = await TravelNumberAllocator.NextAsync(connection, tx, tenantId, TravelNumberAllocator.Kind.Payout, utcNow, cancellationToken);
            var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
                INSERT INTO TravelCommissionPayouts (TenantId, PayoutNumber, BeneficiaryType, UserId, SubAgentId, PaidOn, Method, Reference, Amount, CurrencyCode,
                    IdempotencyKey, Notes, CreatedBy)
                VALUES (@TenantId, @Number, @Type, @UserId, @SubAgentId, @PaidOn, @Method, @Reference, @Amount, @Currency, @Key, @Notes, @Actor);
                SELECT CAST(SCOPE_IDENTITY() AS INT);
                """, new
                {
                    TenantId = tenantId, Number = number, Type = dto.BeneficiaryType,
                    UserId = dto.BeneficiaryType == "Staff" ? dto.BeneficiaryId : (int?)null,
                    SubAgentId = dto.BeneficiaryType == "SubAgent" ? dto.BeneficiaryId : (int?)null,
                    PaidOn = dto.PaidOn.Date, dto.Method, dto.Reference, dto.Amount, Currency = dto.CurrencyCode, Key = dto.IdempotencyKey, dto.Notes, Actor = userId
                }, tx, cancellationToken: cancellationToken));
            await TravelSupplierBillRepository.HistoryAsync(connection, tx, tenantId, "PayoutRecorded", null, number, dto.Amount, dto.CurrencyCode,
                $"{dto.BeneficiaryType} #{dto.BeneficiaryId} · {dto.Method}", userId, cancellationToken, payoutId: id);
            tx.Commit();
            return new TravelFinanceWriteResult(id, null);
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            tx.Rollback();
            return TravelFinanceWriteResult.Fail("This payout is being recorded already. Reload the list.");
        }
    }

    public async Task<bool> UpdateAttributionAsync(int tenantId, int invoiceId, int? agentUserId, int? subAgentId, int? userId,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();
        var changed = await connection.QuerySingleOrNullAsync<(int? OldAgent, int? OldSub)>(new CommandDefinition("""
            UPDATE TravelInvoices SET AgentUserId = @AgentUserId, SubAgentId = @SubAgentId, UpdatedAt = SYSUTCDATETIME()
            OUTPUT deleted.AgentUserId, deleted.SubAgentId
            WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0 AND Status <> N'Void'
            """, new { Id = invoiceId, TenantId = tenantId, AgentUserId = agentUserId, SubAgentId = subAgentId }, tx, cancellationToken: cancellationToken));
        if (changed is not { } old)
        {
            tx.Rollback();
            return false;
        }
        await TravelSupplierBillRepository.HistoryAsync(connection, tx, tenantId, "AttributionChanged", Describe(old.OldAgent, old.OldSub),
            Describe(agentUserId, subAgentId), null, null, null, userId, cancellationToken, invoiceId: invoiceId);
        tx.Commit();
        return true;
    }

    private static string Describe(int? agent, int? sub) => $"agent {(agent?.ToString() ?? "none")} · sub-agent {(sub?.ToString() ?? "none")}";
}
