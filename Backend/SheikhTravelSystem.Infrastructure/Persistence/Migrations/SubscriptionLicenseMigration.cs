using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Stage 4 Subscription &amp; License: plan catalog + license metadata on TenantSubscriptions.
/// No billing engine or payment gateway.
/// </summary>
public static class SubscriptionLicenseMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'SubscriptionPlans')
            CREATE TABLE SubscriptionPlans (
                SubscriptionCode NVARCHAR(50) NOT NULL,
                DisplayName NVARCHAR(100) NOT NULL,
                Description NVARCHAR(500) NULL,
                PlanType NVARCHAR(50) NOT NULL CONSTRAINT DF_SubscriptionPlans_PlanType DEFAULT N'Standard',
                Status NVARCHAR(50) NOT NULL CONSTRAINT DF_SubscriptionPlans_Status DEFAULT N'Active',
                SortOrder INT NOT NULL CONSTRAINT DF_SubscriptionPlans_SortOrder DEFAULT (0),
                DurationMonths INT NULL,
                IsDefault BIT NOT NULL CONSTRAINT DF_SubscriptionPlans_IsDefault DEFAULT (0),
                Visible BIT NOT NULL CONSTRAINT DF_SubscriptionPlans_Visible DEFAULT (1),
                DocumentationUrl NVARCHAR(500) NULL,
                DefaultModuleCodesJson NVARCHAR(MAX) NULL,
                MaxUsers INT NULL,
                MaxVehicles INT NULL,
                MaxDrivers INT NULL,
                MaxBranches INT NULL,
                MaxGpsDevices INT NULL,
                StorageQuotaGb INT NULL,
                AICredits INT NULL,
                GPSEnabled BIT NOT NULL CONSTRAINT DF_SubscriptionPlans_GPSEnabled DEFAULT (1),
                CONSTRAINT PK_SubscriptionPlans PRIMARY KEY (SubscriptionCode)
            );
            """, cancellationToken: cancellationToken));

        await AddColumnIfMissingAsync(connection, "TenantSubscriptions", "SubscriptionCode", "NVARCHAR(50) NULL", cancellationToken);
        await AddColumnIfMissingAsync(connection, "TenantSubscriptions", "StorageQuotaGb", "INT NULL", cancellationToken);
        await AddColumnIfMissingAsync(connection, "TenantSubscriptions", "AICredits", "INT NULL", cancellationToken);
        await AddColumnIfMissingAsync(connection, "TenantSubscriptions", "GPSEnabled", "BIT NOT NULL CONSTRAINT DF_TenantSubscriptions_GPSEnabled DEFAULT (1)", cancellationToken);

        foreach (var plan in SubscriptionPlanCatalog.All)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF EXISTS (SELECT 1 FROM SubscriptionPlans WHERE SubscriptionCode = @SubscriptionCode)
                    UPDATE SubscriptionPlans SET
                        DisplayName = @DisplayName,
                        Description = @Description,
                        PlanType = @PlanType,
                        Status = @Status,
                        SortOrder = @SortOrder,
                        DurationMonths = @DurationMonths,
                        IsDefault = @IsDefault,
                        Visible = @Visible,
                        DocumentationUrl = @DocumentationUrl,
                        DefaultModuleCodesJson = @DefaultModuleCodesJson,
                        MaxUsers = @MaxUsers,
                        MaxVehicles = @MaxVehicles,
                        MaxDrivers = @MaxDrivers,
                        MaxBranches = @MaxBranches,
                        MaxGpsDevices = @MaxGpsDevices,
                        StorageQuotaGb = @StorageQuotaGb,
                        AICredits = @AICredits,
                        GPSEnabled = @GPSEnabled
                    WHERE SubscriptionCode = @SubscriptionCode;
                ELSE
                    INSERT INTO SubscriptionPlans (
                        SubscriptionCode, DisplayName, Description, PlanType, Status, SortOrder,
                        DurationMonths, IsDefault, Visible, DocumentationUrl, DefaultModuleCodesJson,
                        MaxUsers, MaxVehicles, MaxDrivers, MaxBranches, MaxGpsDevices,
                        StorageQuotaGb, AICredits, GPSEnabled)
                    VALUES (
                        @SubscriptionCode, @DisplayName, @Description, @PlanType, @Status, @SortOrder,
                        @DurationMonths, @IsDefault, @Visible, @DocumentationUrl, @DefaultModuleCodesJson,
                        @MaxUsers, @MaxVehicles, @MaxDrivers, @MaxBranches, @MaxGpsDevices,
                        @StorageQuotaGb, @AICredits, @GPSEnabled);
                """,
                new
                {
                    plan.SubscriptionCode,
                    plan.DisplayName,
                    plan.Description,
                    plan.PlanType,
                    plan.Status,
                    plan.SortOrder,
                    plan.DurationMonths,
                    plan.IsDefault,
                    plan.Visible,
                    plan.DocumentationUrl,
                    DefaultModuleCodesJson = SubscriptionPlanCatalog.SerializeModuleCodes(plan.DefaultModuleCodes),
                    plan.MaxUsers,
                    plan.MaxVehicles,
                    plan.MaxDrivers,
                    plan.MaxBranches,
                    plan.MaxGpsDevices,
                    plan.StorageQuotaGb,
                    plan.AICredits,
                    plan.GPSEnabled
                },
                cancellationToken: cancellationToken));
        }

        // Backfill SubscriptionCode + license metadata from PlanName / catalog defaults.
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE s
            SET s.SubscriptionCode = CASE
                    WHEN UPPER(LTRIM(RTRIM(COALESCE(s.PlanName, t.SubscriptionPlan, N'')))) LIKE N'%ENTERPRISE%' THEN N'ENTERPRISE'
                    WHEN UPPER(LTRIM(RTRIM(COALESCE(s.PlanName, t.SubscriptionPlan, N'')))) LIKE N'%PRO%' THEN N'PRO'
                    WHEN UPPER(LTRIM(RTRIM(COALESCE(s.PlanName, t.SubscriptionPlan, N'')))) LIKE N'%STARTER%' THEN N'STARTER'
                    ELSE N'ENTERPRISE'
                END
            FROM TenantSubscriptions s
            INNER JOIN Tenants t ON t.Id = s.TenantId
            WHERE s.SubscriptionCode IS NULL OR LTRIM(RTRIM(s.SubscriptionCode)) = '';

            UPDATE s
            SET s.StorageQuotaGb = COALESCE(s.StorageQuotaGb, p.StorageQuotaGb),
                s.AICredits = COALESCE(s.AICredits, p.AICredits),
                s.GPSEnabled = COALESCE(s.GPSEnabled, p.GPSEnabled)
            FROM TenantSubscriptions s
            INNER JOIN SubscriptionPlans p ON p.SubscriptionCode = s.SubscriptionCode;
            """, cancellationToken: cancellationToken));

        logger.LogInformation(
            "SubscriptionLicenseMigration applied ({PlanCount} subscription plans + TenantSubscriptions license columns).",
            SubscriptionPlanCatalog.All.Count);
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
