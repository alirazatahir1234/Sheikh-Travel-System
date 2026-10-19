using Dapper;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Settlements.DTOs;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories;

public sealed class DriverSettlementRepository(IDbConnectionFactory dbFactory) : IDriverSettlementRepository
{
    public async Task<PagedResult<SettlementDto>> GetPagedAsync(
        int tenantId, int page, int pageSize, int? driverId, string? status,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var where = "s.TenantId = @TenantId AND s.IsDeleted = 0";
        if (driverId.HasValue) where += " AND s.DriverId = @DriverId";
        if (!string.IsNullOrWhiteSpace(status)) where += " AND s.Status = @Status";

        var parameters = new
        {
            TenantId = tenantId,
            DriverId = driverId,
            Status = status,
            Offset = (Math.Max(page, 1) - 1) * Math.Max(pageSize, 1),
            PageSize = Math.Max(pageSize, 1)
        };

        var total = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            $"SELECT COUNT(1) FROM DriverSettlements s WHERE {where}",
            parameters, cancellationToken: cancellationToken));

        var items = (await connection.QueryAsync<SettlementDto>(new CommandDefinition(
            $"""
            SELECT s.Id, s.DriverId, d.FullName AS DriverName, s.PeriodStart, s.PeriodEnd,
                   s.GrossCollected, s.FuelDeducted, s.AllowanceAmount, s.Adjustments, s.Payable,
                   s.Status, s.PaidAt, s.Reference, s.CreatedAt
            FROM DriverSettlements s
            LEFT JOIN Drivers d ON d.Id = s.DriverId AND d.TenantId = s.TenantId
            WHERE {where}
            ORDER BY s.PeriodEnd DESC, s.Id DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
            """,
            parameters, cancellationToken: cancellationToken))).ToList();

        return new PagedResult<SettlementDto>
        {
            Items = items,
            TotalCount = total,
            Page = Math.Max(page, 1),
            PageSize = Math.Max(pageSize, 1)
        };
    }

    public async Task<SettlementDetailDto?> GetByIdAsync(int id, int tenantId, CancellationToken cancellationToken = default)
    {
        var header = await GetHeaderAsync(id, tenantId, cancellationToken);
        if (header is null) return null;

        using var connection = dbFactory.CreateConnection();
        var lines = (await connection.QueryAsync<SettlementLineDto>(new CommandDefinition("""
            SELECT Id, LineType, Description, Amount, ReferenceId
            FROM DriverSettlementLines
            WHERE SettlementId = @Id AND TenantId = @TenantId
            ORDER BY Id
            """, new { Id = id, TenantId = tenantId }, cancellationToken: cancellationToken))).ToList();

        return header with { Lines = lines };
    }

    public async Task<bool> DriverExistsAsync(int driverId, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT CASE WHEN EXISTS(SELECT 1 FROM Drivers WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0) THEN 1 ELSE 0 END",
            new { Id = driverId, TenantId = tenantId }, cancellationToken: cancellationToken)) == 1;
    }

    public async Task<SettlementTotals> CalculateTotalsAsync(
        int tenantId, int driverId, DateTime periodStart, DateTime periodEnd,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var start = periodStart.Date;
        var end = periodEnd.Date.AddDays(1).AddTicks(-1);

        var gross = await connection.ExecuteScalarAsync<decimal>(new CommandDefinition("""
            SELECT ISNULL(SUM(p.Amount), 0)
            FROM Payments p
            INNER JOIN Bookings b ON b.Id = p.BookingId
            WHERE b.DriverId = @DriverId AND b.TenantId = @TenantId AND p.TenantId = @TenantId
              AND b.IsDeleted = 0 AND p.IsDeleted = 0
              AND p.Status IN (@Paid, @Partial)
              AND b.PickupTime BETWEEN @From AND @To
            """, new
            {
                DriverId = driverId,
                TenantId = tenantId,
                From = start,
                To = end,
                Paid = (int)PaymentStatus.Paid,
                Partial = (int)PaymentStatus.PartiallyPaid
            }, cancellationToken: cancellationToken));

        var fuel = await connection.ExecuteScalarAsync<decimal>(new CommandDefinition("""
            SELECT ISNULL(SUM(TotalCost), 0)
            FROM FuelLogs
            WHERE DriverId = @DriverId AND TenantId = @TenantId AND IsDeleted = 0
              AND COALESCE(FuelDate, CreatedAt) BETWEEN @From AND @To
            """, new { DriverId = driverId, TenantId = tenantId, From = start, To = end },
            cancellationToken: cancellationToken));

        // Period allowance MVP: FixedAmount once + PerDay × inclusive days (route/vehicle rules stay on Calculate API).
        var days = Math.Max(1, (periodEnd.Date - periodStart.Date).Days + 1);
        var fixedSum = await connection.ExecuteScalarAsync<decimal>(new CommandDefinition("""
            SELECT ISNULL(SUM(Value), 0)
            FROM DriverAllowanceRules
            WHERE TenantId = @TenantId AND IsDeleted = 0 AND IsActive = 1
              AND CalculationType = @Fixed
            """, new { TenantId = tenantId, Fixed = (int)AllowanceCalculationType.FixedAmount },
            cancellationToken: cancellationToken));
        var perDay = await connection.ExecuteScalarAsync<decimal>(new CommandDefinition("""
            SELECT ISNULL(SUM(Value), 0)
            FROM DriverAllowanceRules
            WHERE TenantId = @TenantId AND IsDeleted = 0 AND IsActive = 1
              AND CalculationType = @PerDay
            """, new { TenantId = tenantId, PerDay = (int)AllowanceCalculationType.PerDay },
            cancellationToken: cancellationToken));

        return new SettlementTotals
        {
            GrossCollected = gross,
            FuelDeducted = fuel,
            AllowanceAmount = Math.Round(fixedSum + (perDay * days), 2)
        };
    }

    public async Task<IReadOnlyList<SettlementLineInput>> BuildLinesAsync(
        int tenantId, int driverId, DateTime periodStart, DateTime periodEnd,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var start = periodStart.Date;
        var end = periodEnd.Date.AddDays(1).AddTicks(-1);

        var paymentLines = (await connection.QueryAsync<(int Id, decimal Amount, string? BookingNumber)>(new CommandDefinition("""
            SELECT p.Id, p.Amount, b.BookingNumber
            FROM Payments p
            INNER JOIN Bookings b ON b.Id = p.BookingId
            WHERE b.DriverId = @DriverId AND b.TenantId = @TenantId AND p.TenantId = @TenantId
              AND b.IsDeleted = 0 AND p.IsDeleted = 0
              AND p.Status IN (@Paid, @Partial)
              AND b.PickupTime BETWEEN @From AND @To
            ORDER BY p.Id
            """, new
            {
                DriverId = driverId,
                TenantId = tenantId,
                From = start,
                To = end,
                Paid = (int)PaymentStatus.Paid,
                Partial = (int)PaymentStatus.PartiallyPaid
            }, cancellationToken: cancellationToken))).ToList();

        var fuelLines = (await connection.QueryAsync<(int Id, decimal TotalCost, string? Station)>(new CommandDefinition("""
            SELECT Id, TotalCost, Station
            FROM FuelLogs
            WHERE DriverId = @DriverId AND TenantId = @TenantId AND IsDeleted = 0
              AND COALESCE(FuelDate, CreatedAt) BETWEEN @From AND @To
            ORDER BY Id
            """, new { DriverId = driverId, TenantId = tenantId, From = start, To = end },
            cancellationToken: cancellationToken))).ToList();

        var lines = new List<SettlementLineInput>();
        foreach (var p in paymentLines)
        {
            lines.Add(new SettlementLineInput
            {
                LineType = "Collected",
                Description = string.IsNullOrWhiteSpace(p.BookingNumber)
                    ? $"Payment #{p.Id}"
                    : $"Payment #{p.Id} ({p.BookingNumber})",
                Amount = p.Amount,
                ReferenceId = p.Id
            });
        }

        foreach (var f in fuelLines)
        {
            lines.Add(new SettlementLineInput
            {
                LineType = "Fuel",
                Description = string.IsNullOrWhiteSpace(f.Station)
                    ? $"Fuel receipt #{f.Id}"
                    : $"Fuel #{f.Id} @ {f.Station}",
                Amount = -f.TotalCost,
                ReferenceId = f.Id
            });
        }

        return lines;
    }

    public async Task<int> CreateDraftAsync(
        int tenantId, int? createdBy, int driverId,
        DateTime periodStart, DateTime periodEnd,
        SettlementTotals totals, decimal adjustments, decimal payable,
        IReadOnlyList<SettlementLineInput> lines,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();

        var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            INSERT INTO DriverSettlements
                (TenantId, DriverId, PeriodStart, PeriodEnd, GrossCollected, FuelDeducted,
                 AllowanceAmount, Adjustments, Payable, Status, CreatedAt, CreatedBy, IsDeleted)
            VALUES
                (@TenantId, @DriverId, @PeriodStart, @PeriodEnd, @Gross, @Fuel,
                 @Allowance, @Adjustments, @Payable, N'Draft', SYSUTCDATETIME(), @CreatedBy, 0);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """, new
            {
                TenantId = tenantId,
                DriverId = driverId,
                PeriodStart = periodStart.Date,
                PeriodEnd = periodEnd.Date,
                Gross = totals.GrossCollected,
                Fuel = totals.FuelDeducted,
                Allowance = totals.AllowanceAmount,
                Adjustments = adjustments,
                Payable = payable,
                CreatedBy = createdBy
            }, transaction: tx, cancellationToken: cancellationToken));

        foreach (var line in lines)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO DriverSettlementLines
                    (SettlementId, TenantId, LineType, Description, Amount, ReferenceId, CreatedAt)
                VALUES
                    (@SettlementId, @TenantId, @LineType, @Description, @Amount, @ReferenceId, SYSUTCDATETIME());
                """, new
                {
                    SettlementId = id,
                    TenantId = tenantId,
                    line.LineType,
                    line.Description,
                    line.Amount,
                    line.ReferenceId
                }, transaction: tx, cancellationToken: cancellationToken));
        }

        if (totals.AllowanceAmount != 0)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO DriverSettlementLines
                    (SettlementId, TenantId, LineType, Description, Amount, ReferenceId, CreatedAt)
                VALUES
                    (@SettlementId, @TenantId, N'Allowance', N'Period allowance', @Amount, NULL, SYSUTCDATETIME());
                """, new { SettlementId = id, TenantId = tenantId, Amount = totals.AllowanceAmount },
                transaction: tx, cancellationToken: cancellationToken));
        }

        if (adjustments != 0)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO DriverSettlementLines
                    (SettlementId, TenantId, LineType, Description, Amount, ReferenceId, CreatedAt)
                VALUES
                    (@SettlementId, @TenantId, N'Adjustment', N'Manual adjustment', @Amount, NULL, SYSUTCDATETIME());
                """, new { SettlementId = id, TenantId = tenantId, Amount = adjustments },
                transaction: tx, cancellationToken: cancellationToken));
        }

        tx.Commit();
        return id;
    }

    public async Task<SettlementDetailDto?> GetHeaderAsync(int id, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<SettlementHeaderRow>(new CommandDefinition("""
            SELECT s.Id, s.DriverId, d.FullName AS DriverName, s.PeriodStart, s.PeriodEnd,
                   s.GrossCollected, s.FuelDeducted, s.AllowanceAmount, s.Adjustments, s.Payable,
                   s.Status, s.PaidAt, s.Reference, s.Notes, s.CreatedAt, s.UpdatedAt
            FROM DriverSettlements s
            LEFT JOIN Drivers d ON d.Id = s.DriverId AND d.TenantId = s.TenantId
            WHERE s.Id = @Id AND s.TenantId = @TenantId AND s.IsDeleted = 0
            """, new { Id = id, TenantId = tenantId }, cancellationToken: cancellationToken));

        if (row is null) return null;

        return new SettlementDetailDto(
            row.Id, row.DriverId, row.DriverName, row.PeriodStart, row.PeriodEnd,
            row.GrossCollected, row.FuelDeducted, row.AllowanceAmount, row.Adjustments, row.Payable,
            row.Status, row.PaidAt, row.Reference, row.Notes, row.CreatedAt, row.UpdatedAt,
            Array.Empty<SettlementLineDto>());
    }

    public async Task ApproveAsync(int id, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE DriverSettlements
            SET Status = N'Approved', UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0 AND Status = N'Draft'
            """, new { Id = id, TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    public async Task MarkPaidAsync(int id, int tenantId, string? reference, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE DriverSettlements
            SET Status = N'Paid', PaidAt = SYSUTCDATETIME(), Reference = COALESCE(@Reference, Reference),
                UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0
              AND Status IN (N'Draft', N'Approved')
            """, new { Id = id, TenantId = tenantId, Reference = reference },
            cancellationToken: cancellationToken));
    }

    public async Task AdjustAsync(
        int id, int tenantId, decimal adjustments, decimal payable, string? notes,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE DriverSettlements
            SET Adjustments = @Adjustments, Payable = @Payable,
                Notes = COALESCE(@Notes, Notes), UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0 AND Status = N'Draft'
            """, new { Id = id, TenantId = tenantId, Adjustments = adjustments, Payable = payable, Notes = notes },
            cancellationToken: cancellationToken));
    }

    private sealed class SettlementHeaderRow
    {
        public int Id { get; init; }
        public int DriverId { get; init; }
        public string? DriverName { get; init; }
        public DateTime PeriodStart { get; init; }
        public DateTime PeriodEnd { get; init; }
        public decimal GrossCollected { get; init; }
        public decimal FuelDeducted { get; init; }
        public decimal AllowanceAmount { get; init; }
        public decimal Adjustments { get; init; }
        public decimal Payable { get; init; }
        public string Status { get; init; } = "";
        public DateTime? PaidAt { get; init; }
        public string? Reference { get; init; }
        public string? Notes { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime? UpdatedAt { get; init; }
    }
}
