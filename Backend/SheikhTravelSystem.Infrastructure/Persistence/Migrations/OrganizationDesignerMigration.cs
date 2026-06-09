using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Adds Department-to-Branch relationship and indexes for Organization Designer.
/// </summary>
public static class OrganizationDesignerMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        await AddDepartmentBranchIdAsync(connection, cancellationToken);
        await CreateDepartmentBranchIndexAsync(connection, cancellationToken);

        logger.LogInformation("Organization Designer migration completed.");
    }

    private static async Task AddDepartmentBranchIdAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        var exists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Departments' AND COLUMN_NAME = 'BranchId'",
            cancellationToken: ct));

        if (exists == 0)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "ALTER TABLE Departments ADD BranchId INT NULL",
                cancellationToken: ct));

            await connection.ExecuteAsync(new CommandDefinition("""
                ALTER TABLE Departments
                ADD CONSTRAINT FK_Departments_Branches FOREIGN KEY (BranchId)
                REFERENCES Branches(Id) ON DELETE SET NULL
                """, cancellationToken: ct));
        }
    }

    private static async Task CreateDepartmentBranchIndexAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        var indexExists = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT COUNT(*) FROM sys.indexes
            WHERE name = 'IX_Departments_BranchId' AND object_id = OBJECT_ID('Departments')
            """, cancellationToken: ct));

        if (indexExists == 0)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "CREATE INDEX IX_Departments_BranchId ON Departments (BranchId)",
                cancellationToken: ct));
        }
    }
}
