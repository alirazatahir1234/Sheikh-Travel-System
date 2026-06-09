using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Adds subscription auto-renewal, invoices, and billing history tables for Subscription Management.
/// </summary>
public static class SubscriptionBillingMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        await ExtendSubscriptionsAsync(connection, cancellationToken);
        await CreateInvoicesTableAsync(connection, cancellationToken);
        await CreateBillingHistoryTableAsync(connection, cancellationToken);
        await SeedSampleBillingAsync(connection, cancellationToken);

        logger.LogInformation("Subscription billing migration completed.");
    }

    private static async Task ExtendSubscriptionsAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        await AddColumnIfMissingAsync(connection, "TenantSubscriptions", "AutoRenew", "BIT NOT NULL DEFAULT 1", ct);
        await AddColumnIfMissingAsync(connection, "TenantSubscriptions", "BillingCycle", "NVARCHAR(20) NOT NULL DEFAULT N'Monthly'", ct);
        await AddColumnIfMissingAsync(connection, "TenantSubscriptions", "MonthlyAmount", "DECIMAL(18,2) NULL", ct);
        await AddColumnIfMissingAsync(connection, "TenantSubscriptions", "CurrencyCode", "NVARCHAR(10) NULL", ct);

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE TenantSubscriptions
            SET MonthlyAmount = CASE
                    WHEN LOWER(COALESCE(PlanName, N'')) LIKE N'%enterprise%' THEN 25000
                    WHEN LOWER(COALESCE(PlanName, N'')) LIKE N'%pro%' THEN 15000
                    ELSE 5000
                END
            WHERE MonthlyAmount IS NULL;

            UPDATE TenantSubscriptions SET CurrencyCode = N'PKR' WHERE CurrencyCode IS NULL OR LTRIM(RTRIM(CurrencyCode)) = '';

            UPDATE TenantSubscriptions
            SET SubscriptionEndDate = DATEADD(year, 1, COALESCE(SubscriptionStartDate, GETUTCDATE()))
            WHERE SubscriptionEndDate IS NULL;
            """, cancellationToken: ct));
    }

    private static async Task CreateInvoicesTableAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'TenantInvoices')
            CREATE TABLE TenantInvoices (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                TenantId INT NOT NULL,
                InvoiceNumber NVARCHAR(50) NOT NULL,
                PlanName NVARCHAR(100) NULL,
                Amount DECIMAL(18,2) NOT NULL,
                CurrencyCode NVARCHAR(10) NOT NULL DEFAULT N'PKR',
                Status NVARCHAR(30) NOT NULL DEFAULT N'Pending',
                IssuedDate DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                DueDate DATETIME2 NULL,
                PaidDate DATETIME2 NULL,
                CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                CONSTRAINT FK_TenantInvoices_Tenants FOREIGN KEY (TenantId) REFERENCES Tenants(Id),
                CONSTRAINT UQ_TenantInvoices_Number UNIQUE (InvoiceNumber)
            );
            """, cancellationToken: ct));

        await CreateIndexIfMissingAsync(connection, "IX_TenantInvoices_TenantId", "TenantInvoices", "TenantId", ct);
    }

    private static async Task CreateBillingHistoryTableAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'TenantBillingHistory')
            CREATE TABLE TenantBillingHistory (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                TenantId INT NOT NULL,
                InvoiceId INT NULL,
                Amount DECIMAL(18,2) NOT NULL,
                CurrencyCode NVARCHAR(10) NOT NULL DEFAULT N'PKR',
                PaymentMethod NVARCHAR(50) NULL,
                Status NVARCHAR(30) NOT NULL DEFAULT N'Completed',
                Reference NVARCHAR(100) NULL,
                Notes NVARCHAR(500) NULL,
                PaidAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                CONSTRAINT FK_TenantBillingHistory_Tenants FOREIGN KEY (TenantId) REFERENCES Tenants(Id)
            );
            """, cancellationToken: ct));

        await CreateIndexIfMissingAsync(connection, "IX_TenantBillingHistory_TenantId", "TenantBillingHistory", "TenantId", ct);
    }

    private static async Task SeedSampleBillingAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        var existing = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM TenantInvoices", cancellationToken: ct));
        if (existing > 0) return;

        var tenants = (await connection.QueryAsync<(int Id, string? Plan, decimal? Amount, string? Currency)>(
            new CommandDefinition("""
                SELECT t.Id, COALESCE(s.PlanName, t.SubscriptionPlan) AS PlanName, s.MonthlyAmount AS Amount, s.CurrencyCode AS Currency
                FROM Tenants t
                LEFT JOIN TenantSubscriptions s ON s.TenantId = t.Id
                WHERE t.IsActive = 1
                """, cancellationToken: ct))).ToList();

        foreach (var tenant in tenants)
        {
            var amount = tenant.Amount ?? 5000m;
            var currency = string.IsNullOrWhiteSpace(tenant.Currency) ? "PKR" : tenant.Currency;
            var plan = tenant.Plan ?? "Enterprise";

            // 3 months of history: 2 paid, 1 pending current
            for (var monthsAgo = 2; monthsAgo >= 0; monthsAgo--)
            {
                var isPaid = monthsAgo > 0;
                var invoiceNumber = $"INV-{tenant.Id:D4}-{DateTime.UtcNow.AddMonths(-monthsAgo):yyyyMM}";

                var invoiceId = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
                    INSERT INTO TenantInvoices (TenantId, InvoiceNumber, PlanName, Amount, CurrencyCode, Status, IssuedDate, DueDate, PaidDate, CreatedAt)
                    VALUES (@TenantId, @InvoiceNumber, @Plan, @Amount, @Currency, @Status,
                            DATEADD(month, -@MonthsAgo, GETUTCDATE()),
                            DATEADD(day, 14, DATEADD(month, -@MonthsAgo, GETUTCDATE())),
                            @PaidDate, GETUTCDATE());
                    SELECT CAST(SCOPE_IDENTITY() AS INT);
                    """, new
                {
                    TenantId = tenant.Id,
                    InvoiceNumber = invoiceNumber,
                    Plan = plan,
                    Amount = amount,
                    Currency = currency,
                    Status = isPaid ? "Paid" : "Pending",
                    MonthsAgo = monthsAgo,
                    PaidDate = isPaid ? (DateTime?)DateTime.UtcNow.AddMonths(-monthsAgo).AddDays(3) : null
                }, cancellationToken: ct));

                if (isPaid)
                {
                    await connection.ExecuteAsync(new CommandDefinition("""
                        INSERT INTO TenantBillingHistory (TenantId, InvoiceId, Amount, CurrencyCode, PaymentMethod, Status, Reference, PaidAt)
                        VALUES (@TenantId, @InvoiceId, @Amount, @Currency, N'Bank Transfer', N'Completed', @Reference,
                                DATEADD(day, 3, DATEADD(month, -@MonthsAgo, GETUTCDATE())));
                        """, new
                    {
                        TenantId = tenant.Id,
                        InvoiceId = invoiceId,
                        Amount = amount,
                        Currency = currency,
                        Reference = $"PAY-{invoiceId:D6}",
                        MonthsAgo = monthsAgo
                    }, cancellationToken: ct));
                }
            }
        }
    }

    private static async Task CreateIndexIfMissingAsync(
        System.Data.IDbConnection connection, string indexName, string tableName, string columnName, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = @IndexName AND object_id = OBJECT_ID(@TableName))
            EXEC('CREATE INDEX [' + @IndexName + '] ON [' + @TableName + '] ([' + @ColumnName + '])');
            """, new { IndexName = indexName, TableName = tableName, ColumnName = columnName }, cancellationToken: ct));
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
