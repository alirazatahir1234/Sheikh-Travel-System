using Dapper;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.WhatsApp.DTOs;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories;

public sealed class WhatsAppTemplateRepository(IDbConnectionFactory dbFactory) : IWhatsAppTemplateRepository
{
    private const string SelectSql = """
        SELECT t.Id, t.WhatsAppAccountId, a.Code AS AccountCode,
               t.Name, t.Language, t.Category, t.Status, t.MetaTemplateId, t.BodyPreview
        FROM WhatsAppTemplates t
        INNER JOIN WhatsAppAccounts a ON a.Id = t.WhatsAppAccountId
        """;

    public async Task<IReadOnlyList<WhatsAppTemplateDto>> ListAsync(
        int tenantId,
        int? accountId = null,
        string? status = null,
        CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.QueryAsync<WhatsAppTemplateDto>(new CommandDefinition($"""
            {SelectSql}
            WHERE t.TenantId = @TenantId AND t.IsDeleted = 0
              AND (@AccountId IS NULL OR t.WhatsAppAccountId = @AccountId)
              AND (@Status IS NULL OR t.Status = @Status)
            ORDER BY a.Code, t.Name, t.Language
            """, new { TenantId = tenantId, AccountId = accountId, Status = status },
            cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<WhatsAppTemplateDto?> GetByIdAsync(int tenantId, int id, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<WhatsAppTemplateDto>(new CommandDefinition($"""
            {SelectSql}
            WHERE t.TenantId = @TenantId AND t.Id = @Id AND t.IsDeleted = 0
            """, new { TenantId = tenantId, Id = id }, cancellationToken: ct));
    }

    public async Task<WhatsAppTemplateDto?> GetByNameAsync(
        int tenantId,
        int accountId,
        string name,
        string language,
        CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<WhatsAppTemplateDto>(new CommandDefinition($"""
            {SelectSql}
            WHERE t.TenantId = @TenantId AND t.WhatsAppAccountId = @AccountId
              AND t.Name = @Name AND t.Language = @Language AND t.IsDeleted = 0
            """, new
            {
                TenantId = tenantId,
                AccountId = accountId,
                Name = name.Trim(),
                Language = string.IsNullOrWhiteSpace(language) ? "en" : language.Trim()
            }, cancellationToken: ct));
    }

    public async Task<WhatsAppTemplateDto?> UpsertAsync(
        int tenantId,
        int accountId,
        string name,
        string language,
        string category,
        string status,
        string? metaTemplateId,
        string? bodyPreview,
        CancellationToken ct = default)
    {
        language = string.IsNullOrWhiteSpace(language) ? "en" : language.Trim();
        name = name.Trim();
        using var connection = dbFactory.CreateConnection();
        var existingId = await connection.ExecuteScalarAsync<int?>(new CommandDefinition("""
            SELECT Id FROM WhatsAppTemplates
            WHERE TenantId = @TenantId AND WhatsAppAccountId = @AccountId
              AND Name = @Name AND Language = @Language AND IsDeleted = 0
            """, new { TenantId = tenantId, AccountId = accountId, Name = name, Language = language },
            cancellationToken: ct));

        if (existingId is null)
        {
            existingId = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
                INSERT INTO WhatsAppTemplates
                    (TenantId, WhatsAppAccountId, Name, Language, Category, Status, MetaTemplateId, BodyPreview)
                OUTPUT INSERTED.Id
                VALUES (@TenantId, @AccountId, @Name, @Language, @Category, @Status, @MetaTemplateId, @BodyPreview)
                """, new
                {
                    TenantId = tenantId,
                    AccountId = accountId,
                    Name = name,
                    Language = language,
                    Category = category,
                    Status = status,
                    MetaTemplateId = metaTemplateId,
                    BodyPreview = bodyPreview
                }, cancellationToken: ct));
        }
        else
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE WhatsAppTemplates
                SET Category = @Category,
                    Status = @Status,
                    MetaTemplateId = @MetaTemplateId,
                    BodyPreview = @BodyPreview,
                    UpdatedAt = SYSUTCDATETIME()
                WHERE Id = @Id AND TenantId = @TenantId
                """, new
                {
                    Id = existingId.Value,
                    TenantId = tenantId,
                    Category = category,
                    Status = status,
                    MetaTemplateId = metaTemplateId,
                    BodyPreview = bodyPreview
                }, cancellationToken: ct));
        }

        return await GetByIdAsync(tenantId, existingId.Value, ct);
    }

    public async Task<WhatsAppTemplateDto?> SetStatusAsync(
        int tenantId,
        int id,
        string status,
        string? metaTemplateId = null,
        CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE WhatsAppTemplates
            SET Status = @Status,
                MetaTemplateId = COALESCE(@MetaTemplateId, MetaTemplateId),
                UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0
            """, new { Id = id, TenantId = tenantId, Status = status, MetaTemplateId = metaTemplateId },
            cancellationToken: ct));
        if (rows == 0) return null;
        return await GetByIdAsync(tenantId, id, ct);
    }
}
