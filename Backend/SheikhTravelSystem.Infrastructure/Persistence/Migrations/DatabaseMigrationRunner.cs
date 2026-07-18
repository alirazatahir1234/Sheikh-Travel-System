using System.Data;
using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

public sealed class DatabaseMigrationRunner(
    IDbConnectionFactory dbFactory,
    ILogger<DatabaseMigrationRunner> logger) : IDatabaseMigrationRunner
{
    private const string HistoryTableSql = """
        IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'SchemaMigrationHistory')
        CREATE TABLE SchemaMigrationHistory (
            Id INT IDENTITY(1,1) PRIMARY KEY,
            MigrationName NVARCHAR(200) NOT NULL,
            AppliedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_SchemaMigrationHistory_AppliedAtUtc DEFAULT GETUTCDATE(),
            AppliedBy NVARCHAR(100) NOT NULL,
            CONSTRAINT UQ_SchemaMigrationHistory_MigrationName UNIQUE (MigrationName)
        );
        """;

    public async Task EnsureHistoryTableAsync(CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(HistoryTableSql, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<SchemaMigrationStatusDto>> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        await EnsureHistoryTableAsync(cancellationToken);

        using var connection = dbFactory.CreateConnection();
        var applied = await LoadAppliedAsync(connection, cancellationToken);
        var registry = DatabaseMigrationRegistry.All;

        var result = new List<SchemaMigrationStatusDto>(registry.Count);
        for (var i = 0; i < registry.Count; i++)
        {
            var migration = registry[i];
            applied.TryGetValue(migration.Name, out var history);
            result.Add(new SchemaMigrationStatusDto
            {
                Name = migration.Name,
                Order = i + 1,
                IsApplied = history is not null,
                AppliedAtUtc = history?.AppliedAtUtc,
                AppliedBy = history?.AppliedBy
            });
        }

        return result;
    }

    public async Task<SchemaMigrationApplyResultDto> ApplyPendingAsync(
        string appliedBy,
        CancellationToken cancellationToken = default)
    {
        await EnsureHistoryTableAsync(cancellationToken);

        using var connection = dbFactory.CreateConnection();
        var applied = await LoadAppliedAsync(connection, cancellationToken);
        var registry = DatabaseMigrationRegistry.All;

        var pending = registry.Where(m => !applied.ContainsKey(m.Name)).ToList();
        if (pending.Count == 0)
        {
            logger.LogInformation(
                "No pending database migrations ({AppliedCount}/{TotalCount} applied).",
                applied.Count,
                registry.Count);
            return new SchemaMigrationApplyResultDto
            {
                AppliedCount = 0,
                SkippedCount = registry.Count,
                AppliedNames = Array.Empty<string>()
            };
        }

        logger.LogInformation(
            "Applying {PendingCount} pending database migration(s) ({AlreadyApplied} already applied).",
            pending.Count,
            applied.Count);

        var appliedNames = new List<string>();
        foreach (var migration in pending)
        {
            try
            {
                logger.LogInformation("Applying migration {MigrationName}...", migration.Name);
                await migration.ApplyAsync(dbFactory, logger, cancellationToken);
                await RecordAppliedAsync(connection, migration.Name, appliedBy, cancellationToken);
                appliedNames.Add(migration.Name);
                logger.LogInformation("Migration {MigrationName} recorded in SchemaMigrationHistory.", migration.Name);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Migration {MigrationName} failed.", migration.Name);
                return new SchemaMigrationApplyResultDto
                {
                    AppliedCount = appliedNames.Count,
                    SkippedCount = applied.Count,
                    AppliedNames = appliedNames,
                    FailedMigration = migration.Name,
                    ErrorMessage = ex.Message
                };
            }
        }

        return new SchemaMigrationApplyResultDto
        {
            AppliedCount = appliedNames.Count,
            SkippedCount = applied.Count,
            AppliedNames = appliedNames
        };
    }

    private static async Task<Dictionary<string, MigrationHistoryRow>> LoadAppliedAsync(
        IDbConnection connection,
        CancellationToken cancellationToken)
    {
        var rows = await connection.QueryAsync<MigrationHistoryRow>(new CommandDefinition(
            "SELECT MigrationName, AppliedAtUtc, AppliedBy FROM SchemaMigrationHistory",
            cancellationToken: cancellationToken));

        return rows.ToDictionary(r => r.MigrationName, StringComparer.OrdinalIgnoreCase);
    }

    private static async Task RecordAppliedAsync(
        IDbConnection connection,
        string migrationName,
        string appliedBy,
        CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM SchemaMigrationHistory WHERE MigrationName = @MigrationName)
            INSERT INTO SchemaMigrationHistory (MigrationName, AppliedAtUtc, AppliedBy)
            VALUES (@MigrationName, GETUTCDATE(), @AppliedBy);
            """,
            new { MigrationName = migrationName, AppliedBy = appliedBy },
            cancellationToken: cancellationToken));
    }

    private sealed class MigrationHistoryRow
    {
        public string MigrationName { get; init; } = "";
        public DateTime AppliedAtUtc { get; init; }
        public string AppliedBy { get; init; } = "";
    }
}
