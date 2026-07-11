using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

public static class VehicleDocumentOcrMigration
{
    public static async Task ApplyAsync(IDbConnectionFactory dbFactory, ILogger logger, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        try
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF COL_LENGTH('VehicleDocuments', 'VerificationStatus') IS NULL
                    ALTER TABLE VehicleDocuments ADD VerificationStatus NVARCHAR(30) NULL;
                IF COL_LENGTH('VehicleDocuments', 'ConfidenceScore') IS NULL
                    ALTER TABLE VehicleDocuments ADD ConfidenceScore INT NULL;
                IF COL_LENGTH('VehicleDocuments', 'OcrJson') IS NULL
                    ALTER TABLE VehicleDocuments ADD OcrJson NVARCHAR(MAX) NULL;
                IF COL_LENGTH('VehicleDocuments', 'VerifiedAt') IS NULL
                    ALTER TABLE VehicleDocuments ADD VerifiedAt DATETIME2 NULL;
                """, cancellationToken: cancellationToken));

            logger.LogInformation("VehicleDocumentOcrMigration applied successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "VehicleDocumentOcrMigration failed.");
        }
    }
}
