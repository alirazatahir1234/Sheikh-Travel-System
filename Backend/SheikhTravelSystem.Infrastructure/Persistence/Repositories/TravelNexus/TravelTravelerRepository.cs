using Dapper;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Travelers.DTOs;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories.TravelNexus;

/// <summary>
/// Travelers. Root predicate <c>t.TenantId = @TenantId</c>; travelers of archived customer profiles are hidden.
/// Identity values (passport, CNIC) are returned raw — masking happens in the application layer.
/// </summary>
public sealed class TravelTravelerRepository(IDbConnectionFactory dbFactory) : ITravelTravelerRepository
{
    private const string Select = """
        SELECT t.Id, t.CustomerId, t.Title, t.GivenNames, t.Surname, t.Gender, t.DateOfBirth, t.PlaceOfBirth,
               t.NationalityCode, nat.Name AS NationalityName,
               t.PassportNumber, t.PassportIssuingCountryCode, iss.Name AS PassportIssuingCountryName,
               t.PassportIssueDate, t.PassportExpiryDate, t.Cnic, t.Phone, t.Email,
               t.MahramTravelerId, CONCAT(m.GivenNames, ' ', m.Surname) AS MahramName, t.MahramRelation,
               t.Notes, t.CreatedAt
        FROM TravelTravelers t
        INNER JOIN TravelCustomerProfiles p ON p.CustomerId = t.CustomerId AND p.TenantId = t.TenantId AND p.IsDeleted = 0
        LEFT JOIN TravelCountries nat ON nat.Code = t.NationalityCode
        LEFT JOIN TravelCountries iss ON iss.Code = t.PassportIssuingCountryCode
        LEFT JOIN TravelTravelers m ON m.Id = t.MahramTravelerId AND m.TenantId = t.TenantId AND m.IsDeleted = 0
        """;

    public async Task<IReadOnlyList<TravelTravelerDto>> GetByCustomerAsync(int customerId, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return (await connection.QueryAsync<TravelTravelerDto>(new CommandDefinition($"""
            {Select}
            WHERE t.CustomerId = @CustomerId AND t.TenantId = @TenantId AND t.IsDeleted = 0
            ORDER BY t.CreatedAt, t.Id
            """, new { CustomerId = customerId, TenantId = tenantId }, cancellationToken: cancellationToken))).ToList();
    }

    public async Task<TravelTravelerDto?> GetByIdAsync(int id, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<TravelTravelerDto>(new CommandDefinition($"""
            {Select}
            WHERE t.Id = @Id AND t.TenantId = @TenantId AND t.IsDeleted = 0
            """, new { Id = id, TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<TravelTravelerDto>> GetByIdsAsync(IReadOnlyList<int> ids, int tenantId, CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0) return [];
        using var connection = dbFactory.CreateConnection();
        return (await connection.QueryAsync<TravelTravelerDto>(new CommandDefinition($$"""
            {{Select}}
            WHERE t.Id IN @Ids AND t.TenantId = @TenantId AND t.IsDeleted = 0
            """, new { Ids = ids.ToArray(), TenantId = tenantId }, cancellationToken: cancellationToken))).ToList();
    }

    public async Task<TravelTravelerIdentity?> GetIdentityAsync(int id, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<TravelTravelerIdentity>(new CommandDefinition("""
            SELECT t.Id, t.CustomerId, t.Gender, t.PassportNumber, t.Cnic
            FROM TravelTravelers t
            INNER JOIN TravelCustomerProfiles p ON p.CustomerId = t.CustomerId AND p.TenantId = t.TenantId AND p.IsDeleted = 0
            WHERE t.Id = @Id AND t.TenantId = @TenantId AND t.IsDeleted = 0
            """, new { Id = id, TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    public async Task<bool> PassportExistsAsync(
        int tenantId, string issuingCountryCode, string passportNumber, int? excludeId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT COUNT(1) FROM TravelTravelers
            WHERE TenantId = @TenantId AND IsDeleted = 0
              AND PassportIssuingCountryCode = @Country AND PassportNumber = @Passport
              AND (@ExcludeId IS NULL OR Id <> @ExcludeId)
            """, new { TenantId = tenantId, Country = issuingCountryCode, Passport = passportNumber, ExcludeId = excludeId },
            cancellationToken: cancellationToken)) > 0;
    }

    public async Task<int> CreateAsync(int tenantId, int customerId, SaveTravelTravelerDto dto, int? userId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var p = Params(tenantId, dto);
        p.Add("CustomerId", customerId);
        p.Add("UserId", userId);
        // INSERT … SELECT guarantees the customer is an active travel customer of this tenant.
        return await connection.ExecuteScalarAsync<int?>(new CommandDefinition("""
            INSERT INTO TravelTravelers (TenantId, CustomerId, Title, GivenNames, Surname, Gender, DateOfBirth, PlaceOfBirth,
                NationalityCode, PassportNumber, PassportIssuingCountryCode, PassportIssueDate, PassportExpiryDate,
                Cnic, Phone, Email, MahramTravelerId, MahramRelation, Notes, CreatedBy)
            SELECT @TenantId, @CustomerId, @Title, @GivenNames, @Surname, @Gender, @DateOfBirth, @PlaceOfBirth,
                @NationalityCode, @PassportNumber, @PassportIssuingCountryCode, @PassportIssueDate, @PassportExpiryDate,
                @Cnic, @Phone, @Email, @MahramTravelerId, @MahramRelation, @Notes, @UserId
            FROM TravelCustomerProfiles p
            WHERE p.CustomerId = @CustomerId AND p.TenantId = @TenantId AND p.IsDeleted = 0;
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """, p, cancellationToken: cancellationToken)) ?? 0;
    }

    public async Task<bool> UpdateAsync(int id, int tenantId, SaveTravelTravelerDto dto, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var p = Params(tenantId, dto);
        p.Add("Id", id);
        return await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE TravelTravelers SET
                Title = @Title, GivenNames = @GivenNames, Surname = @Surname, Gender = @Gender,
                DateOfBirth = @DateOfBirth, PlaceOfBirth = @PlaceOfBirth, NationalityCode = @NationalityCode,
                PassportNumber = @PassportNumber, PassportIssuingCountryCode = @PassportIssuingCountryCode,
                PassportIssueDate = @PassportIssueDate, PassportExpiryDate = @PassportExpiryDate,
                Cnic = @Cnic, Phone = @Phone, Email = @Email,
                MahramTravelerId = @MahramTravelerId, MahramRelation = @MahramRelation, Notes = @Notes,
                UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0
            """, p, cancellationToken: cancellationToken)) > 0;
    }

    public async Task<bool> DeleteAsync(int id, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();

        var rows = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE TravelTravelers SET IsDeleted = 1, UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0
            """, new { Id = id, TenantId = tenantId }, tx, cancellationToken: cancellationToken));
        if (rows == 0)
        {
            tx.Rollback();
            return false;
        }

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE TravelTravelers SET MahramTravelerId = NULL, MahramRelation = NULL, UpdatedAt = SYSUTCDATETIME()
            WHERE MahramTravelerId = @Id AND TenantId = @TenantId
            """, new { Id = id, TenantId = tenantId }, tx, cancellationToken: cancellationToken));

        tx.Commit();
        return true;
    }

    private static DynamicParameters Params(int tenantId, SaveTravelTravelerDto dto)
    {
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        p.Add("Title", dto.Title);
        p.Add("GivenNames", dto.GivenNames);
        p.Add("Surname", dto.Surname);
        p.Add("Gender", dto.Gender);
        p.Add("DateOfBirth", dto.DateOfBirth);
        p.Add("PlaceOfBirth", dto.PlaceOfBirth);
        p.Add("NationalityCode", dto.NationalityCode);
        p.Add("PassportNumber", dto.PassportNumber);
        p.Add("PassportIssuingCountryCode", dto.PassportIssuingCountryCode);
        p.Add("PassportIssueDate", dto.PassportIssueDate);
        p.Add("PassportExpiryDate", dto.PassportExpiryDate);
        p.Add("Cnic", dto.Cnic);
        p.Add("Phone", dto.Phone);
        p.Add("Email", dto.Email);
        p.Add("MahramTravelerId", dto.MahramTravelerId);
        p.Add("MahramRelation", dto.MahramRelation);
        p.Add("Notes", dto.Notes);
        return p;
    }
}
