using Dapper;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Customers.DTOs;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories;

public sealed class CustomerRepository(
    IDbConnectionFactory dbFactory,
    ITenantContext tenantContext,
    ICurrentUserService currentUser,
    IDataScopeEngine dataScopeEngine) : ICustomerRepository
{
    public async Task<CustomerMutationResult<int>> CreateAsync(
        CreateCustomerDto dto,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        if (!string.IsNullOrWhiteSpace(dto.CNIC))
        {
            var dup = await connection.ExecuteScalarAsync<int?>(
                new CommandDefinition(
                    "SELECT TOP 1 Id FROM Customers WHERE CNIC = @CNIC AND IsDeleted = 0",
                    new { dto.CNIC },
                    cancellationToken: cancellationToken));
            if (dup.HasValue)
                return CustomerMutationResult<int>.Fail("A customer with this CNIC already exists.");
        }

        var id = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                @"INSERT INTO Customers (FullName, Phone, Email, Address, CNIC, FatherOrHusbandName, Gender, DateOfBirth, Nationality, IsActive, CreatedAt, IsDeleted)
                  VALUES (@FullName, @Phone, @Email, @Address, @CNIC, @FatherOrHusbandName, @Gender, @DateOfBirth, @Nationality, 1, @CreatedAt, 0);
                  SELECT SCOPE_IDENTITY();",
                new
                {
                    dto.FullName,
                    dto.Phone,
                    dto.Email,
                    dto.Address,
                    dto.CNIC,
                    dto.FatherOrHusbandName,
                    dto.Gender,
                    dto.DateOfBirth,
                    dto.Nationality,
                    CreatedAt = DateTime.UtcNow
                },
                cancellationToken: cancellationToken));

        return CustomerMutationResult<int>.Ok(id);
    }

    public async Task<CustomerMutationResult<bool>> UpdateAsync(
        int id,
        UpdateCustomerDto dto,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        var exists = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT CASE WHEN EXISTS(SELECT 1 FROM Customers WHERE Id = @Id AND IsDeleted = 0) THEN 1 ELSE 0 END",
                new { Id = id },
                cancellationToken: cancellationToken));

        if (!exists)
            throw new NotFoundException("Customer", id);

        if (!string.IsNullOrWhiteSpace(dto.CNIC))
        {
            var conflict = await connection.ExecuteScalarAsync<int?>(
                new CommandDefinition(
                    @"SELECT TOP 1 Id FROM Customers WHERE CNIC = @CNIC AND IsDeleted = 0 AND Id <> @Id",
                    new { dto.CNIC, Id = id },
                    cancellationToken: cancellationToken));
            if (conflict.HasValue)
                return CustomerMutationResult<bool>.Fail("Another customer already uses this CNIC.");
        }

        await connection.ExecuteAsync(
            new CommandDefinition(
                @"UPDATE Customers SET FullName = @FullName, Phone = @Phone, Email = @Email,
                  Address = @Address, CNIC = @CNIC,
                  FatherOrHusbandName = @FatherOrHusbandName, Gender = @Gender, DateOfBirth = @DateOfBirth, Nationality = @Nationality,
                  UpdatedAt = @UpdatedAt WHERE Id = @Id",
                new
                {
                    dto.FullName,
                    dto.Phone,
                    dto.Email,
                    dto.Address,
                    dto.CNIC,
                    dto.FatherOrHusbandName,
                    dto.Gender,
                    dto.DateOfBirth,
                    dto.Nationality,
                    UpdatedAt = DateTime.UtcNow,
                    Id = id
                },
                cancellationToken: cancellationToken));

        return CustomerMutationResult<bool>.Ok(true);
    }

    public async Task SoftDeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        var exists = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT CASE WHEN EXISTS(SELECT 1 FROM Customers WHERE Id = @Id AND IsDeleted = 0) THEN 1 ELSE 0 END",
                new { Id = id },
                cancellationToken: cancellationToken));

        if (!exists)
            throw new NotFoundException("Customer", id);

        await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE Customers SET IsDeleted = 1, UpdatedAt = @UpdatedAt WHERE Id = @Id",
                new { Id = id, UpdatedAt = DateTime.UtcNow },
                cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<CustomerDto>> GetPagedAsync(
        int page,
        int pageSize,
        string? search,
        bool? isActive,
        string? recency,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var offset = (page - 1) * pageSize;
        var tenantId = tenantContext.GetRequiredTenantId();
        var (whereClause, parameters) = CustomerQueryFilters.Build(
            search,
            isActive,
            recency,
            tenantId: tenantId);

        parameters.Add("Offset", offset);
        parameters.Add("PageSize", pageSize);

        // Customers have no BranchId/DepartmentId — tenant isolation + soft company-wide/pass-through only.
        if (currentUser.UserId is int userId)
            _ = await dataScopeEngine.ResolveAsync(userId, tenantId, cancellationToken);

        var customers = await connection.QueryAsync<CustomerDto>(
            new CommandDefinition(
                $@"SELECT Id, FullName, Phone, Email, Address, CNIC, IsActive, CreatedAt,
                  FatherOrHusbandName, Gender, DateOfBirth, Nationality
                  FROM Customers
                  {whereClause}
                  ORDER BY CreatedAt DESC
                  OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY",
                parameters,
                cancellationToken: cancellationToken));

        var totalCount = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                $"SELECT COUNT(*) FROM Customers {whereClause}",
                parameters,
                cancellationToken: cancellationToken));

        return new PagedResult<CustomerDto>
        {
            Items = customers.ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<CustomerDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        var customer = await connection.QuerySingleOrDefaultAsync<CustomerDto>(
            new CommandDefinition(
                @"SELECT Id, FullName, Phone, Email, Address, CNIC, IsActive, CreatedAt,
                  FatherOrHusbandName, Gender, DateOfBirth, Nationality
                  FROM Customers WHERE Id = @Id AND IsDeleted = 0",
                new { Id = id },
                cancellationToken: cancellationToken));

        if (customer is null)
            throw new NotFoundException("Customer", id);

        return customer;
    }

    public async Task<CustomerListStatsDto> GetListStatsAsync(
        string? search,
        bool? isActive,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();
        var (whereClause, parameters) = CustomerQueryFilters.Build(
            search,
            isActive,
            recency: null,
            applyRecency: false,
            tenantId: tenantId);

        return await connection.QuerySingleAsync<CustomerListStatsDto>(
            new CommandDefinition(
                $@"SELECT
                    COUNT(*) AS Total,
                    SUM(CASE WHEN CreatedAt >= @NewSince THEN 1 ELSE 0 END) AS New,
                    SUM(CASE WHEN CreatedAt < @NewSince THEN 1 ELSE 0 END) AS Returning
                  FROM Customers
                  {whereClause}",
                parameters,
                cancellationToken: cancellationToken));
    }
}

/// <summary>
/// Shared WHERE-clause builder for customer list/stats queries (moved from Application).
/// </summary>
internal static class CustomerQueryFilters
{
    public const int NewCustomerDays = 30;

    public static (string WhereClause, DynamicParameters Parameters) Build(
        string? search,
        bool? isActive,
        string? recency,
        bool applyRecency = true,
        int? tenantId = null)
    {
        var where = "WHERE IsDeleted = 0";
        var parameters = new DynamicParameters();
        var newSince = DateTime.UtcNow.Date.AddDays(-NewCustomerDays);
        parameters.Add("NewSince", newSince);

        if (tenantId.HasValue)
        {
            where += " AND TenantId = @TenantId";
            parameters.Add("TenantId", tenantId.Value);
        }

        if (isActive.HasValue)
        {
            where += " AND IsActive = @IsActive";
            parameters.Add("IsActive", isActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            where += @" AND (
                FullName LIKE @SearchPattern OR
                Phone LIKE @SearchPattern OR
                Email LIKE @SearchPattern OR
                CNIC LIKE @SearchPattern OR
                Address LIKE @SearchPattern)";
            parameters.Add("SearchPattern", $"%{search.Trim()}%");
        }

        if (applyRecency && !string.IsNullOrWhiteSpace(recency))
        {
            switch (recency.Trim().ToUpperInvariant())
            {
                case "NEW":
                    where += " AND CreatedAt >= @NewSince";
                    break;
                case "RETURNING":
                    where += " AND CreatedAt < @NewSince";
                    break;
            }
        }

        return (where, parameters);
    }
}
