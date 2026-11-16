using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Paid-through / access-expiry columns for subscription deactivation lifecycle.
/// Does not rewrite historical invoice amounts.
/// </summary>
public static class SubscriptionAccessExpiryMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        await AddColumnIfMissingAsync(connection, "TenantSubscriptions", "PaidThroughUtc", "DATETIME2 NULL", cancellationToken);
        await AddColumnIfMissingAsync(connection, "TenantSubscriptions", "AccessExpiresAtUtc", "DATETIME2 NULL", cancellationToken);
        await AddColumnIfMissingAsync(connection, "TenantInvoices", "ExchangeRateUsed", "DECIMAL(18,6) NULL", cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE TenantSubscriptions
            SET PaidThroughUtc = COALESCE(PaidThroughUtc, SubscriptionEndDate, NextBillingDate, SubscriptionStartDate)
            WHERE PaidThroughUtc IS NULL;

            UPDATE TenantSubscriptions
            SET AccessExpiresAtUtc = DATEADD(day, @GraceDays, PaidThroughUtc)
            WHERE PaidThroughUtc IS NOT NULL
              AND (AccessExpiresAtUtc IS NULL OR AccessExpiresAtUtc < PaidThroughUtc);
            """, new { GraceDays = SubscriptionPlanPricing.OverdueGraceDays }, cancellationToken: cancellationToken));

        await EnsureIndexAsync(connection, "IX_TenantSubscriptions_NextBilling_Status_AutoRenew", """
            CREATE NONCLUSTERED INDEX IX_TenantSubscriptions_NextBilling_Status_AutoRenew
            ON TenantSubscriptions (NextBillingDate, Status, AutoRenew)
            """, cancellationToken);

        await EnsureIndexAsync(connection, "IX_TenantSubscriptions_AccessExpires_Status", """
            CREATE NONCLUSTERED INDEX IX_TenantSubscriptions_AccessExpires_Status
            ON TenantSubscriptions (AccessExpiresAtUtc, Status)
            """, cancellationToken);

        // Best-effort unique period guard (skip if duplicates already exist).
        try
        {
            await EnsureIndexAsync(connection, "UX_TenantInvoices_Tenant_BillingPeriod", """
                CREATE UNIQUE NONCLUSTERED INDEX UX_TenantInvoices_Tenant_BillingPeriod
                ON TenantInvoices (TenantId, BillingPeriodStart, BillingPeriodEnd)
                WHERE BillingPeriodStart IS NOT NULL AND BillingPeriodEnd IS NOT NULL
                  AND Status IN (N'Pending', N'Overdue', N'Paid')
                """, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Skipped UX_TenantInvoices_Tenant_BillingPeriod (likely existing duplicate periods).");
        }

        logger.LogInformation("Subscription access-expiry migration completed.");
    }

    private static async Task AddColumnIfMissingAsync(
        System.Data.IDbConnection connection, string table, string column, string definition, CancellationToken ct)
    {
        var exists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @Table AND COLUMN_NAME = @Column",
            new { Table = table, Column = column }, cancellationToken: ct));

        if (exists == 0)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                $"ALTER TABLE [{table}] ADD [{column}] {definition}", cancellationToken: ct));
        }
    }

    private static async Task EnsureIndexAsync(
        System.Data.IDbConnection connection, string indexName, string createSql, CancellationToken ct)
    {
        var exists = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT COUNT(*) FROM sys.indexes WHERE name = @Name AND object_id = OBJECT_ID(N'dbo.TenantSubscriptions')
            """, new { Name = indexName }, cancellationToken: ct));
        // Also check TenantInvoices for UX index
        if (exists == 0)
        {
            exists = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
                SELECT COUNT(*) FROM sys.indexes WHERE name = @Name
                """, new { Name = indexName }, cancellationToken: ct));
        }

        if (exists == 0)
            await connection.ExecuteAsync(new CommandDefinition(createSql, cancellationToken: ct));
    }
}
