using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Driver inspection: SignatureUrl column + ensure mirrors/fuel checklist keys.
/// </summary>
public static class DriverInspectionMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        await connection.ExecuteAsync(new CommandDefinition("""
            IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Inspections')
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                               WHERE TABLE_NAME = 'Inspections' AND COLUMN_NAME = 'SignatureUrl')
                    ALTER TABLE Inspections ADD SignatureUrl NVARCHAR(1000) NULL;

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Inspections_Driver_Date')
                    CREATE INDEX IX_Inspections_Driver_Date ON Inspections (DriverId, InspectionDate DESC)
                    WHERE IsDeleted = 0 AND DriverId IS NOT NULL;
            END
            """, cancellationToken: cancellationToken));

        // Ensure Standard template includes mirrors + fuel (ERP seed may predate Phase E).
        var checklist = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            @"SELECT TOP 1 ChecklistJson FROM InspectionTemplates
              WHERE IsDeleted = 0 AND IsActive = 1
              ORDER BY CASE WHEN Name LIKE N'%Standard%' THEN 0 ELSE 1 END, Id",
            cancellationToken: cancellationToken));

        if (!string.IsNullOrWhiteSpace(checklist) &&
            (!checklist.Contains("\"mirrors\"", StringComparison.OrdinalIgnoreCase) ||
             !checklist.Contains("\"fuel\"", StringComparison.OrdinalIgnoreCase)))
        {
            const string enriched = """
                [
                  {"key":"tyres","label":"Tyres & Wheels","required":true},
                  {"key":"brakes","label":"Brakes","required":true},
                  {"key":"mirrors","label":"Mirrors","required":true},
                  {"key":"lights","label":"Lights & Indicators","required":true},
                  {"key":"engine","label":"Engine & Oil","required":true},
                  {"key":"fuel","label":"Fuel Level","required":true},
                  {"key":"body","label":"Body & Paint","required":false},
                  {"key":"interior","label":"Interior & Seats","required":false},
                  {"key":"documents","label":"Documents Present","required":true},
                  {"key":"firstaid","label":"First Aid & Safety Kit","required":true}
                ]
                """;

            await connection.ExecuteAsync(new CommandDefinition(
                @"UPDATE InspectionTemplates SET ChecklistJson = @Checklist
                  WHERE IsDeleted = 0 AND IsActive = 1
                    AND (Name LIKE N'%Standard%' OR Id = (
                        SELECT TOP 1 Id FROM InspectionTemplates WHERE IsDeleted = 0 AND IsActive = 1 ORDER BY Id
                    ))",
                new { Checklist = enriched },
                cancellationToken: cancellationToken));
        }

        logger.LogInformation("Driver inspection migration completed.");
    }
}
