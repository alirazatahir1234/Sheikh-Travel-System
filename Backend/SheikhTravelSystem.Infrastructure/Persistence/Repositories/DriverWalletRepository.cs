using Dapper;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.DriverWallets.DTOs;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories;

public sealed class DriverWalletRepository(IDbConnectionFactory dbFactory) : IDriverWalletRepository
{
    public async Task EnsureWalletAsync(int tenantId, int driverId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (
                SELECT 1 FROM DriverWallets WHERE TenantId = @TenantId AND DriverId = @DriverId AND IsDeleted = 0)
            INSERT INTO DriverWallets (TenantId, DriverId, Balance, Currency, CashOutCadence)
            VALUES (@TenantId, @DriverId, 0, N'PKR', N'Weekly');
            """, new { TenantId = tenantId, DriverId = driverId }, cancellationToken: cancellationToken));
    }

    public async Task<DriverWalletDto?> GetWalletAsync(int tenantId, int driverId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var row = await connection.QueryFirstOrDefaultAsync<DriverWalletRow>(new CommandDefinition("""
            SELECT w.DriverId,
                   d.FullName AS DriverName,
                   w.Balance,
                   w.Currency,
                   w.CashOutCadence,
                   w.BankAccountName,
                   w.BankAccountNumber,
                   w.BankName,
                   w.JazzCashNumber,
                   w.EasypaisaNumber
            FROM DriverWallets w
            LEFT JOIN Drivers d ON d.Id = w.DriverId AND d.TenantId = w.TenantId
            WHERE w.TenantId = @TenantId AND w.DriverId = @DriverId AND w.IsDeleted = 0
            """, new { TenantId = tenantId, DriverId = driverId }, cancellationToken: cancellationToken));
        return row is null ? null : MapWallet(row);
    }

    public async Task<PagedResult<DriverWalletDto>> GetWalletsPagedAsync(
        int tenantId, int page, int pageSize, int? driverId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var where = "w.TenantId = @TenantId AND w.IsDeleted = 0";
        if (driverId.HasValue) where += " AND w.DriverId = @DriverId";

        var parameters = new
        {
            TenantId = tenantId,
            DriverId = driverId,
            Offset = (Math.Max(page, 1) - 1) * Math.Max(pageSize, 1),
            PageSize = Math.Max(pageSize, 1)
        };

        var total = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            $"SELECT COUNT(1) FROM DriverWallets w WHERE {where}", parameters, cancellationToken: cancellationToken));

        var items = (await connection.QueryAsync<DriverWalletRow>(new CommandDefinition(
            $"""
            SELECT w.DriverId, d.FullName AS DriverName, w.Balance, w.Currency, w.CashOutCadence,
                   w.BankAccountName, w.BankAccountNumber, w.BankName, w.JazzCashNumber, w.EasypaisaNumber
            FROM DriverWallets w
            LEFT JOIN Drivers d ON d.Id = w.DriverId AND d.TenantId = w.TenantId
            WHERE {where}
            ORDER BY w.Balance DESC, w.DriverId
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
            """,
            parameters, cancellationToken: cancellationToken))).Select(MapWallet).ToList();

        return new PagedResult<DriverWalletDto>
        {
            Items = items,
            TotalCount = total,
            Page = Math.Max(page, 1),
            PageSize = Math.Max(pageSize, 1)
        };
    }

    public async Task UpdatePayoutDetailsAsync(
        int tenantId, int driverId, UpdateDriverWalletPayoutDetailsDto details, CancellationToken cancellationToken = default)
    {
        await EnsureWalletAsync(tenantId, driverId, cancellationToken);
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE DriverWallets SET
                BankAccountName = @BankAccountName,
                BankAccountNumber = @BankAccountNumber,
                BankName = @BankName,
                JazzCashNumber = @JazzCashNumber,
                EasypaisaNumber = @EasypaisaNumber,
                CashOutCadence = COALESCE(NULLIF(@CashOutCadence, ''), CashOutCadence),
                UpdatedAt = SYSUTCDATETIME()
            WHERE TenantId = @TenantId AND DriverId = @DriverId AND IsDeleted = 0
            """, new
        {
            TenantId = tenantId,
            DriverId = driverId,
            details.BankAccountName,
            details.BankAccountNumber,
            details.BankName,
            details.JazzCashNumber,
            details.EasypaisaNumber,
            details.CashOutCadence
        }, cancellationToken: cancellationToken));
    }

    public async Task<bool> TryCreditAsync(
        int tenantId,
        int driverId,
        decimal amount,
        string entryType,
        string referenceType,
        int referenceId,
        string description,
        int? createdByUserId,
        CancellationToken cancellationToken = default)
    {
        if (amount <= 0) return false;
        await EnsureWalletAsync(tenantId, driverId, cancellationToken);

        using var connection = dbFactory.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();

        var exists = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT CASE WHEN EXISTS(
                SELECT 1 FROM DriverWalletLedger
                WHERE TenantId = @TenantId AND ReferenceType = @ReferenceType AND ReferenceId = @ReferenceId) THEN 1 ELSE 0 END
            """, new { TenantId = tenantId, ReferenceType = referenceType, ReferenceId = referenceId },
            transaction: tx, cancellationToken: cancellationToken));
        if (exists == 1)
        {
            tx.Commit();
            return false;
        }

        var wallet = await connection.QueryFirstAsync<(int Id, decimal Balance)>(new CommandDefinition("""
            SELECT Id, Balance FROM DriverWallets WITH (UPDLOCK, ROWLOCK)
            WHERE TenantId = @TenantId AND DriverId = @DriverId AND IsDeleted = 0
            """, new { TenantId = tenantId, DriverId = driverId }, transaction: tx, cancellationToken: cancellationToken));

        var newBalance = wallet.Balance + amount;
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE DriverWallets SET Balance = @Balance, UpdatedAt = SYSUTCDATETIME() WHERE Id = @Id
            """, new { Balance = newBalance, Id = wallet.Id }, transaction: tx, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO DriverWalletLedger
                (TenantId, DriverId, WalletId, EntryType, Amount, BalanceAfter, ReferenceType, ReferenceId, Description, CreatedByUserId)
            VALUES
                (@TenantId, @DriverId, @WalletId, @EntryType, @Amount, @BalanceAfter, @ReferenceType, @ReferenceId, @Description, @CreatedByUserId)
            """, new
        {
            TenantId = tenantId,
            DriverId = driverId,
            WalletId = wallet.Id,
            EntryType = entryType,
            Amount = amount,
            BalanceAfter = newBalance,
            ReferenceType = referenceType,
            ReferenceId = referenceId,
            Description = description,
            CreatedByUserId = createdByUserId
        }, transaction: tx, cancellationToken: cancellationToken));

        tx.Commit();
        return true;
    }

    public async Task<PagedResult<DriverWalletLedgerEntryDto>> GetLedgerPagedAsync(
        int tenantId, int? driverId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var where = "l.TenantId = @TenantId";
        if (driverId.HasValue) where += " AND l.DriverId = @DriverId";

        var parameters = new
        {
            TenantId = tenantId,
            DriverId = driverId,
            Offset = (Math.Max(page, 1) - 1) * Math.Max(pageSize, 1),
            PageSize = Math.Max(pageSize, 1)
        };

        var total = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            $"SELECT COUNT(1) FROM DriverWalletLedger l WHERE {where}", parameters, cancellationToken: cancellationToken));

        var items = (await connection.QueryAsync<DriverWalletLedgerEntryDto>(new CommandDefinition(
            $"""
            SELECT l.Id, l.EntryType, l.Amount, l.BalanceAfter, l.ReferenceType, l.ReferenceId, l.Description, l.CreatedAt
            FROM DriverWalletLedger l
            WHERE {where}
            ORDER BY l.CreatedAt DESC, l.Id DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
            """,
            parameters, cancellationToken: cancellationToken))).ToList();

        return new PagedResult<DriverWalletLedgerEntryDto>
        {
            Items = items,
            TotalCount = total,
            Page = Math.Max(page, 1),
            PageSize = Math.Max(pageSize, 1)
        };
    }

    public async Task<PagedResult<DriverCashOutRequestDto>> GetCashOutRequestsPagedAsync(
        int tenantId, int page, int pageSize, int? driverId, string? status, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var where = "c.TenantId = @TenantId AND c.IsDeleted = 0";
        if (driverId.HasValue) where += " AND c.DriverId = @DriverId";
        if (!string.IsNullOrWhiteSpace(status)) where += " AND c.Status = @Status";

        var parameters = new
        {
            TenantId = tenantId,
            DriverId = driverId,
            Status = status,
            Offset = (Math.Max(page, 1) - 1) * Math.Max(pageSize, 1),
            PageSize = Math.Max(pageSize, 1)
        };

        var total = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            $"SELECT COUNT(1) FROM DriverCashOutRequests c WHERE {where}", parameters, cancellationToken: cancellationToken));

        var items = (await connection.QueryAsync<DriverCashOutRequestDto>(new CommandDefinition(
            $"""
            SELECT c.Id, c.DriverId, d.FullName AS DriverName, c.GrossAmount, c.ProcessingFee, c.NetPayout,
                   c.ProcessingFeeRate, c.Method, c.DestinationSnapshot, c.Status, c.RequestedAt, c.ProcessedAt,
                   c.PaymentReference, c.RejectionReason
            FROM DriverCashOutRequests c
            LEFT JOIN Drivers d ON d.Id = c.DriverId AND d.TenantId = c.TenantId
            WHERE {where}
            ORDER BY c.RequestedAt DESC, c.Id DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
            """,
            parameters, cancellationToken: cancellationToken))).ToList();

        return new PagedResult<DriverCashOutRequestDto>
        {
            Items = items,
            TotalCount = total,
            Page = Math.Max(page, 1),
            PageSize = Math.Max(pageSize, 1)
        };
    }

    public async Task<int> CreateCashOutRequestAsync(
        int tenantId,
        int driverId,
        int? requestedByUserId,
        CreateDriverCashOutRequestDto request,
        CashOutFeeBreakdown fee,
        string destinationSnapshot,
        CancellationToken cancellationToken = default)
    {
        await EnsureWalletAsync(tenantId, driverId, cancellationToken);
        using var connection = dbFactory.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();

        var wallet = await connection.QueryFirstAsync<(int Id, decimal Balance)>(new CommandDefinition("""
            SELECT Id, Balance FROM DriverWallets WITH (UPDLOCK, ROWLOCK)
            WHERE TenantId = @TenantId AND DriverId = @DriverId AND IsDeleted = 0
            """, new { TenantId = tenantId, DriverId = driverId }, transaction: tx, cancellationToken: cancellationToken));

        if (wallet.Balance < fee.GrossAmount)
            throw new InvalidOperationException("Insufficient wallet balance.");

        var newBalance = wallet.Balance - fee.GrossAmount;
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE DriverWallets SET Balance = @Balance, UpdatedAt = SYSUTCDATETIME() WHERE Id = @Id
            """, new { Balance = newBalance, Id = wallet.Id }, transaction: tx, cancellationToken: cancellationToken));

        var cashOutId = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            INSERT INTO DriverCashOutRequests
                (TenantId, DriverId, WalletId, GrossAmount, ProcessingFee, NetPayout, ProcessingFeeRate,
                 Method, DestinationSnapshot, Status, Notes)
            OUTPUT INSERTED.Id
            VALUES
                (@TenantId, @DriverId, @WalletId, @GrossAmount, @ProcessingFee, @NetPayout, @ProcessingFeeRate,
                 @Method, @DestinationSnapshot, N'Pending', @Notes)
            """, new
        {
            TenantId = tenantId,
            DriverId = driverId,
            WalletId = wallet.Id,
            fee.GrossAmount,
            ProcessingFee = fee.ProcessingFee,
            fee.NetPayout,
            ProcessingFeeRate = fee.ProcessingFeeRate,
            Method = request.Method,
            DestinationSnapshot = destinationSnapshot,
            request.Notes
        }, transaction: tx, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO DriverWalletLedger
                (TenantId, DriverId, WalletId, EntryType, Amount, BalanceAfter, ReferenceType, ReferenceId, Description, CreatedByUserId)
            VALUES
                (@TenantId, @DriverId, @WalletId, N'CashOutHold', @Amount, @BalanceAfter, N'CashOutRequest', @ReferenceId, @Description, @CreatedByUserId)
            """, new
        {
            TenantId = tenantId,
            DriverId = driverId,
            WalletId = wallet.Id,
            Amount = -fee.GrossAmount,
            BalanceAfter = newBalance,
            ReferenceId = cashOutId,
            Description = $"Cash-out request ({request.Method}) gross {fee.GrossAmount:N2} PKR, fee {fee.ProcessingFee:N2}, net {fee.NetPayout:N2}",
            CreatedByUserId = requestedByUserId
        }, transaction: tx, cancellationToken: cancellationToken));

        tx.Commit();
        return cashOutId;
    }

    public async Task<DriverCashOutRequestDto?> GetCashOutRequestAsync(
        int tenantId, int id, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<DriverCashOutRequestDto>(new CommandDefinition("""
            SELECT c.Id, c.DriverId, d.FullName AS DriverName, c.GrossAmount, c.ProcessingFee, c.NetPayout,
                   c.ProcessingFeeRate, c.Method, c.DestinationSnapshot, c.Status, c.RequestedAt, c.ProcessedAt,
                   c.PaymentReference, c.RejectionReason
            FROM DriverCashOutRequests c
            LEFT JOIN Drivers d ON d.Id = c.DriverId AND d.TenantId = c.TenantId
            WHERE c.TenantId = @TenantId AND c.Id = @Id AND c.IsDeleted = 0
            """, new { TenantId = tenantId, Id = id }, cancellationToken: cancellationToken));
    }

    public async Task RejectCashOutAsync(
        int tenantId, int id, int processedByUserId, string reason, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();

        var row = await connection.QueryFirstOrDefaultAsync<(int DriverId, int WalletId, decimal GrossAmount, string Status)>(
            new CommandDefinition("""
                SELECT DriverId, WalletId, GrossAmount, Status FROM DriverCashOutRequests WITH (UPDLOCK, ROWLOCK)
                WHERE TenantId = @TenantId AND Id = @Id AND IsDeleted = 0
                """, new { TenantId = tenantId, Id = id }, transaction: tx, cancellationToken: cancellationToken));

        if (row.DriverId == 0 || !string.Equals(row.Status, "Pending", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Cash-out request cannot be rejected.");

        var walletBalance = await connection.QueryFirstAsync<decimal>(new CommandDefinition("""
            SELECT Balance FROM DriverWallets WITH (UPDLOCK, ROWLOCK) WHERE Id = @Id
            """, new { Id = row.WalletId }, transaction: tx, cancellationToken: cancellationToken));

        var newBalance = walletBalance + row.GrossAmount;
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE DriverWallets SET Balance = @Balance, UpdatedAt = SYSUTCDATETIME() WHERE Id = @WalletId
            """, new { Balance = newBalance, WalletId = row.WalletId }, transaction: tx, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE DriverCashOutRequests SET Status = N'Rejected', ProcessedAt = SYSUTCDATETIME(),
                ProcessedByUserId = @UserId, RejectionReason = @Reason
            WHERE TenantId = @TenantId AND Id = @Id
            """, new { TenantId = tenantId, Id = id, UserId = processedByUserId, Reason = reason },
            transaction: tx, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO DriverWalletLedger
                (TenantId, DriverId, WalletId, EntryType, Amount, BalanceAfter, ReferenceType, ReferenceId, Description, CreatedByUserId)
            VALUES
                (@TenantId, @DriverId, @WalletId, N'CashOutRefund', @Amount, @BalanceAfter, N'CashOutRequest', @ReferenceId, @Description, @CreatedByUserId)
            """, new
        {
            TenantId = tenantId,
            DriverId = row.DriverId,
            WalletId = row.WalletId,
            Amount = row.GrossAmount,
            BalanceAfter = newBalance,
            ReferenceId = id,
            Description = "Cash-out rejected — balance restored",
            CreatedByUserId = processedByUserId
        }, transaction: tx, cancellationToken: cancellationToken));

        tx.Commit();
    }

    public async Task<int> BulkMarkCashOutPaidAsync(
        int tenantId,
        IReadOnlyList<int> ids,
        int processedByUserId,
        string? paymentReferencePrefix,
        CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0) return 0;
        using var connection = dbFactory.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();

        var updated = 0;
        foreach (var id in ids.Distinct())
        {
            var row = await connection.QueryFirstOrDefaultAsync<(string Status, decimal NetPayout, string Method)>(
                new CommandDefinition("""
                    SELECT Status, NetPayout, Method FROM DriverCashOutRequests WITH (UPDLOCK, ROWLOCK)
                    WHERE TenantId = @TenantId AND Id = @Id AND IsDeleted = 0
                    """, new { TenantId = tenantId, Id = id }, transaction: tx, cancellationToken: cancellationToken));

            if (row.Status is null || !string.Equals(row.Status, "Pending", StringComparison.OrdinalIgnoreCase))
                continue;

            var reference = !string.IsNullOrWhiteSpace(paymentReferencePrefix)
                ? paymentReferencePrefix
                : $"PAYOUT-{id}-{DateTime.UtcNow:yyyyMMddHHmmss}";
            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE DriverCashOutRequests SET Status = N'Paid', ProcessedAt = SYSUTCDATETIME(),
                    ProcessedByUserId = @UserId, PaymentReference = @Reference
                WHERE TenantId = @TenantId AND Id = @Id
                """, new { TenantId = tenantId, Id = id, UserId = processedByUserId, Reference = reference },
                transaction: tx, cancellationToken: cancellationToken));
            updated++;
        }

        tx.Commit();
        return updated;
    }

    private sealed class DriverWalletRow
    {
        public int DriverId { get; init; }
        public string? DriverName { get; init; }
        public decimal Balance { get; init; }
        public string Currency { get; init; } = "PKR";
        public string CashOutCadence { get; init; } = "Weekly";
        public string? BankAccountName { get; init; }
        public string? BankAccountNumber { get; init; }
        public string? BankName { get; init; }
        public string? JazzCashNumber { get; init; }
        public string? EasypaisaNumber { get; init; }
    }

    private static DriverWalletDto MapWallet(DriverWalletRow w) => new(
        w.DriverId,
        w.DriverName,
        w.Balance,
        w.Currency,
        w.CashOutCadence,
        new DriverWalletPayoutDetailsDto(
            w.BankAccountName,
            w.BankAccountNumber,
            w.BankName,
            w.JazzCashNumber,
            w.EasypaisaNumber));
}
