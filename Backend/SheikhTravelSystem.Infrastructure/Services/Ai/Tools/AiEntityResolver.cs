using System.Text.Json;
using Dapper;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Services.Ai.Tools;

/// <summary>Resolves ERP entity names to IDs for AI tool args.</summary>
public sealed class AiEntityResolver(IDbConnectionFactory dbFactory)
{
    public async Task<int?> ResolveDriverIdAsync(int tenantId, string nameOrFragment, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(nameOrFragment)) return null;
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int?>(new CommandDefinition("""
            SELECT TOP 1 Id FROM Drivers
            WHERE IsDeleted = 0 AND TenantId = @TenantId AND IsActive = 1
              AND (FullName LIKE '%' + @Name + '%'
                   OR FirstName LIKE '%' + @Name + '%'
                   OR LastName LIKE '%' + @Name + '%'
                   OR DriverCode LIKE '%' + @Name + '%')
            ORDER BY FullName
            """, new { TenantId = tenantId, Name = nameOrFragment.Trim() }, cancellationToken: ct));
    }

    public async Task<int?> ResolveVehicleIdByPlateAsync(int tenantId, string plate, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(plate)) return null;
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int?>(new CommandDefinition("""
            SELECT TOP 1 Id FROM Vehicles
            WHERE IsDeleted = 0 AND TenantId = @TenantId
              AND PlateNumber LIKE '%' + @Plate + '%'
            ORDER BY PlateNumber
            """, new { TenantId = tenantId, Plate = plate.Trim() }, cancellationToken: ct));
    }

    public async Task<(string? DriverName, string? BookingRef)> GetAssignmentLabelsAsync(
        int tenantId, int bookingId, int driverId, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        var driverName = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT FullName FROM Drivers WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
            new { Id = driverId, TenantId = tenantId }, cancellationToken: ct));
        var bookingNum = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT BookingNumber FROM Bookings WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
            new { Id = bookingId, TenantId = tenantId }, cancellationToken: ct));
        return (driverName, bookingNum ?? $"#{bookingId}");
    }
}
