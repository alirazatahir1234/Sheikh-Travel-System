using Dapper;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Features.Drivers.DTOs;

namespace SheikhTravelSystem.Application.Features.Drivers;

internal static class DriverUniquenessHelper
{
    internal static async Task<DriverAvailabilityDto> CheckAvailabilityAsync(
        System.Data.IDbConnection connection,
        int tenantId,
        string? phone,
        string? email,
        string? licenseNumber,
        int? excludeId,
        CancellationToken cancellationToken)
    {
        var phoneAvailable = string.IsNullOrWhiteSpace(phone)
            || !await ExistsAsync(connection, tenantId, "Phone", phone.Trim(), excludeId, cancellationToken);

        var emailAvailable = string.IsNullOrWhiteSpace(email)
            || !await ExistsAsync(connection, tenantId, "Email", email.Trim(), excludeId, cancellationToken);

        var licenseAvailable = string.IsNullOrWhiteSpace(licenseNumber)
            || !await ExistsAsync(connection, tenantId, "LicenseNumber", licenseNumber.Trim(), excludeId, cancellationToken);

        return new DriverAvailabilityDto(phoneAvailable, emailAvailable, licenseAvailable);
    }

    internal static async Task EnsureUniqueAsync(
        System.Data.IDbConnection connection,
        int tenantId,
        string phone,
        string? email,
        string licenseNumber,
        int? excludeId,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(licenseNumber)
            && await ExistsAsync(connection, tenantId, "LicenseNumber", licenseNumber.Trim(), excludeId, cancellationToken))
        {
            throw new ConflictException($"Driver with license '{licenseNumber}' already exists.");
        }

        if (await ExistsAsync(connection, tenantId, "Phone", phone.Trim(), excludeId, cancellationToken))
            throw new ConflictException($"A driver with mobile number '{phone}' already exists.");

        if (!string.IsNullOrWhiteSpace(email)
            && await ExistsAsync(connection, tenantId, "Email", email.Trim(), excludeId, cancellationToken))
        {
            throw new ConflictException($"A driver with email '{email}' already exists.");
        }
    }

    private static async Task<bool> ExistsAsync(
        System.Data.IDbConnection connection,
        int tenantId,
        string column,
        string value,
        int? excludeId,
        CancellationToken cancellationToken)
    {
        // Column names are fixed constants — never user input.
        var sql = column switch
        {
            "Phone" => """
                SELECT CASE WHEN EXISTS(
                    SELECT 1 FROM Drivers WHERE Phone = @Value AND IsDeleted = 0 AND TenantId = @TenantId
                      AND (@ExcludeId IS NULL OR Id != @ExcludeId)
                  ) THEN 1 ELSE 0 END
                """,
            "Email" => """
                SELECT CASE WHEN EXISTS(
                    SELECT 1 FROM Drivers WHERE Email = @Value AND IsDeleted = 0 AND TenantId = @TenantId
                      AND (@ExcludeId IS NULL OR Id != @ExcludeId)
                  ) THEN 1 ELSE 0 END
                """,
            "LicenseNumber" => """
                SELECT CASE WHEN EXISTS(
                    SELECT 1 FROM Drivers WHERE LicenseNumber = @Value AND IsDeleted = 0 AND TenantId = @TenantId
                      AND (@ExcludeId IS NULL OR Id != @ExcludeId)
                  ) THEN 1 ELSE 0 END
                """,
            _ => throw new ArgumentOutOfRangeException(nameof(column), column, "Unsupported uniqueness column.")
        };

        return await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(sql, new { Value = value, TenantId = tenantId, ExcludeId = excludeId },
                cancellationToken: cancellationToken));
    }
}
