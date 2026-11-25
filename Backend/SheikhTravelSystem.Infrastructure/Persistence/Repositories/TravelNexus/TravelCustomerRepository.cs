using Dapper;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Customers.DTOs;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories.TravelNexus;

/// <summary>
/// Travel customers = Customers ⨝ TravelCustomerProfiles. The profile row is the tenant root
/// (<c>p.TenantId = @TenantId</c>); every join repeats the tenant predicate.
/// </summary>
public sealed class TravelCustomerRepository(IDbConnectionFactory dbFactory) : ITravelCustomerRepository
{
    private const string ListSelect = """
        SELECT c.Id AS CustomerId, p.CustomerKind, c.FullName, p.ContactPerson, c.Phone, c.Email, p.City,
               p.AssignedAgentUserId, u.FullName AS AssignedAgentName,
               p.BranchId, b.Name AS BranchName, ls.Name AS LeadSourceName,
               (SELECT COUNT(1) FROM TravelTravelers t
                 WHERE t.TenantId = p.TenantId AND t.CustomerId = c.Id AND t.IsDeleted = 0) AS TravelerCount,
               (SELECT MIN(t.PassportExpiryDate) FROM TravelTravelers t
                 WHERE t.TenantId = p.TenantId AND t.CustomerId = c.Id AND t.IsDeleted = 0) AS NextPassportExpiry,
               p.CreatedAt
        """;

    private const string FromJoins = """
        FROM TravelCustomerProfiles p
        INNER JOIN Customers c ON c.Id = p.CustomerId AND c.TenantId = p.TenantId AND c.IsDeleted = 0
        LEFT JOIN Users u ON u.Id = p.AssignedAgentUserId AND u.TenantId = p.TenantId
        LEFT JOIN Branches b ON b.Id = p.BranchId AND b.TenantId = p.TenantId
        LEFT JOIN TravelLeadSources ls ON ls.Id = p.LeadSourceId AND ls.TenantId = p.TenantId
        """;

    public async Task<PagedResult<TravelCustomerListItemDto>> GetPagedAsync(
        int tenantId, TravelBranchScope scope, int page, int pageSize,
        string? search, string? customerKind, int? agentUserId, int? branchId,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        var clauses = new List<string> { "p.TenantId = @TenantId", "p.IsDeleted = 0" };
        TravelSql.ApplyBranchScope(scope, "p.BranchId", clauses, p);

        if (search is not null)
        {
            clauses.Add("(c.FullName LIKE @Search OR c.Phone LIKE @Search OR c.Email LIKE @Search OR p.ContactPerson LIKE @Search)");
            p.Add("Search", $"%{TravelSql.EscapeLike(search)}%");
        }
        if (customerKind is not null) { clauses.Add("p.CustomerKind = @Kind"); p.Add("Kind", customerKind); }
        if (agentUserId.HasValue) { clauses.Add("p.AssignedAgentUserId = @AgentId"); p.Add("AgentId", agentUserId); }
        if (branchId.HasValue) { clauses.Add("p.BranchId = @FilterBranchId"); p.Add("FilterBranchId", branchId); }

        var where = string.Join(" AND ", clauses);
        var total = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            $"SELECT COUNT(1) {FromJoins} WHERE {where}", p, cancellationToken: cancellationToken));

        p.Add("Offset", (page - 1) * pageSize);
        p.Add("PageSize", pageSize);
        var items = (await connection.QueryAsync<TravelCustomerListItemDto>(new CommandDefinition($"""
            {ListSelect}
            {FromJoins}
            WHERE {where}
            ORDER BY p.CreatedAt DESC, c.Id DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
            """, p, cancellationToken: cancellationToken))).ToList();

        return new PagedResult<TravelCustomerListItemDto> { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
    }

    public async Task<TravelCustomerDto?> GetByIdAsync(int customerId, int tenantId, TravelBranchScope scope, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        p.Add("CustomerId", customerId);
        var clauses = new List<string> { "p.TenantId = @TenantId", "p.CustomerId = @CustomerId", "p.IsDeleted = 0" };
        TravelSql.ApplyBranchScope(scope, "p.BranchId", clauses, p);

        return await connection.QuerySingleOrDefaultAsync<TravelCustomerDto>(new CommandDefinition($"""
            {ListSelect},
               c.Address, c.CNIC AS Cnic, p.TaxNumber, p.AlternatePhone, p.LeadSourceId, p.Notes
            {FromJoins}
            WHERE {string.Join(" AND ", clauses)}
            """, p, cancellationToken: cancellationToken));
    }

    public async Task<TravelCustomerRef?> GetRefAsync(int customerId, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<TravelCustomerRef>(new CommandDefinition("""
            SELECT p.CustomerId, p.BranchId
            FROM TravelCustomerProfiles p
            INNER JOIN Customers c ON c.Id = p.CustomerId AND c.TenantId = p.TenantId AND c.IsDeleted = 0
            WHERE p.CustomerId = @CustomerId AND p.TenantId = @TenantId AND p.IsDeleted = 0
            """, new { CustomerId = customerId, TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    public async Task<ExistingCustomerState> GetExistingCustomerStateAsync(int customerId, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<int?>(new CommandDefinition("""
            SELECT CASE WHEN EXISTS (
                       SELECT 1 FROM TravelCustomerProfiles p
                       WHERE p.CustomerId = c.Id AND p.TenantId = c.TenantId AND p.IsDeleted = 0)
                   THEN 1 ELSE 0 END
            FROM Customers c
            WHERE c.Id = @CustomerId AND c.TenantId = @TenantId AND c.IsDeleted = 0
            """, new { CustomerId = customerId, TenantId = tenantId }, cancellationToken: cancellationToken));

        return row switch
        {
            null => ExistingCustomerState.NotFound,
            1 => ExistingCustomerState.AlreadyTravelCustomer,
            _ => ExistingCustomerState.Available
        };
    }

    public async Task<int?> FindCustomerIdByCnicAsync(int tenantId, string cnic, int? excludeCustomerId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int?>(new CommandDefinition("""
            SELECT TOP 1 Id FROM Customers
            WHERE TenantId = @TenantId AND IsDeleted = 0
              AND REPLACE(CNIC, '-', '') = REPLACE(@Cnic, '-', '')
              AND (@ExcludeId IS NULL OR Id <> @ExcludeId)
            """, new { TenantId = tenantId, Cnic = cnic, ExcludeId = excludeCustomerId },
            cancellationToken: cancellationToken));
    }

    public async Task<string?> ValidateReferencesAsync(
        int tenantId, int? agentUserId, int? branchId, int? leadSourceId, CancellationToken cancellationToken = default)
    {
        if (agentUserId is null && branchId is null && leadSourceId is null) return null;

        using var connection = dbFactory.CreateConnection();
        var r = await connection.QuerySingleAsync<(int AgentOk, int BranchOk, int LeadOk)>(new CommandDefinition("""
            SELECT
              CASE WHEN @AgentId IS NULL OR EXISTS (SELECT 1 FROM Users WHERE Id = @AgentId AND TenantId = @TenantId) THEN 1 ELSE 0 END,
              CASE WHEN @BranchId IS NULL OR EXISTS (SELECT 1 FROM Branches WHERE Id = @BranchId AND TenantId = @TenantId AND IsActive = 1) THEN 1 ELSE 0 END,
              CASE WHEN @LeadId IS NULL OR EXISTS (SELECT 1 FROM TravelLeadSources WHERE Id = @LeadId AND TenantId = @TenantId AND IsDeleted = 0) THEN 1 ELSE 0 END
            """, new { TenantId = tenantId, AgentId = agentUserId, BranchId = branchId, LeadId = leadSourceId },
            cancellationToken: cancellationToken));

        if (r.AgentOk == 0) return "Assigned agent not found.";
        if (r.BranchOk == 0) return "Branch not found.";
        if (r.LeadOk == 0) return "Lead source not found.";
        return null;
    }

    public async Task<int> CreateAsync(int tenantId, CreateTravelCustomerDto dto, int? userId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();

        int customerId;
        if (dto.ExistingCustomerId is int existingId)
        {
            var rows = await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE Customers SET FullName = @FullName, Phone = @Phone, Email = @Email, Address = @Address,
                    CNIC = @Cnic, UpdatedAt = SYSUTCDATETIME()
                WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0
                """, new { Id = existingId, TenantId = tenantId, dto.FullName, dto.Phone, dto.Email, dto.Address, dto.Cnic },
                tx, cancellationToken: cancellationToken));
            if (rows == 0)
                throw new InvalidOperationException("Customer disappeared during travel profile creation.");
            customerId = existingId;
        }
        else
        {
            customerId = await TravelCustomerSql.InsertCustomerAsync(
                connection, tx, tenantId, dto.FullName, dto.Phone, dto.Email, dto.Address, dto.Cnic, cancellationToken);
        }

        await TravelCustomerSql.UpsertProfileAsync(connection, tx, tenantId, customerId,
            new TravelProfileValues(dto.CustomerKind, dto.FullName, dto.ContactPerson, dto.TaxNumber, dto.AlternatePhone,
                dto.City, dto.AssignedAgentUserId, dto.BranchId, dto.LeadSourceId, dto.Notes),
            userId, cancellationToken);

        tx.Commit();
        return customerId;
    }

    public async Task<bool> UpdateAsync(int customerId, int tenantId, UpdateTravelCustomerDto dto, string? cnic, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();

        var p = TravelCustomerSql.ProfileParams(tenantId, customerId,
            new TravelProfileValues(dto.CustomerKind, dto.FullName, dto.ContactPerson, dto.TaxNumber, dto.AlternatePhone,
                dto.City, dto.AssignedAgentUserId, dto.BranchId, dto.LeadSourceId, dto.Notes));
        var profileRows = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE TravelCustomerProfiles SET
                CustomerKind = @Kind, CompanyName = @CompanyName, ContactPerson = @ContactPerson, TaxNumber = @TaxNumber,
                AlternatePhone = @AlternatePhone, City = @City, AssignedAgentUserId = @AgentId, BranchId = @BranchId,
                LeadSourceId = @LeadSourceId, Notes = @Notes, UpdatedAt = SYSUTCDATETIME()
            WHERE TenantId = @TenantId AND CustomerId = @CustomerId AND IsDeleted = 0
            """, p, tx, cancellationToken: cancellationToken));
        if (profileRows == 0)
        {
            tx.Rollback();
            return false;
        }

        var customerRows = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE Customers SET FullName = @FullName, Phone = @Phone, Email = @Email, Address = @Address,
                CNIC = @Cnic, UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @CustomerId AND TenantId = @TenantId AND IsDeleted = 0
            """, new { CustomerId = customerId, TenantId = tenantId, dto.FullName, dto.Phone, dto.Email, dto.Address, Cnic = cnic },
            tx, cancellationToken: cancellationToken));
        if (customerRows == 0)
        {
            tx.Rollback();
            return false;
        }

        tx.Commit();
        return true;
    }

    public async Task<bool> DeleteAsync(int customerId, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE TravelCustomerProfiles SET IsDeleted = 1, UpdatedAt = SYSUTCDATETIME()
            WHERE CustomerId = @CustomerId AND TenantId = @TenantId AND IsDeleted = 0
            """, new { CustomerId = customerId, TenantId = tenantId }, cancellationToken: cancellationToken)) > 0;
    }

    public async Task<string?> GetCnicAsync(int customerId, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT CNIC FROM Customers WHERE Id = @CustomerId AND TenantId = @TenantId AND IsDeleted = 0",
            new { CustomerId = customerId, TenantId = tenantId }, cancellationToken: cancellationToken));
    }
}
