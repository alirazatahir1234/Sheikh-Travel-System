using System.Data;
using Dapper;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Packages.DTOs;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories.TravelNexus;

/// <summary>Packages with stays and prices. Root predicate <c>p.TenantId = @TenantId</c>; child writes in one transaction.</summary>
public sealed class TravelPackageRepository(IDbConnectionFactory dbFactory) : ITravelPackageRepository
{
    private const string ListSelect = """
        SELECT p.Id, p.Code, p.Name, p.PackageType, p.DurationNights, p.NightsMakkah, p.NightsMadinah, p.VisaTypeId, vt.Name AS VisaTypeName,
               p.CurrencyCode, p.Status,
               (SELECT MIN(pr.AdultPrice) FROM TravelPackagePrices pr WHERE pr.TenantId = p.TenantId AND pr.PackageId = p.Id) AS FromPrice,
               (SELECT COUNT(1) FROM TravelGroups g WHERE g.TenantId = p.TenantId AND g.PackageId = p.Id AND g.IsDeleted = 0) AS GroupCount,
               p.CreatedAt
        FROM TravelPackages p
        LEFT JOIN TravelVisaTypes vt ON vt.Id = p.VisaTypeId AND vt.TenantId = p.TenantId
        """;

    public async Task<IReadOnlyList<TravelPackageListItemDto>> GetListAsync(int tenantId, string? status, string? packageType, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return (await connection.QueryAsync<TravelPackageListItemDto>(new CommandDefinition($"""
            {ListSelect}
            WHERE p.TenantId = @TenantId AND p.IsDeleted = 0
              AND (@Status IS NULL OR p.Status = @Status) AND (@PackageType IS NULL OR p.PackageType = @PackageType)
            ORDER BY CASE p.Status WHEN N'Published' THEN 0 WHEN N'Draft' THEN 1 ELSE 2 END, p.Name
            """, new { TenantId = tenantId, Status = status, PackageType = packageType }, cancellationToken: cancellationToken))).ToList();
    }

    public async Task<TravelPackageDto?> GetByIdAsync(int id, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var package = await connection.QuerySingleOrDefaultAsync<TravelPackageDto>(new CommandDefinition($"""
            SELECT x.*, p.Inclusions, p.Exclusions FROM ({ListSelect}
                WHERE p.Id = @Id AND p.TenantId = @TenantId AND p.IsDeleted = 0) x
            INNER JOIN TravelPackages p ON p.Id = x.Id AND p.TenantId = @TenantId
            """, new { Id = id, TenantId = tenantId }, cancellationToken: cancellationToken));
        if (package is null) return null;

        package.Stays = (await connection.QueryAsync<TravelPackageStayDto>(new CommandDefinition("""
            SELECT s.HotelId, h.Name AS HotelName, h.City, s.Nights, s.Sequence
            FROM TravelPackageHotels s
            INNER JOIN TravelHotels h ON h.Id = s.HotelId AND h.TenantId = s.TenantId
            WHERE s.PackageId = @Id AND s.TenantId = @TenantId
            ORDER BY s.Sequence
            """, new { Id = id, TenantId = tenantId }, cancellationToken: cancellationToken))).ToList();
        package.Prices = (await connection.QueryAsync<TravelPackagePriceDto>(new CommandDefinition("""
            SELECT RoomType, AdultPrice, ChildPrice, InfantPrice FROM TravelPackagePrices
            WHERE PackageId = @Id AND TenantId = @TenantId
            ORDER BY CASE RoomType WHEN N'Quad' THEN 0 WHEN N'Triple' THEN 1 WHEN N'Double' THEN 2 ELSE 3 END
            """, new { Id = id, TenantId = tenantId }, cancellationToken: cancellationToken))).ToList();
        return package;
    }

    public async Task<bool> CodeExistsAsync(int tenantId, string code, int? excludeId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT COUNT(1) FROM TravelPackages WHERE TenantId = @TenantId AND Code = @Code AND IsDeleted = 0 AND (@ExcludeId IS NULL OR Id <> @ExcludeId)
            """, new { TenantId = tenantId, Code = code, ExcludeId = excludeId }, cancellationToken: cancellationToken)) > 0;
    }

    public async Task<int> CreateAsync(int tenantId, SaveTravelPackageDto dto, int? userId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();
        var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            INSERT INTO TravelPackages (TenantId, Code, Name, PackageType, DurationNights, NightsMakkah, NightsMadinah, VisaTypeId,
                Inclusions, Exclusions, CurrencyCode, Status, CreatedBy)
            VALUES (@TenantId, @Code, @Name, @PackageType, @DurationNights, @NightsMakkah, @NightsMadinah, @VisaTypeId,
                @Inclusions, @Exclusions, @CurrencyCode, N'Draft', @UserId);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """, Header(tenantId, dto, userId), tx, cancellationToken: cancellationToken));
        await WriteChildrenAsync(connection, tx, tenantId, id, dto, cancellationToken);
        tx.Commit();
        return id;
    }

    public async Task<bool> UpdateAsync(int id, int tenantId, SaveTravelPackageDto dto, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();
        var p = Header(tenantId, dto, null);
        p.Add("Id", id);
        var rows = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE TravelPackages SET Code = @Code, Name = @Name, PackageType = @PackageType, DurationNights = @DurationNights,
                NightsMakkah = @NightsMakkah, NightsMadinah = @NightsMadinah, VisaTypeId = @VisaTypeId, Inclusions = @Inclusions,
                Exclusions = @Exclusions, CurrencyCode = @CurrencyCode, UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0 AND Status <> N'Archived'
            """, p, tx, cancellationToken: cancellationToken));
        if (rows == 0)
        {
            tx.Rollback();
            return false;
        }
        await connection.ExecuteAsync(new CommandDefinition("""
            DELETE FROM TravelPackageHotels WHERE PackageId = @Id AND TenantId = @TenantId;
            DELETE FROM TravelPackagePrices WHERE PackageId = @Id AND TenantId = @TenantId;
            """, new { Id = id, TenantId = tenantId }, tx, cancellationToken: cancellationToken));
        await WriteChildrenAsync(connection, tx, tenantId, id, dto, cancellationToken);
        tx.Commit();
        return true;
    }

    public async Task<bool> SetStatusAsync(int id, int tenantId, string expectedStatus, string newStatus, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE TravelPackages SET Status = @NewStatus, UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0 AND Status = @Expected
            """, new { Id = id, TenantId = tenantId, Expected = expectedStatus, NewStatus = newStatus }, cancellationToken: cancellationToken)) > 0;
    }

    public async Task<bool> DeleteDraftAsync(int id, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE TravelPackages SET IsDeleted = 1, UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0 AND Status = N'Draft'
              AND NOT EXISTS (SELECT 1 FROM TravelGroups g WHERE g.TenantId = @TenantId AND g.PackageId = @Id AND g.IsDeleted = 0)
            """, new { Id = id, TenantId = tenantId }, cancellationToken: cancellationToken)) > 0;
    }

    private static DynamicParameters Header(int tenantId, SaveTravelPackageDto dto, int? userId)
    {
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        p.Add("Code", dto.Code);
        p.Add("Name", dto.Name);
        p.Add("PackageType", dto.PackageType);
        p.Add("DurationNights", dto.DurationNights);
        p.Add("NightsMakkah", dto.NightsMakkah);
        p.Add("NightsMadinah", dto.NightsMadinah);
        p.Add("VisaTypeId", dto.VisaTypeId);
        p.Add("Inclusions", string.IsNullOrWhiteSpace(dto.Inclusions) ? null : dto.Inclusions.Trim());
        p.Add("Exclusions", string.IsNullOrWhiteSpace(dto.Exclusions) ? null : dto.Exclusions.Trim());
        p.Add("CurrencyCode", dto.CurrencyCode);
        p.Add("UserId", userId);
        return p;
    }

    private static async Task WriteChildrenAsync(IDbConnection connection, IDbTransaction tx, int tenantId, int packageId, SaveTravelPackageDto dto, CancellationToken ct)
    {
        for (var i = 0; i < dto.Stays.Count; i++)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO TravelPackageHotels (TenantId, PackageId, HotelId, Nights, Sequence)
                SELECT @TenantId, @PackageId, h.Id, @Nights, @Sequence FROM TravelHotels h WHERE h.Id = @HotelId AND h.TenantId = @TenantId
                """, new { TenantId = tenantId, PackageId = packageId, dto.Stays[i].HotelId, dto.Stays[i].Nights, Sequence = i + 1 }, tx, cancellationToken: ct));
        }
        foreach (var price in dto.Prices)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO TravelPackagePrices (TenantId, PackageId, RoomType, AdultPrice, ChildPrice, InfantPrice)
                VALUES (@TenantId, @PackageId, @RoomType, @AdultPrice, @ChildPrice, @InfantPrice)
                """, new { TenantId = tenantId, PackageId = packageId, price.RoomType, price.AdultPrice, price.ChildPrice, price.InfantPrice }, tx, cancellationToken: ct));
        }
    }
}
