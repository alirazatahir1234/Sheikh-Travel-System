using Dapper;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories;

public sealed class TenantRepository(IDbConnectionFactory dbFactory) : ITenantRepository
{
    public async Task<TenantBrandingRow?> GetBrandingAsync(
        int tenantId,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<TenantBrandingRow>(
            new CommandDefinition("""
                SELECT t.Id, t.Name, t.Slug, COALESCE(b.LogoUrl, t.LogoUrl) AS LogoUrl, COALESCE(b.PrimaryColor, t.PrimaryColor) AS PrimaryColor
                FROM Tenants t
                LEFT JOIN TenantBranding b ON b.TenantId = t.Id
                WHERE t.Id = @Id AND t.IsActive = 1
                """,
                new { Id = tenantId },
                cancellationToken: cancellationToken));

        return row is null || row.Id == 0 ? null : row;
    }

    public async Task UpdateBrandingAsync(
        int tenantId,
        string? logoUrl,
        string? primaryColor,
        string? website,
        string? supportEmail,
        string? country,
        string? currencyCode,
        string? timeZone,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        var exists = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT CASE WHEN EXISTS(SELECT 1 FROM Tenants WHERE Id = @Id) THEN 1 ELSE 0 END",
            new { Id = tenantId }, cancellationToken: cancellationToken));
        if (!exists) throw new NotFoundException("Tenant", tenantId);

        await connection.ExecuteAsync(new CommandDefinition("""
            IF EXISTS (SELECT 1 FROM TenantBranding WHERE TenantId = @TenantId)
                UPDATE TenantBranding
                SET LogoUrl = @LogoUrl, PrimaryColor = @PrimaryColor, Website = @Website,
                    SupportEmail = @SupportEmail, Country = @Country, CurrencyCode = @CurrencyCode, TimeZone = @TimeZone
                WHERE TenantId = @TenantId;
            ELSE
                INSERT INTO TenantBranding (TenantId, LogoUrl, PrimaryColor, Website, SupportEmail, Country, CurrencyCode, TimeZone)
                VALUES (@TenantId, @LogoUrl, @PrimaryColor, @Website, @SupportEmail, @Country, @CurrencyCode, @TimeZone);
            """, new
        {
            TenantId = tenantId,
            LogoUrl = logoUrl,
            PrimaryColor = primaryColor,
            Website = website,
            SupportEmail = supportEmail,
            Country = country,
            CurrencyCode = currencyCode,
            TimeZone = timeZone
        }, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE Tenants SET LogoUrl = @LogoUrl, PrimaryColor = @PrimaryColor, UpdatedAt = GETUTCDATE()
            WHERE Id = @TenantId
            """, new
        {
            TenantId = tenantId,
            LogoUrl = logoUrl,
            PrimaryColor = primaryColor
        }, cancellationToken: cancellationToken));
    }

    public async Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT CASE WHEN EXISTS(SELECT 1 FROM Tenants WHERE Slug = @Slug) THEN 1 ELSE 0 END",
            new { Slug = slug }, cancellationToken: cancellationToken));
    }
}
