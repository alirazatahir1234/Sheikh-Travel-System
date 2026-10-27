using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

public static class DriverProviderVerificationMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'DriverProviderVerificationChecks')
            CREATE TABLE DriverProviderVerificationChecks (
                Id                 INT IDENTITY(1,1) PRIMARY KEY,
                TenantId           INT NOT NULL,
                DriverId           INT NOT NULL,
                Provider           NVARCHAR(40)  NOT NULL,
                DocumentType       NVARCHAR(60)  NOT NULL,
                Status             NVARCHAR(30)  NOT NULL,
                Summary            NVARCHAR(500) NOT NULL,
                ExternalReference  NVARCHAR(120) NULL,
                HttpStatusCode     INT NULL,
                MissingConfigKeys  NVARCHAR(500) NULL,
                CheckedAt          DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
                IsDeleted          BIT           NOT NULL DEFAULT 0,
                CONSTRAINT FK_DriverProviderChecks_Drivers FOREIGN KEY (DriverId) REFERENCES Drivers(Id)
            );
            """, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DriverProviderChecks_Driver')
                CREATE INDEX IX_DriverProviderChecks_Driver
                ON DriverProviderVerificationChecks (TenantId, DriverId, CheckedAt DESC)
                WHERE IsDeleted = 0;
            """, cancellationToken: cancellationToken));

        logger.LogInformation("Driver provider verification migration completed.");
    }
}
