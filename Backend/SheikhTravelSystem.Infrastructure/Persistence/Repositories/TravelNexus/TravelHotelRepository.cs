using Dapper;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Hotels.DTOs;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories.TravelNexus;

/// <summary>Hotel catalog. Every statement filters on <c>TenantId = @TenantId</c>.</summary>
public sealed class TravelHotelRepository(IDbConnectionFactory dbFactory) : ITravelHotelRepository
{
    private const string Select = """
        SELECT Id, Name, City, CountryCode, StarRating, DistanceToHaramMeters, Address, Phone, IsActive FROM TravelHotels
        """;

    public async Task<IReadOnlyList<TravelHotelDto>> GetListAsync(int tenantId, string? city, bool? isActive, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return (await connection.QueryAsync<TravelHotelDto>(new CommandDefinition($"""
            {Select}
            WHERE TenantId = @TenantId AND IsDeleted = 0 AND (@City IS NULL OR City = @City) AND (@IsActive IS NULL OR IsActive = @IsActive)
            ORDER BY City, Name
            """, new { TenantId = tenantId, City = city, IsActive = isActive }, cancellationToken: cancellationToken))).ToList();
    }

    public async Task<TravelHotelDto?> GetByIdAsync(int id, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<TravelHotelDto>(new CommandDefinition($"""
            {Select}
            WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0
            """, new { Id = id, TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    public async Task<bool> NameExistsAsync(int tenantId, string name, string city, int? excludeId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT COUNT(1) FROM TravelHotels
            WHERE TenantId = @TenantId AND Name = @Name AND City = @City AND IsDeleted = 0 AND (@ExcludeId IS NULL OR Id <> @ExcludeId)
            """, new { TenantId = tenantId, Name = name, City = city, ExcludeId = excludeId }, cancellationToken: cancellationToken)) > 0;
    }

    public async Task<int> CreateAsync(int tenantId, SaveTravelHotelDto dto, int? userId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            INSERT INTO TravelHotels (TenantId, Name, City, CountryCode, StarRating, DistanceToHaramMeters, Address, Phone, IsActive, CreatedBy)
            VALUES (@TenantId, @Name, @City, @CountryCode, @StarRating, @DistanceToHaramMeters, @Address, @Phone, @IsActive, @UserId);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """, new { TenantId = tenantId, dto.Name, dto.City, dto.CountryCode, dto.StarRating, dto.DistanceToHaramMeters, dto.Address, dto.Phone, dto.IsActive, UserId = userId },
            cancellationToken: cancellationToken));
    }

    public async Task<bool> UpdateAsync(int id, int tenantId, SaveTravelHotelDto dto, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE TravelHotels SET Name = @Name, City = @City, CountryCode = @CountryCode, StarRating = @StarRating,
                DistanceToHaramMeters = @DistanceToHaramMeters, Address = @Address, Phone = @Phone, IsActive = @IsActive, UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0
            """, new { Id = id, TenantId = tenantId, dto.Name, dto.City, dto.CountryCode, dto.StarRating, dto.DistanceToHaramMeters, dto.Address, dto.Phone, dto.IsActive },
            cancellationToken: cancellationToken)) > 0;
    }

    public async Task<bool> DeleteAsync(int id, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE TravelHotels SET IsDeleted = 1, UpdatedAt = SYSUTCDATETIME() WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0
            """, new { Id = id, TenantId = tenantId }, cancellationToken: cancellationToken)) > 0;
    }
}
