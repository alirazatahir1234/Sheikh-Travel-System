using Dapper;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Suppliers.DTOs;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories.TravelNexus;

/// <summary>Travel suppliers. Every statement filters on <c>TenantId = @TenantId</c>; usage subqueries repeat the predicate.</summary>
public sealed class TravelSupplierRepository(IDbConnectionFactory dbFactory) : ITravelSupplierRepository
{
    private const string Select = """
        SELECT s.Id, s.Name, s.SupplierType, s.IataCode, s.ContactName, s.Phone, s.Email, s.CurrencyCode, s.IsActive, s.Notes,
               (SELECT COUNT(1) FROM TravelFlightBookings f WHERE f.SupplierId = s.Id AND f.TenantId = s.TenantId AND f.IsDeleted = 0)
             + (SELECT COUNT(1) FROM TravelHotelBookings h WHERE h.SupplierId = s.Id AND h.TenantId = s.TenantId AND h.IsDeleted = 0) AS UsageCount
        FROM TravelSuppliers s
        """;

    public async Task<IReadOnlyList<TravelSupplierDto>> GetListAsync(int tenantId, string? supplierType, bool? isActive, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return (await connection.QueryAsync<TravelSupplierDto>(new CommandDefinition($"""
            {Select}
            WHERE s.TenantId = @TenantId AND s.IsDeleted = 0 AND (@Type IS NULL OR s.SupplierType = @Type) AND (@IsActive IS NULL OR s.IsActive = @IsActive)
            ORDER BY s.SupplierType, s.Name
            """, new { TenantId = tenantId, Type = supplierType, IsActive = isActive }, cancellationToken: cancellationToken))).ToList();
    }

    public async Task<TravelSupplierDto?> GetByIdAsync(int id, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<TravelSupplierDto>(new CommandDefinition($"""
            {Select}
            WHERE s.Id = @Id AND s.TenantId = @TenantId AND s.IsDeleted = 0
            """, new { Id = id, TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    public async Task<bool> NameExistsAsync(int tenantId, string name, int? excludeId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT COUNT(1) FROM TravelSuppliers
            WHERE TenantId = @TenantId AND Name = @Name AND IsDeleted = 0 AND (@ExcludeId IS NULL OR Id <> @ExcludeId)
            """, new { TenantId = tenantId, Name = name, ExcludeId = excludeId }, cancellationToken: cancellationToken)) > 0;
    }

    public async Task<int> CreateAsync(int tenantId, SaveTravelSupplierDto dto, int? userId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            INSERT INTO TravelSuppliers (TenantId, Name, SupplierType, IataCode, ContactName, Phone, Email, CurrencyCode, IsActive, Notes, CreatedBy)
            VALUES (@TenantId, @Name, @SupplierType, @IataCode, @ContactName, @Phone, @Email, @CurrencyCode, @IsActive, @Notes, @UserId);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """, new
            {
                TenantId = tenantId, dto.Name, dto.SupplierType, dto.IataCode, dto.ContactName, dto.Phone, dto.Email, dto.CurrencyCode,
                dto.IsActive, dto.Notes, UserId = userId
            }, cancellationToken: cancellationToken));
    }

    public async Task<bool> UpdateAsync(int id, int tenantId, SaveTravelSupplierDto dto, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE TravelSuppliers SET Name = @Name, SupplierType = @SupplierType, IataCode = @IataCode, ContactName = @ContactName,
                Phone = @Phone, Email = @Email, CurrencyCode = @CurrencyCode, IsActive = @IsActive, Notes = @Notes, UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0
            """, new
            {
                Id = id, TenantId = tenantId, dto.Name, dto.SupplierType, dto.IataCode, dto.ContactName, dto.Phone, dto.Email, dto.CurrencyCode,
                dto.IsActive, dto.Notes
            }, cancellationToken: cancellationToken)) > 0;
    }

    public async Task<bool> DeleteAsync(int id, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE TravelSuppliers SET IsDeleted = 1, UpdatedAt = SYSUTCDATETIME() WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0
            """, new { Id = id, TenantId = tenantId }, cancellationToken: cancellationToken)) > 0;
    }
}
