using Dapper;
using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Application.Features.MaintenanceModule;

public record ListPartsQuery(string? Search) : IRequest<ApiResponse<IReadOnlyList<PartDto>>>;

public record GetPartsInventoryStatsQuery() : IRequest<ApiResponse<PartsInventoryStatsDto>>;

public record CreatePartCommand(CreatePartDto Body) : IRequest<ApiResponse<int>>;

public record AddPartStockCommand(int PartId, AddPartStockDto Body) : IRequest<ApiResponse<bool>>;

public record IssuePartCommand(int PartId, IssuePartDto Body) : IRequest<ApiResponse<int>>;

public record TransferPartStockCommand(int PartId, TransferPartStockDto Body) : IRequest<ApiResponse<bool>>;

public record RecordPartUsageCommand(int WorkOrderId, int PartId, int Quantity) : IRequest<ApiResponse<int>>;

public class ListPartsQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<ListPartsQuery, ApiResponse<IReadOnlyList<PartDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<PartDto>>> Handle(ListPartsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        using var connection = dbFactory.CreateConnection();

        var sql = """
            SELECT p.Id, p.PartNumber, p.PartName, p.Category, p.Brand, p.Supplier, p.UnitCost, p.MinStockLevel,
                   ISNULL(inv.StockQuantity, 0) AS StockQuantity,
                   p.VehicleCompatibilityJson,
                   inv.Location
            FROM Parts p
            LEFT JOIN (
                SELECT PartId, SUM(StockQuantity) AS StockQuantity,
                       MIN(NULLIF(Location, '')) AS Location
                FROM PartInventory
                WHERE TenantId = @TenantId
                GROUP BY PartId
            ) inv ON inv.PartId = p.Id
            WHERE p.IsDeleted = 0 AND p.TenantId = @TenantId
            """;

        if (!string.IsNullOrWhiteSpace(request.Search))
            sql += " AND (p.PartNumber LIKE @Search OR p.PartName LIKE @Search)";

        sql += " ORDER BY p.PartName";

        var search = string.IsNullOrWhiteSpace(request.Search) ? null : $"%{request.Search.Trim()}%";
        var rows = await connection.QueryAsync<PartRow>(new CommandDefinition(
            sql, new { TenantId = tenantId, Search = search }, cancellationToken: cancellationToken));

        return ApiResponse<IReadOnlyList<PartDto>>.SuccessResponse(rows.Select(r => r.ToDto()).ToList());
    }
}

public class GetPartsInventoryStatsQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<GetPartsInventoryStatsQuery, ApiResponse<PartsInventoryStatsDto>>
{
    public async Task<ApiResponse<PartsInventoryStatsDto>> Handle(
        GetPartsInventoryStatsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        using var connection = dbFactory.CreateConnection();

        var stats = await connection.QuerySingleAsync<PartsInventoryStatsDto>(new CommandDefinition("""
            SELECT
                COUNT(*) AS TotalParts,
                SUM(CASE WHEN ISNULL(inv.StockQuantity, 0) > 0
                          AND ISNULL(inv.StockQuantity, 0) < p.MinStockLevel THEN 1 ELSE 0 END) AS LowStock,
                SUM(CASE WHEN ISNULL(inv.StockQuantity, 0) <= 0 THEN 1 ELSE 0 END) AS OutOfStock,
                ISNULL(SUM(ISNULL(inv.StockQuantity, 0) * p.UnitCost), 0) AS InventoryValue
            FROM Parts p
            LEFT JOIN (
                SELECT PartId, SUM(StockQuantity) AS StockQuantity
                FROM PartInventory
                WHERE TenantId = @TenantId
                GROUP BY PartId
            ) inv ON inv.PartId = p.Id
            WHERE p.IsDeleted = 0 AND p.TenantId = @TenantId
            """, new { TenantId = tenantId }, cancellationToken: cancellationToken));

        return ApiResponse<PartsInventoryStatsDto>.SuccessResponse(stats);
    }
}

public class CreatePartCommandHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<CreatePartCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(CreatePartCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var body = request.Body;
        using var connection = dbFactory.CreateConnection();

        var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            INSERT INTO Parts (TenantId, PartNumber, PartName, Category, Brand, Supplier, UnitCost, MinStockLevel, VehicleCompatibilityJson)
            VALUES (@TenantId, @PartNumber, @PartName, @Category, @Brand, @Supplier, @UnitCost, @MinStockLevel, @VehicleCompatibilityJson);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """, new
        {
            TenantId = tenantId,
            body.PartNumber,
            body.PartName,
            body.Category,
            body.Brand,
            body.Supplier,
            body.UnitCost,
            body.MinStockLevel,
            VehicleCompatibilityJson = PartStockHelper.SerializeCompatibility(body.VehicleCompatibility)
        }, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO PartInventory (TenantId, PartId, StockQuantity, Location)
            VALUES (@TenantId, @PartId, @Stock, @Location)
            """, new
        {
            TenantId = tenantId,
            PartId = id,
            Stock = body.InitialStock,
            Location = body.Location
        }, cancellationToken: cancellationToken));

        return ApiResponse<int>.SuccessResponse(id);
    }
}

public class AddPartStockCommandHandler(
    IDbConnectionFactory dbFactory, ITenantContext tenantContext, ICurrentUserService currentUser)
    : IRequestHandler<AddPartStockCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(AddPartStockCommand request, CancellationToken cancellationToken)
    {
        if (request.Body.Quantity <= 0)
            throw new ValidationException("Quantity must be greater than zero.");

        var tenantId = tenantContext.GetRequiredTenantId();
        using var connection = dbFactory.CreateConnection();

        var exists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM Parts WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
            new { Id = request.PartId, TenantId = tenantId }, cancellationToken: cancellationToken));
        if (exists == 0)
            throw new NotFoundException("Part", request.PartId);

        var invId = await connection.ExecuteScalarAsync<int?>(new CommandDefinition("""
            SELECT TOP 1 Id FROM PartInventory
            WHERE PartId = @PartId AND TenantId = @TenantId
            ORDER BY Id
            """, new { PartId = request.PartId, TenantId = tenantId }, cancellationToken: cancellationToken));

        if (invId is null)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO PartInventory (TenantId, PartId, StockQuantity, Location)
                VALUES (@TenantId, @PartId, @Qty, @Location)
                """, new
            {
                TenantId = tenantId,
                PartId = request.PartId,
                Qty = request.Body.Quantity,
                Location = request.Body.Location
            }, cancellationToken: cancellationToken));
        }
        else
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE PartInventory
                SET StockQuantity = StockQuantity + @Qty,
                    Location = COALESCE(@Location, Location),
                    UpdatedAt = GETUTCDATE()
                WHERE Id = @Id
                """, new
            {
                Id = invId,
                Qty = request.Body.Quantity,
                Location = request.Body.Location
            }, cancellationToken: cancellationToken));
        }

        var createdBy = currentUser.UserId?.ToString() ?? "system";
        await PartStockHelper.InsertMovementAsync(
            connection, tenantId, request.PartId, "Receipt", request.Body.Quantity,
            null, request.Body.Location, null, null, request.Body.Notes, createdBy, cancellationToken);

        return ApiResponse<bool>.SuccessResponse(true);
    }
}

public class IssuePartCommandHandler(
    IDbConnectionFactory dbFactory, ITenantContext tenantContext, ICurrentUserService currentUser)
    : IRequestHandler<IssuePartCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(IssuePartCommand request, CancellationToken cancellationToken)
    {
        if (request.Body.Quantity <= 0)
            throw new ValidationException("Quantity must be greater than zero.");

        var tenantId = tenantContext.GetRequiredTenantId();
        using var connection = dbFactory.CreateConnection();

        var part = await connection.QuerySingleOrDefaultAsync<(decimal UnitCost, string PartName)>(
            new CommandDefinition(
                "SELECT UnitCost, PartName FROM Parts WHERE Id = @PartId AND TenantId = @TenantId AND IsDeleted = 0",
                new { PartId = request.PartId, TenantId = tenantId }, cancellationToken: cancellationToken));

        if (string.IsNullOrEmpty(part.PartName))
            throw new NotFoundException("Part", request.PartId);

        var vehicleExists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM Vehicles WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
            new { Id = request.Body.VehicleId, TenantId = tenantId }, cancellationToken: cancellationToken));
        if (vehicleExists == 0)
            throw new NotFoundException("Vehicle", request.Body.VehicleId);

        if (request.Body.WorkOrderId is int woId)
        {
            var woExists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT COUNT(1) FROM WorkOrders WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
                new { Id = woId, TenantId = tenantId }, cancellationToken: cancellationToken));
            if (woExists == 0)
                throw new NotFoundException("WorkOrder", woId);
        }

        var stock = await PartStockHelper.GetTotalStockAsync(connection, tenantId, request.PartId, cancellationToken);
        if (stock < request.Body.Quantity)
            throw new ValidationException("Insufficient stock for this issue.");

        var total = part.UnitCost * request.Body.Quantity;
        var createdBy = currentUser.UserId?.ToString() ?? "system";

        var usageId = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            INSERT INTO PartUsage (TenantId, VehicleId, WorkOrderId, PartId, Quantity, UnitCost, CreatedBy)
            VALUES (@TenantId, @VehicleId, @WorkOrderId, @PartId, @Quantity, @UnitCost, @CreatedBy);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """, new
        {
            TenantId = tenantId,
            request.Body.VehicleId,
            request.Body.WorkOrderId,
            PartId = request.PartId,
            request.Body.Quantity,
            part.UnitCost,
            CreatedBy = createdBy
        }, cancellationToken: cancellationToken));

        await DeductStockAsync(connection, tenantId, request.PartId, request.Body.Quantity, cancellationToken);

        if (request.Body.WorkOrderId is int workOrderId)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE WorkOrders SET PartsCost = PartsCost + @Total, UpdatedAt = GETUTCDATE() WHERE Id = @Id",
                new { Total = total, Id = workOrderId }, cancellationToken: cancellationToken));
        }

        await PartStockHelper.InsertMovementAsync(
            connection, tenantId, request.PartId, "Issue", request.Body.Quantity,
            null, null, request.Body.VehicleId, request.Body.WorkOrderId, request.Body.Notes, createdBy, cancellationToken);

        var newStock = await PartStockHelper.GetTotalStockAsync(connection, tenantId, request.PartId, cancellationToken);
        var minStock = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT MinStockLevel FROM Parts WHERE Id = @PartId",
            new { PartId = request.PartId }, cancellationToken: cancellationToken));

        await PartStockHelper.MaybeInsertLowStockAlertAsync(
            connection, tenantId, request.PartId, part.PartName, newStock, minStock, cancellationToken);

        return ApiResponse<int>.SuccessResponse(usageId);
    }

    private static async Task DeductStockAsync(
        System.Data.IDbConnection connection, int tenantId, int partId, int quantity, CancellationToken ct)
    {
        var remaining = quantity;
        var rows = await connection.QueryAsync<(int Id, int StockQuantity)>(new CommandDefinition("""
            SELECT Id, StockQuantity FROM PartInventory
            WHERE PartId = @PartId AND TenantId = @TenantId AND StockQuantity > 0
            ORDER BY Id
            """, new { PartId = partId, TenantId = tenantId }, cancellationToken: ct));

        foreach (var row in rows)
        {
            if (remaining <= 0) break;
            var deduct = Math.Min(remaining, row.StockQuantity);
            await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE PartInventory SET StockQuantity = StockQuantity - @Deduct, UpdatedAt = GETUTCDATE() WHERE Id = @Id",
                new { Deduct = deduct, row.Id }, cancellationToken: ct));
            remaining -= deduct;
        }
    }
}

public class TransferPartStockCommandHandler(
    IDbConnectionFactory dbFactory, ITenantContext tenantContext, ICurrentUserService currentUser)
    : IRequestHandler<TransferPartStockCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(TransferPartStockCommand request, CancellationToken cancellationToken)
    {
        var body = request.Body;
        if (body.Quantity <= 0)
            throw new ValidationException("Quantity must be greater than zero.");
        if (string.IsNullOrWhiteSpace(body.ToLocation))
            throw new ValidationException("Destination location is required.");

        var tenantId = tenantContext.GetRequiredTenantId();
        using var connection = dbFactory.CreateConnection();

        var partExists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM Parts WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
            new { Id = request.PartId, TenantId = tenantId }, cancellationToken: cancellationToken));
        if (partExists == 0)
            throw new NotFoundException("Part", request.PartId);

        var fromLocation = body.FromLocation?.Trim() ?? string.Empty;
        var toLocation = body.ToLocation.Trim();

        var source = await connection.QuerySingleOrDefaultAsync<(int Id, int StockQuantity, string? Location)>(
            new CommandDefinition("""
                SELECT TOP 1 Id, StockQuantity, Location FROM PartInventory
                WHERE PartId = @PartId AND TenantId = @TenantId
                  AND ISNULL(Location, '') = @FromLocation
                ORDER BY Id
                """, new { PartId = request.PartId, TenantId = tenantId, FromLocation = fromLocation },
                cancellationToken: cancellationToken));

        if (source.Id == 0 || source.StockQuantity < body.Quantity)
            throw new ValidationException("Insufficient stock at the source location.");

        if (body.Quantity == source.StockQuantity && string.Equals(fromLocation, toLocation, StringComparison.OrdinalIgnoreCase))
            return ApiResponse<bool>.SuccessResponse(true);

        if (body.Quantity == source.StockQuantity)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE PartInventory SET Location = @ToLocation, UpdatedAt = GETUTCDATE() WHERE Id = @Id",
                new { ToLocation = toLocation, source.Id }, cancellationToken: cancellationToken));
        }
        else
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE PartInventory SET StockQuantity = StockQuantity - @Qty, UpdatedAt = GETUTCDATE() WHERE Id = @Id",
                new { Qty = body.Quantity, source.Id }, cancellationToken: cancellationToken));

            var dest = await connection.QuerySingleOrDefaultAsync<int?>(new CommandDefinition("""
                SELECT TOP 1 Id FROM PartInventory
                WHERE PartId = @PartId AND TenantId = @TenantId AND ISNULL(Location, '') = @ToLocation
                """, new { PartId = request.PartId, TenantId = tenantId, ToLocation = toLocation },
                cancellationToken: cancellationToken));

            if (dest is null)
            {
                await connection.ExecuteAsync(new CommandDefinition("""
                    INSERT INTO PartInventory (TenantId, PartId, StockQuantity, Location)
                    VALUES (@TenantId, @PartId, @Qty, @ToLocation)
                    """, new { TenantId = tenantId, PartId = request.PartId, Qty = body.Quantity, ToLocation = toLocation },
                    cancellationToken: cancellationToken));
            }
            else
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    "UPDATE PartInventory SET StockQuantity = StockQuantity + @Qty, UpdatedAt = GETUTCDATE() WHERE Id = @Id",
                    new { Qty = body.Quantity, Id = dest }, cancellationToken: cancellationToken));
            }
        }

        var createdBy = currentUser.UserId?.ToString() ?? "system";
        await PartStockHelper.InsertMovementAsync(
            connection, tenantId, request.PartId, "Transfer", body.Quantity,
            string.IsNullOrEmpty(fromLocation) ? null : fromLocation,
            toLocation, null, null, body.Notes, createdBy, cancellationToken);

        return ApiResponse<bool>.SuccessResponse(true);
    }
}

public class RecordPartUsageCommandHandler(
    IDbConnectionFactory dbFactory, ITenantContext tenantContext, ICurrentUserService currentUser)
    : IRequestHandler<RecordPartUsageCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(RecordPartUsageCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        using var connection = dbFactory.CreateConnection();

        var wo = await connection.QuerySingleOrDefaultAsync<int>(
            new CommandDefinition(
                "SELECT VehicleId FROM WorkOrders WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
                new { Id = request.WorkOrderId, TenantId = tenantId }, cancellationToken: cancellationToken));

        if (wo == 0)
            throw new NotFoundException("WorkOrder", request.WorkOrderId);

        var issueHandler = new IssuePartCommandHandler(dbFactory, tenantContext, currentUser);
        return await issueHandler.Handle(
            new IssuePartCommand(request.PartId, new IssuePartDto(wo, request.Quantity, request.WorkOrderId, null)),
            cancellationToken);
    }
}
