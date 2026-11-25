using Dapper;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Configuration.DTOs;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories.TravelNexus;

/// <summary>TravelNexus configuration. Every tenant table statement filters on <c>TenantId = @TenantId</c>.</summary>
public sealed class TravelConfigRepository(IDbConnectionFactory dbFactory) : ITravelConfigRepository
{
    // ── Countries (platform reference data) ─────────────────────────────────

    public async Task<IReadOnlyList<TravelCountryDto>> GetCountriesAsync(CancellationToken cancellationToken = default)
    {
        // PLATFORM-SCOPE: TravelCountries is shared ISO reference data with no TenantId.
        using var connection = dbFactory.CreateConnection();
        return (await connection.QueryAsync<TravelCountryDto>(new CommandDefinition(
            "SELECT Code, Name FROM TravelCountries WHERE IsActive = 1 ORDER BY Name",
            cancellationToken: cancellationToken))).ToList();
    }

    public async Task<bool> CountryExistsAsync(string code, CancellationToken cancellationToken = default)
    {
        // PLATFORM-SCOPE: TravelCountries is shared ISO reference data with no TenantId.
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM TravelCountries WHERE Code = @Code AND IsActive = 1",
            new { Code = code }, cancellationToken: cancellationToken)) > 0;
    }

    public async Task<TravelLookupsDto> GetLookupsAsync(int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var branches = (await connection.QueryAsync<TravelLookupItemDto>(new CommandDefinition("""
            SELECT Id, Name FROM Branches WHERE TenantId = @TenantId AND IsActive = 1 ORDER BY Name
            """, new { TenantId = tenantId }, cancellationToken: cancellationToken))).ToList();
        var agents = (await connection.QueryAsync<TravelLookupItemDto>(new CommandDefinition("""
            SELECT Id, FullName AS Name FROM Users WHERE TenantId = @TenantId AND IsActive = 1 ORDER BY FullName
            """, new { TenantId = tenantId }, cancellationToken: cancellationToken))).ToList();
        return new TravelLookupsDto { Branches = branches, Agents = agents };
    }

    // ── Visa types ──────────────────────────────────────────────────────────

    private const string VisaTypeSelect = """
        SELECT v.Id, v.CountryCode, c.Name AS CountryName, v.Name, v.Category, v.EntryType,
               v.ValidityDays, v.StayDays, v.ProcessingDays, v.DefaultFee, v.CurrencyCode, v.IsActive, v.Notes
        FROM TravelVisaTypes v
        INNER JOIN TravelCountries c ON c.Code = v.CountryCode
        """;

    public async Task<IReadOnlyList<TravelVisaTypeDto>> GetVisaTypesAsync(
        int tenantId, string? countryCode, bool? isActive, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var clauses = new List<string> { "v.TenantId = @TenantId", "v.IsDeleted = 0" };
        if (!string.IsNullOrWhiteSpace(countryCode)) clauses.Add("v.CountryCode = @CountryCode");
        if (isActive.HasValue) clauses.Add("v.IsActive = @IsActive");
        return (await connection.QueryAsync<TravelVisaTypeDto>(new CommandDefinition($"""
            {VisaTypeSelect}
            WHERE {string.Join(" AND ", clauses)}
            ORDER BY c.Name, v.Name
            """, new { TenantId = tenantId, CountryCode = countryCode, IsActive = isActive },
            cancellationToken: cancellationToken))).ToList();
    }

    public async Task<TravelVisaTypeDto?> GetVisaTypeByIdAsync(int id, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<TravelVisaTypeDto>(new CommandDefinition($"""
            {VisaTypeSelect}
            WHERE v.Id = @Id AND v.TenantId = @TenantId AND v.IsDeleted = 0
            """, new { Id = id, TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    public async Task<bool> VisaTypeNameExistsAsync(
        int tenantId, string countryCode, string name, int? excludeId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT COUNT(1) FROM TravelVisaTypes
            WHERE TenantId = @TenantId AND CountryCode = @CountryCode AND Name = @Name AND IsDeleted = 0
              AND (@ExcludeId IS NULL OR Id <> @ExcludeId)
            """, new { TenantId = tenantId, CountryCode = countryCode, Name = name, ExcludeId = excludeId },
            cancellationToken: cancellationToken)) > 0;
    }

    public async Task<int> CreateVisaTypeAsync(int tenantId, SaveTravelVisaTypeDto dto, int? userId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            INSERT INTO TravelVisaTypes (TenantId, CountryCode, Name, Category, EntryType, ValidityDays, StayDays,
                ProcessingDays, DefaultFee, CurrencyCode, IsActive, Notes, CreatedBy)
            VALUES (@TenantId, @CountryCode, @Name, @Category, @EntryType, @ValidityDays, @StayDays,
                @ProcessingDays, @DefaultFee, @CurrencyCode, @IsActive, @Notes, @UserId);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """, VisaTypeParams(tenantId, dto, userId), cancellationToken: cancellationToken));
    }

    public async Task<bool> UpdateVisaTypeAsync(int id, int tenantId, SaveTravelVisaTypeDto dto, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var p = VisaTypeParams(tenantId, dto, null);
        p.Add("Id", id);
        return await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE TravelVisaTypes SET
                CountryCode = @CountryCode, Name = @Name, Category = @Category, EntryType = @EntryType,
                ValidityDays = @ValidityDays, StayDays = @StayDays, ProcessingDays = @ProcessingDays,
                DefaultFee = @DefaultFee, CurrencyCode = @CurrencyCode, IsActive = @IsActive, Notes = @Notes,
                UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0
            """, p, cancellationToken: cancellationToken)) > 0;
    }

    public Task<bool> DeleteVisaTypeAsync(int id, int tenantId, CancellationToken cancellationToken = default)
        => SoftDeleteAsync("TravelVisaTypes", id, tenantId, cancellationToken);

    private static DynamicParameters VisaTypeParams(int tenantId, SaveTravelVisaTypeDto dto, int? userId)
    {
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        p.Add("CountryCode", dto.CountryCode);
        p.Add("Name", dto.Name);
        p.Add("Category", dto.Category);
        p.Add("EntryType", dto.EntryType);
        p.Add("ValidityDays", dto.ValidityDays);
        p.Add("StayDays", dto.StayDays);
        p.Add("ProcessingDays", dto.ProcessingDays);
        p.Add("DefaultFee", dto.DefaultFee);
        p.Add("CurrencyCode", string.IsNullOrWhiteSpace(dto.CurrencyCode) ? null : dto.CurrencyCode);
        p.Add("IsActive", dto.IsActive);
        p.Add("Notes", dto.Notes);
        p.Add("UserId", userId);
        return p;
    }

    // ── Document types ──────────────────────────────────────────────────────

    private const string DocumentTypeSelect =
        "SELECT Id, Code, Name, Description, RequiresExpiryDate, IsActive FROM TravelDocumentTypes";

    public async Task<IReadOnlyList<TravelDocumentTypeDto>> GetDocumentTypesAsync(
        int tenantId, bool? isActive, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return (await connection.QueryAsync<TravelDocumentTypeDto>(new CommandDefinition($"""
            {DocumentTypeSelect}
            WHERE TenantId = @TenantId AND IsDeleted = 0 AND (@IsActive IS NULL OR IsActive = @IsActive)
            ORDER BY Name
            """, new { TenantId = tenantId, IsActive = isActive }, cancellationToken: cancellationToken))).ToList();
    }

    public async Task<TravelDocumentTypeDto?> GetDocumentTypeByIdAsync(int id, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<TravelDocumentTypeDto>(new CommandDefinition($"""
            {DocumentTypeSelect}
            WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0
            """, new { Id = id, TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    public async Task<bool> DocumentTypeCodeExistsAsync(int tenantId, string code, int? excludeId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT COUNT(1) FROM TravelDocumentTypes
            WHERE TenantId = @TenantId AND Code = @Code AND IsDeleted = 0
              AND (@ExcludeId IS NULL OR Id <> @ExcludeId)
            """, new { TenantId = tenantId, Code = code, ExcludeId = excludeId },
            cancellationToken: cancellationToken)) > 0;
    }

    public async Task<int> CreateDocumentTypeAsync(int tenantId, SaveTravelDocumentTypeDto dto, int? userId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            INSERT INTO TravelDocumentTypes (TenantId, Code, Name, Description, RequiresExpiryDate, IsActive, CreatedBy)
            VALUES (@TenantId, @Code, @Name, @Description, @RequiresExpiryDate, @IsActive, @UserId);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """, new { TenantId = tenantId, dto.Code, dto.Name, dto.Description, dto.RequiresExpiryDate, dto.IsActive, UserId = userId },
            cancellationToken: cancellationToken));
    }

    public async Task<bool> UpdateDocumentTypeAsync(int id, int tenantId, SaveTravelDocumentTypeDto dto, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE TravelDocumentTypes SET
                Code = @Code, Name = @Name, Description = @Description,
                RequiresExpiryDate = @RequiresExpiryDate, IsActive = @IsActive, UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0
            """, new { Id = id, TenantId = tenantId, dto.Code, dto.Name, dto.Description, dto.RequiresExpiryDate, dto.IsActive },
            cancellationToken: cancellationToken)) > 0;
    }

    public Task<bool> DeleteDocumentTypeAsync(int id, int tenantId, CancellationToken cancellationToken = default)
        => SoftDeleteAsync("TravelDocumentTypes", id, tenantId, cancellationToken);

    // ── Lead sources ────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<TravelLeadSourceDto>> GetLeadSourcesAsync(int tenantId, bool? isActive, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return (await connection.QueryAsync<TravelLeadSourceDto>(new CommandDefinition("""
            SELECT Id, Name, IsActive FROM TravelLeadSources
            WHERE TenantId = @TenantId AND IsDeleted = 0 AND (@IsActive IS NULL OR IsActive = @IsActive)
            ORDER BY Name
            """, new { TenantId = tenantId, IsActive = isActive }, cancellationToken: cancellationToken))).ToList();
    }

    public async Task<TravelLeadSourceDto?> GetLeadSourceByIdAsync(int id, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<TravelLeadSourceDto>(new CommandDefinition("""
            SELECT Id, Name, IsActive FROM TravelLeadSources
            WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0
            """, new { Id = id, TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    public async Task<bool> LeadSourceNameExistsAsync(int tenantId, string name, int? excludeId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT COUNT(1) FROM TravelLeadSources
            WHERE TenantId = @TenantId AND Name = @Name AND IsDeleted = 0
              AND (@ExcludeId IS NULL OR Id <> @ExcludeId)
            """, new { TenantId = tenantId, Name = name, ExcludeId = excludeId },
            cancellationToken: cancellationToken)) > 0;
    }

    public async Task<int> CreateLeadSourceAsync(int tenantId, SaveTravelLeadSourceDto dto, int? userId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            INSERT INTO TravelLeadSources (TenantId, Name, IsActive, CreatedBy)
            VALUES (@TenantId, @Name, @IsActive, @UserId);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """, new { TenantId = tenantId, dto.Name, dto.IsActive, UserId = userId }, cancellationToken: cancellationToken));
    }

    public async Task<bool> UpdateLeadSourceAsync(int id, int tenantId, SaveTravelLeadSourceDto dto, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE TravelLeadSources SET Name = @Name, IsActive = @IsActive, UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0
            """, new { Id = id, TenantId = tenantId, dto.Name, dto.IsActive }, cancellationToken: cancellationToken)) > 0;
    }

    public Task<bool> DeleteLeadSourceAsync(int id, int tenantId, CancellationToken cancellationToken = default)
        => SoftDeleteAsync("TravelLeadSources", id, tenantId, cancellationToken);

    // ── Lost reasons ────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<TravelLostReasonDto>> GetLostReasonsAsync(int tenantId, bool? isActive, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return (await connection.QueryAsync<TravelLostReasonDto>(new CommandDefinition("""
            SELECT Id, Name, IsActive FROM TravelLostReasons
            WHERE TenantId = @TenantId AND IsDeleted = 0 AND (@IsActive IS NULL OR IsActive = @IsActive)
            ORDER BY Name
            """, new { TenantId = tenantId, IsActive = isActive }, cancellationToken: cancellationToken))).ToList();
    }

    public async Task<TravelLostReasonDto?> GetLostReasonByIdAsync(int id, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<TravelLostReasonDto>(new CommandDefinition("""
            SELECT Id, Name, IsActive FROM TravelLostReasons
            WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0
            """, new { Id = id, TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    public async Task<bool> LostReasonNameExistsAsync(int tenantId, string name, int? excludeId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT COUNT(1) FROM TravelLostReasons
            WHERE TenantId = @TenantId AND Name = @Name AND IsDeleted = 0
              AND (@ExcludeId IS NULL OR Id <> @ExcludeId)
            """, new { TenantId = tenantId, Name = name, ExcludeId = excludeId },
            cancellationToken: cancellationToken)) > 0;
    }

    public async Task<int> CreateLostReasonAsync(int tenantId, SaveTravelLostReasonDto dto, int? userId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            INSERT INTO TravelLostReasons (TenantId, Name, IsActive, CreatedBy)
            VALUES (@TenantId, @Name, @IsActive, @UserId);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """, new { TenantId = tenantId, dto.Name, dto.IsActive, UserId = userId }, cancellationToken: cancellationToken));
    }

    public async Task<bool> UpdateLostReasonAsync(int id, int tenantId, SaveTravelLostReasonDto dto, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE TravelLostReasons SET Name = @Name, IsActive = @IsActive, UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0
            """, new { Id = id, TenantId = tenantId, dto.Name, dto.IsActive }, cancellationToken: cancellationToken)) > 0;
    }

    public Task<bool> DeleteLostReasonAsync(int id, int tenantId, CancellationToken cancellationToken = default)
        => SoftDeleteAsync("TravelLostReasons", id, tenantId, cancellationToken);

    // ── Settings ────────────────────────────────────────────────────────────

    public async Task<TravelSettingsDto> GetSettingsAsync(int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<TravelSettingsDto>(new CommandDefinition("""
            SELECT DefaultCurrencyCode, EnquiryPrefix, VisaCasePrefix, InvoicePrefix, PassportExpiryWarningDays
            FROM TravelSettings WHERE TenantId = @TenantId
            """, new { TenantId = tenantId }, cancellationToken: cancellationToken))
            ?? new TravelSettingsDto();
    }

    public async Task UpsertSettingsAsync(int tenantId, UpdateTravelSettingsDto dto, int? userId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE TravelSettings SET
                DefaultCurrencyCode = @DefaultCurrencyCode, EnquiryPrefix = @EnquiryPrefix,
                VisaCasePrefix = @VisaCasePrefix, InvoicePrefix = @InvoicePrefix,
                PassportExpiryWarningDays = @PassportExpiryWarningDays,
                UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @UserId
            WHERE TenantId = @TenantId;

            IF @@ROWCOUNT = 0
                INSERT INTO TravelSettings (TenantId, DefaultCurrencyCode, EnquiryPrefix, VisaCasePrefix, InvoicePrefix,
                    PassportExpiryWarningDays, UpdatedAt, UpdatedBy)
                VALUES (@TenantId, @DefaultCurrencyCode, @EnquiryPrefix, @VisaCasePrefix, @InvoicePrefix,
                    @PassportExpiryWarningDays, SYSUTCDATETIME(), @UserId);
            """,
            new
            {
                TenantId = tenantId,
                dto.DefaultCurrencyCode,
                dto.EnquiryPrefix,
                dto.VisaCasePrefix,
                dto.InvoicePrefix,
                dto.PassportExpiryWarningDays,
                UserId = userId
            },
            cancellationToken: cancellationToken));
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private async Task<bool> SoftDeleteAsync(string table, int id, int tenantId, CancellationToken cancellationToken)
    {
        // `table` is always one of the constant names above, never user input.
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition($"""
            UPDATE {table} SET IsDeleted = 1, UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0
            """, new { Id = id, TenantId = tenantId }, cancellationToken: cancellationToken)) > 0;
    }
}
