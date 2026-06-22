using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Adds the DriverReviewNotes table for per-driver reviewer notes and adds
/// a RejectionReason column to ComplianceDocuments for per-document rejections.
/// </summary>
public static class DriverVerificationMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        await AddRejectionReasonColumnAsync(connection, cancellationToken);
        await CreateDriverReviewNotesTableAsync(connection, cancellationToken);

        logger.LogInformation("Driver verification migration completed.");
    }

    private static async Task AddRejectionReasonColumnAsync(
        System.Data.IDbConnection connection, CancellationToken ct)
    {
        // Add RejectionReason to ComplianceDocuments (idempotent)
        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (
                SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_NAME = 'ComplianceDocuments' AND COLUMN_NAME = 'RejectionReason'
            )
            ALTER TABLE ComplianceDocuments ADD RejectionReason NVARCHAR(500) NULL;
            """, cancellationToken: ct));
    }

    private static async Task CreateDriverReviewNotesTableAsync(
        System.Data.IDbConnection connection, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'DriverReviewNotes')
            CREATE TABLE DriverReviewNotes (
                Id           INT IDENTITY(1,1) PRIMARY KEY,
                TenantId     INT NOT NULL,
                DriverId     INT NOT NULL,
                Note         NVARCHAR(1000) NOT NULL,
                DocumentType NVARCHAR(60)   NULL,
                CreatedBy    NVARCHAR(100)  NULL,
                CreatedAt    DATETIME2      NOT NULL DEFAULT GETUTCDATE(),
                IsDeleted    BIT            NOT NULL DEFAULT 0,
                CONSTRAINT FK_DriverReviewNotes_Drivers FOREIGN KEY (DriverId) REFERENCES Drivers(Id)
            );
            """, cancellationToken: ct));

        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DriverReviewNotes_Driver')
                CREATE INDEX IX_DriverReviewNotes_Driver
                ON DriverReviewNotes (DriverId, CreatedAt DESC)
                WHERE IsDeleted = 0;
            """, cancellationToken: ct));
    }
}
