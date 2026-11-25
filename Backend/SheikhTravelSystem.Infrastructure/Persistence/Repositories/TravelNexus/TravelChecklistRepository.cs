using Dapper;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Checklists.DTOs;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories.TravelNexus;

/// <summary>Versioned document checklists. Root predicate <c>t.TenantId = @TenantId</c>; only Drafts are mutated.</summary>
public sealed class TravelChecklistRepository(IDbConnectionFactory dbFactory) : ITravelChecklistRepository
{
    private const string TemplateSelect = """
        SELECT t.Id, t.CountryCode, c.Name AS CountryName, t.VisaTypeId, vt.Name AS VisaTypeName, t.Name, t.Version, t.Status,
               (SELECT COUNT(1) FROM TravelChecklistItems i WHERE i.TenantId = t.TenantId AND i.TemplateId = t.Id) AS ItemCount,
               t.ActivatedAt, t.CreatedAt, t.Notes
        FROM TravelChecklistTemplates t
        INNER JOIN TravelCountries c ON c.Code = t.CountryCode
        LEFT JOIN TravelVisaTypes vt ON vt.Id = t.VisaTypeId AND vt.TenantId = t.TenantId
        """;

    public async Task<IReadOnlyList<TravelChecklistTemplateListItemDto>> GetListAsync(int tenantId, string? countryCode, string? status, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return (await connection.QueryAsync<TravelChecklistTemplateListItemDto>(new CommandDefinition($"""
            {TemplateSelect}
            WHERE t.TenantId = @TenantId AND t.IsDeleted = 0
              AND (@CountryCode IS NULL OR t.CountryCode = @CountryCode)
              AND (@Status IS NULL OR t.Status = @Status)
            ORDER BY c.Name, vt.Name, t.Version DESC
            """, new { TenantId = tenantId, CountryCode = countryCode, Status = status }, cancellationToken: cancellationToken))).ToList();
    }

    public async Task<TravelChecklistTemplateDto?> GetByIdAsync(int id, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var template = await connection.QuerySingleOrDefaultAsync<TravelChecklistTemplateDto>(new CommandDefinition($"""
            {TemplateSelect}
            WHERE t.Id = @Id AND t.TenantId = @TenantId AND t.IsDeleted = 0
            """, new { Id = id, TenantId = tenantId }, cancellationToken: cancellationToken));
        if (template is not null)
            template.Items = await GetItemsAsync(id, tenantId, cancellationToken);
        return template;
    }

    public async Task<IReadOnlyList<TravelChecklistItemDto>> GetItemsAsync(int templateId, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return (await connection.QueryAsync<TravelChecklistItemDto>(new CommandDefinition("""
            SELECT i.Id, i.DocumentTypeId, d.Code AS DocumentTypeCode, d.Name AS DocumentTypeName, i.IsMandatory,
                   i.ApplicantCondition, i.MinValidityDays, i.Instructions, i.SortOrder
            FROM TravelChecklistItems i
            INNER JOIN TravelDocumentTypes d ON d.Id = i.DocumentTypeId AND d.TenantId = i.TenantId
            WHERE i.TemplateId = @TemplateId AND i.TenantId = @TenantId
            ORDER BY i.SortOrder, i.Id
            """, new { TemplateId = templateId, TenantId = tenantId }, cancellationToken: cancellationToken))).ToList();
    }

    public async Task<int> CreateAsync(int tenantId, SaveTravelChecklistTemplateDto dto, int? userId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            INSERT INTO TravelChecklistTemplates (TenantId, CountryCode, VisaTypeId, Name, Version, Status, Notes, CreatedBy)
            SELECT @TenantId, @CountryCode, @VisaTypeId, @Name,
                   ISNULL((SELECT MAX(Version) FROM TravelChecklistTemplates x
                           WHERE x.TenantId = @TenantId AND x.CountryCode = @CountryCode
                             AND ((x.VisaTypeId IS NULL AND @VisaTypeId IS NULL) OR x.VisaTypeId = @VisaTypeId)), 0) + 1,
                   N'Draft', @Notes, @UserId;
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """, new { TenantId = tenantId, dto.CountryCode, dto.VisaTypeId, dto.Name, dto.Notes, UserId = userId },
            cancellationToken: cancellationToken));
    }

    public async Task<bool> UpdateDraftAsync(int id, int tenantId, SaveTravelChecklistTemplateDto dto, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE TravelChecklistTemplates SET CountryCode = @CountryCode, VisaTypeId = @VisaTypeId, Name = @Name, Notes = @Notes,
                UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0 AND Status = N'Draft'
            """, new { Id = id, TenantId = tenantId, dto.CountryCode, dto.VisaTypeId, dto.Name, dto.Notes },
            cancellationToken: cancellationToken)) > 0;
    }

    public async Task<bool> ReplaceItemsAsync(int id, int tenantId, IReadOnlyList<SaveTravelChecklistItemDto> items, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();

        var isDraft = await connection.ExecuteScalarAsync<int?>(new CommandDefinition("""
            SELECT Id FROM TravelChecklistTemplates WITH (UPDLOCK, ROWLOCK)
            WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0 AND Status = N'Draft'
            """, new { Id = id, TenantId = tenantId }, tx, cancellationToken: cancellationToken));
        if (isDraft is null)
        {
            tx.Rollback();
            return false;
        }

        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM TravelChecklistItems WHERE TemplateId = @Id AND TenantId = @TenantId",
            new { Id = id, TenantId = tenantId }, tx, cancellationToken: cancellationToken));

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO TravelChecklistItems (TenantId, TemplateId, DocumentTypeId, IsMandatory, ApplicantCondition,
                    MinValidityDays, Instructions, SortOrder)
                SELECT @TenantId, @TemplateId, d.Id, @IsMandatory, @Condition, @MinValidityDays, @Instructions, @Sort
                FROM TravelDocumentTypes d
                WHERE d.Id = @DocumentTypeId AND d.TenantId = @TenantId AND d.IsDeleted = 0
                """,
                new
                {
                    TenantId = tenantId, TemplateId = id, item.DocumentTypeId, item.IsMandatory, Condition = item.ApplicantCondition,
                    item.MinValidityDays, item.Instructions, Sort = (i + 1) * 10
                }, tx, cancellationToken: cancellationToken));
        }

        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE TravelChecklistTemplates SET UpdatedAt = SYSUTCDATETIME() WHERE Id = @Id AND TenantId = @TenantId",
            new { Id = id, TenantId = tenantId }, tx, cancellationToken: cancellationToken));
        tx.Commit();
        return true;
    }

    public async Task<bool> ActivateAsync(int id, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();

        var scope = await connection.QuerySingleOrDefaultAsync<(string CountryCode, int? VisaTypeId)?>(new CommandDefinition("""
            SELECT CountryCode, VisaTypeId FROM TravelChecklistTemplates WITH (UPDLOCK, ROWLOCK)
            WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0 AND Status = N'Draft'
            """, new { Id = id, TenantId = tenantId }, tx, cancellationToken: cancellationToken));
        if (scope is null)
        {
            tx.Rollback();
            return false;
        }

        // Retire the current Active template of the same scope, then activate this one.
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE TravelChecklistTemplates SET Status = N'Retired', UpdatedAt = SYSUTCDATETIME()
            WHERE TenantId = @TenantId AND IsDeleted = 0 AND Status = N'Active' AND CountryCode = @CountryCode
              AND ((VisaTypeId IS NULL AND @VisaTypeId IS NULL) OR VisaTypeId = @VisaTypeId)
            """, new { TenantId = tenantId, scope.Value.CountryCode, scope.Value.VisaTypeId }, tx, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE TravelChecklistTemplates SET Status = N'Active', ActivatedAt = SYSUTCDATETIME(), UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id AND TenantId = @TenantId
            """, new { Id = id, TenantId = tenantId }, tx, cancellationToken: cancellationToken));

        tx.Commit();
        return true;
    }

    public async Task<int?> NewVersionAsync(int id, int tenantId, int? userId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();

        var newId = await connection.ExecuteScalarAsync<int?>(new CommandDefinition("""
            INSERT INTO TravelChecklistTemplates (TenantId, CountryCode, VisaTypeId, Name, Version, Status, Notes, CreatedBy)
            SELECT t.TenantId, t.CountryCode, t.VisaTypeId, t.Name,
                   (SELECT MAX(x.Version) FROM TravelChecklistTemplates x
                    WHERE x.TenantId = t.TenantId AND x.CountryCode = t.CountryCode
                      AND ((x.VisaTypeId IS NULL AND t.VisaTypeId IS NULL) OR x.VisaTypeId = t.VisaTypeId)) + 1,
                   N'Draft', t.Notes, @UserId
            FROM TravelChecklistTemplates t
            WHERE t.Id = @Id AND t.TenantId = @TenantId AND t.IsDeleted = 0;
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """, new { Id = id, TenantId = tenantId, UserId = userId }, tx, cancellationToken: cancellationToken));
        if (newId is null)
        {
            tx.Rollback();
            return null;
        }

        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO TravelChecklistItems (TenantId, TemplateId, DocumentTypeId, IsMandatory, ApplicantCondition, MinValidityDays, Instructions, SortOrder)
            SELECT i.TenantId, @NewId, i.DocumentTypeId, i.IsMandatory, i.ApplicantCondition, i.MinValidityDays, i.Instructions, i.SortOrder
            FROM TravelChecklistItems i
            WHERE i.TemplateId = @Id AND i.TenantId = @TenantId
            """, new { Id = id, NewId = newId, TenantId = tenantId }, tx, cancellationToken: cancellationToken));

        tx.Commit();
        return newId;
    }

    public async Task<bool> DeleteDraftAsync(int id, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE TravelChecklistTemplates SET IsDeleted = 1, UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0 AND Status = N'Draft'
            """, new { Id = id, TenantId = tenantId }, cancellationToken: cancellationToken)) > 0;
    }

    public async Task<TravelChecklistSnapshotSource?> GetActiveForAsync(int tenantId, string countryCode, int visaTypeId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        // Visa-type-specific checklist wins over the country-wide one.
        var template = await connection.QuerySingleOrDefaultAsync<(int Id, int Version)?>(new CommandDefinition("""
            SELECT TOP 1 t.Id, t.Version
            FROM TravelChecklistTemplates t
            WHERE t.TenantId = @TenantId AND t.IsDeleted = 0 AND t.Status = N'Active' AND t.CountryCode = @CountryCode
              AND (t.VisaTypeId = @VisaTypeId OR t.VisaTypeId IS NULL)
            ORDER BY CASE WHEN t.VisaTypeId IS NULL THEN 1 ELSE 0 END
            """, new { TenantId = tenantId, CountryCode = countryCode, VisaTypeId = visaTypeId }, cancellationToken: cancellationToken));
        if (template is null) return null;

        var items = await GetItemsAsync(template.Value.Id, tenantId, cancellationToken);
        return new TravelChecklistSnapshotSource(template.Value.Id, template.Value.Version, items);
    }
}
