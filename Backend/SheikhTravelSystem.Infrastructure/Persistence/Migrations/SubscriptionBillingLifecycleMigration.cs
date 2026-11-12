using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Monthly subscription billing lifecycle: PKR plan amounts, next billing date, invoice email metadata, feature lock flag.
/// </summary>
public static class SubscriptionBillingLifecycleMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        await AddColumnIfMissingAsync(connection, "TenantSubscriptions", "NextBillingDate", "DATETIME2 NULL", cancellationToken);
        await AddColumnIfMissingAsync(connection, "TenantSubscriptions", "FeaturesLocked", "BIT NOT NULL CONSTRAINT DF_TenantSubscriptions_FeaturesLocked DEFAULT (0)", cancellationToken);
        await AddColumnIfMissingAsync(connection, "TenantInvoices", "EmailSentAt", "DATETIME2 NULL", cancellationToken);
        await AddColumnIfMissingAsync(connection, "TenantInvoices", "BillingPeriodStart", "DATETIME2 NULL", cancellationToken);
        await AddColumnIfMissingAsync(connection, "TenantInvoices", "BillingPeriodEnd", "DATETIME2 NULL", cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE TenantSubscriptions
            SET BillingCycle = N'Monthly'
            WHERE BillingCycle IS NULL OR LTRIM(RTRIM(BillingCycle)) = '';

            UPDATE TenantSubscriptions
            SET MonthlyAmount = CASE
                    WHEN UPPER(COALESCE(SubscriptionCode, PlanName, N'')) LIKE N'%STARTER%' THEN @Starter
                    WHEN UPPER(COALESCE(SubscriptionCode, PlanName, N'')) LIKE N'%PRO%' THEN @Pro
                    WHEN UPPER(COALESCE(SubscriptionCode, PlanName, N'')) LIKE N'%ENTERPRISE%' THEN @Enterprise
                    ELSE MonthlyAmount
                END
            WHERE MonthlyAmount IS NULL
               OR MonthlyAmount IN (5000, 15000, 25000);

            UPDATE TenantSubscriptions
            SET NextBillingDate = COALESCE(
                    NextBillingDate,
                    DATEADD(month, 1, COALESCE(SubscriptionStartDate, GETUTCDATE())))
            WHERE NextBillingDate IS NULL;

            UPDATE TenantSubscriptions
            SET SubscriptionEndDate = DATEADD(month, 1,
                    CASE WHEN SubscriptionEndDate IS NULL OR SubscriptionEndDate < GETUTCDATE()
                         THEN GETUTCDATE() ELSE SubscriptionEndDate END)
            WHERE BillingCycle = N'Monthly'
              AND (SubscriptionEndDate IS NULL OR SubscriptionEndDate < DATEADD(day, -20, GETUTCDATE()));
            """, new
        {
            // Legacy backfill only (rows with no amount): monthly equivalent of one licensed device.
            Starter = SubscriptionPlanPricing.ResolveMonthlyAmount("STARTER", 1),
            Pro = SubscriptionPlanPricing.ResolveMonthlyAmount("PRO", 1),
            Enterprise = SubscriptionPlanPricing.ResolveMonthlyAmount("ENTERPRISE", 1)
        }, cancellationToken: cancellationToken));

        logger.LogInformation("Subscription billing lifecycle migration completed.");
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
}
