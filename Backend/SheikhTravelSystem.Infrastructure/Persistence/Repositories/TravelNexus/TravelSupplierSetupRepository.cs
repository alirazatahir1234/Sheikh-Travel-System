using Dapper;
using Microsoft.Data.SqlClient;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.SupplierFinance.DTOs;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories.TravelNexus;

/// <summary>Exchange rates, supplier rate cards and supplier commission rules. Every statement filters on TenantId.</summary>
public sealed class TravelSupplierSetupRepository(IDbConnectionFactory dbFactory) : ITravelSupplierSetupRepository
{
    // ── Exchange rates ──────────────────────────────────────────────────────

    public async Task<IReadOnlyList<TravelExchangeRateDto>> GetExchangeRatesAsync(int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return (await connection.QueryAsync<TravelExchangeRateDto>(new CommandDefinition("""
            SELECT Id, CurrencyCode, RateToReporting, EffectiveFrom FROM TravelExchangeRates
            WHERE TenantId = @TenantId
            ORDER BY CurrencyCode, EffectiveFrom DESC
            """, new { TenantId = tenantId }, cancellationToken: cancellationToken))).ToList();
    }

    public async Task<int> SaveExchangeRateAsync(int tenantId, int? id, SaveTravelExchangeRateDto dto, int? userId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var args = new { TenantId = tenantId, Id = id, dto.CurrencyCode, dto.RateToReporting, EffectiveFrom = dto.EffectiveFrom.Date, UserId = userId };
        try
        {
            if (id is null)
                return await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
                    INSERT INTO TravelExchangeRates (TenantId, CurrencyCode, RateToReporting, EffectiveFrom, CreatedBy)
                    VALUES (@TenantId, @CurrencyCode, @RateToReporting, @EffectiveFrom, @UserId);
                    SELECT CAST(SCOPE_IDENTITY() AS INT);
                    """, args, cancellationToken: cancellationToken));
            var rows = await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE TravelExchangeRates SET CurrencyCode = @CurrencyCode, RateToReporting = @RateToReporting, EffectiveFrom = @EffectiveFrom
                WHERE Id = @Id AND TenantId = @TenantId
                """, args, cancellationToken: cancellationToken));
            return rows == 0 ? 0 : id.Value;
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            return -1; // UX_TravelExchangeRates_Tenant_Currency_Date
        }
    }

    public async Task<bool> DeleteExchangeRateAsync(int tenantId, int id, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM TravelExchangeRates WHERE Id = @Id AND TenantId = @TenantId",
            new { Id = id, TenantId = tenantId }, cancellationToken: cancellationToken)) > 0;
    }

    // ── Rate cards ──────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<TravelSupplierRateDto>> GetRatesAsync(int tenantId, int supplierId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return (await connection.QueryAsync<TravelSupplierRateDto>(new CommandDefinition("""
            SELECT r.Id, r.SupplierId, r.RateType, r.HotelId, h.Name AS HotelName, r.RoomType, r.Description, r.ValidFrom, r.ValidTo, r.Amount,
                   r.CurrencyCode, r.IsActive
            FROM TravelSupplierRates r
            LEFT JOIN TravelHotels h ON h.Id = r.HotelId AND h.TenantId = r.TenantId
            WHERE r.TenantId = @TenantId AND r.SupplierId = @SupplierId AND r.IsDeleted = 0
            ORDER BY r.RateType, h.Name, r.RoomType, r.ValidFrom DESC
            """, new { TenantId = tenantId, SupplierId = supplierId }, cancellationToken: cancellationToken))).ToList();
    }

    public async Task<int> SaveRateAsync(int tenantId, int supplierId, int? id, SaveTravelSupplierRateDto dto, int? userId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var args = new
        {
            TenantId = tenantId, SupplierId = supplierId, Id = id, dto.RateType, dto.HotelId, dto.RoomType, dto.Description,
            ValidFrom = dto.ValidFrom.Date, ValidTo = dto.ValidTo.Date, dto.Amount, dto.CurrencyCode, dto.IsActive, UserId = userId
        };
        if (id is null)
            return await connection.ExecuteScalarAsync<int?>(new CommandDefinition("""
                INSERT INTO TravelSupplierRates (TenantId, SupplierId, RateType, HotelId, RoomType, Description, ValidFrom, ValidTo, Amount, CurrencyCode,
                    IsActive, CreatedBy)
                SELECT @TenantId, s.Id, @RateType, @HotelId, @RoomType, @Description, @ValidFrom, @ValidTo, @Amount, @CurrencyCode, @IsActive, @UserId
                FROM TravelSuppliers s WHERE s.Id = @SupplierId AND s.TenantId = @TenantId AND s.IsDeleted = 0;
                SELECT CAST(SCOPE_IDENTITY() AS INT);
                """, args, cancellationToken: cancellationToken)) ?? 0;
        var rows = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE TravelSupplierRates SET RateType = @RateType, HotelId = @HotelId, RoomType = @RoomType, Description = @Description,
                ValidFrom = @ValidFrom, ValidTo = @ValidTo, Amount = @Amount, CurrencyCode = @CurrencyCode, IsActive = @IsActive, UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id AND TenantId = @TenantId AND SupplierId = @SupplierId AND IsDeleted = 0
            """, args, cancellationToken: cancellationToken));
        return rows == 0 ? 0 : id.Value;
    }

    public async Task<bool> DeleteRateAsync(int tenantId, int supplierId, int id, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE TravelSupplierRates SET IsDeleted = 1, UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id AND TenantId = @TenantId AND SupplierId = @SupplierId AND IsDeleted = 0
            """, new { Id = id, TenantId = tenantId, SupplierId = supplierId }, cancellationToken: cancellationToken)) > 0;
    }

    // ── Supplier commission rules ───────────────────────────────────────────

    public async Task<IReadOnlyList<TravelSupplierCommissionRuleDto>> GetCommissionRulesAsync(int tenantId, int supplierId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return (await connection.QueryAsync<TravelSupplierCommissionRuleDto>(new CommandDefinition("""
            SELECT Id, SupplierId, ServiceType, Basis, Rate, CurrencyCode, ValidFrom, ValidTo, IsActive
            FROM TravelSupplierCommissionRules
            WHERE TenantId = @TenantId AND SupplierId = @SupplierId AND IsDeleted = 0
            ORDER BY ServiceType, ValidFrom DESC, Id DESC
            """, new { TenantId = tenantId, SupplierId = supplierId }, cancellationToken: cancellationToken))).ToList();
    }

    public async Task<int> SaveCommissionRuleAsync(int tenantId, int supplierId, int? id, SaveTravelSupplierCommissionRuleDto dto, int? userId,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var args = new
        {
            TenantId = tenantId, SupplierId = supplierId, Id = id, dto.ServiceType, dto.Basis, dto.Rate, dto.CurrencyCode,
            ValidFrom = dto.ValidFrom.Date, ValidTo = dto.ValidTo?.Date, dto.IsActive, UserId = userId
        };
        if (id is null)
            return await connection.ExecuteScalarAsync<int?>(new CommandDefinition("""
                INSERT INTO TravelSupplierCommissionRules (TenantId, SupplierId, ServiceType, Basis, Rate, CurrencyCode, ValidFrom, ValidTo, IsActive, CreatedBy)
                SELECT @TenantId, s.Id, @ServiceType, @Basis, @Rate, @CurrencyCode, @ValidFrom, @ValidTo, @IsActive, @UserId
                FROM TravelSuppliers s WHERE s.Id = @SupplierId AND s.TenantId = @TenantId AND s.IsDeleted = 0;
                SELECT CAST(SCOPE_IDENTITY() AS INT);
                """, args, cancellationToken: cancellationToken)) ?? 0;
        var rows = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE TravelSupplierCommissionRules SET ServiceType = @ServiceType, Basis = @Basis, Rate = @Rate, CurrencyCode = @CurrencyCode,
                ValidFrom = @ValidFrom, ValidTo = @ValidTo, IsActive = @IsActive, UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id AND TenantId = @TenantId AND SupplierId = @SupplierId AND IsDeleted = 0
            """, args, cancellationToken: cancellationToken));
        return rows == 0 ? 0 : id.Value;
    }

    public async Task<bool> DeleteCommissionRuleAsync(int tenantId, int supplierId, int id, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE TravelSupplierCommissionRules SET IsDeleted = 1, UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id AND TenantId = @TenantId AND SupplierId = @SupplierId AND IsDeleted = 0
            """, new { Id = id, TenantId = tenantId, SupplierId = supplierId }, cancellationToken: cancellationToken)) > 0;
    }
}
