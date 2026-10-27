using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Per-tracker annual subscription fields on GpsDevices + catalog seed in SubscriptionPlans (PlanType TrackerAnnual).
/// </summary>
public static class TrackerAnnualSubscriptionMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        await AddColumnIfMissingAsync(connection, "GpsDevices", "TrackerAnnualPlanCode", "NVARCHAR(50) NULL", cancellationToken);
        await AddColumnIfMissingAsync(connection, "GpsDevices", "TrackerAnnualAmountPkr", "DECIMAL(18,2) NULL", cancellationToken);
        await AddColumnIfMissingAsync(connection, "GpsDevices", "TrackerSubscriptionStartUtc", "DATETIME2 NULL", cancellationToken);
        await AddColumnIfMissingAsync(connection, "GpsDevices", "TrackerSubscriptionEndUtc", "DATETIME2 NULL", cancellationToken);

        await AddColumnIfMissingAsync(connection, "SubscriptionPlans", "AnnualAmountPkr", "DECIMAL(18,2) NULL", cancellationToken);
        await AddColumnIfMissingAsync(connection, "SubscriptionPlans", "BillingInterval", "NVARCHAR(20) NULL", cancellationToken);

        foreach (var plan in TrackerAnnualPlanCatalog.All)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF EXISTS (SELECT 1 FROM SubscriptionPlans WHERE SubscriptionCode = @PlanCode)
                    UPDATE SubscriptionPlans SET
                        DisplayName = @DisplayName,
                        Description = @Description,
                        PlanType = @PlanType,
                        Status = N'Active',
                        SortOrder = @SortOrder,
                        DurationMonths = 12,
                        IsDefault = @IsDefault,
                        Visible = @Visible,
                        AnnualAmountPkr = @AnnualAmountPkr,
                        BillingInterval = N'Annual',
                        MaxGpsDevices = 1,
                        GPSEnabled = 1
                    WHERE SubscriptionCode = @PlanCode;
                ELSE
                    INSERT INTO SubscriptionPlans (
                        SubscriptionCode, DisplayName, Description, PlanType, Status, SortOrder,
                        DurationMonths, IsDefault, Visible, AnnualAmountPkr, BillingInterval,
                        MaxGpsDevices, GPSEnabled)
                    VALUES (
                        @PlanCode, @DisplayName, @Description, @PlanType, N'Active', @SortOrder,
                        12, @IsDefault, @Visible, @AnnualAmountPkr, N'Annual',
                        1, 1);
                """,
                new
                {
                    plan.PlanCode,
                    plan.DisplayName,
                    plan.Description,
                    PlanType = TrackerAnnualPlanCatalog.PlanType,
                    plan.SortOrder,
                    plan.IsDefault,
                    plan.Visible,
                    plan.AnnualAmountPkr
                },
                cancellationToken: cancellationToken));
        }

        logger.LogInformation(
            "TrackerAnnualSubscriptionMigration applied ({PlanCount} tracker annual plans).",
            TrackerAnnualPlanCatalog.All.Count);
    }

    private static async Task AddColumnIfMissingAsync(
        System.Data.IDbConnection connection,
        string table,
        string column,
        string definition,
        CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition($"""
            IF NOT EXISTS (
                SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_NAME = @Table AND COLUMN_NAME = @Column)
                ALTER TABLE [{table}] ADD [{column}] {definition};
            """,
            new { Table = table, Column = column },
            cancellationToken: ct));
    }
}
