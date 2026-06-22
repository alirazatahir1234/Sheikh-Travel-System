using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Application.Features.MaintenanceModule;

public record CreateVendorCommand(CreateVendorDto Body) : IRequest<ApiResponse<int>>;

public record UpdateVendorCommand(int Id, UpdateVendorDto Body) : IRequest<ApiResponse<bool>>;

public record SetVendorActiveCommand(int Id, bool IsActive) : IRequest<ApiResponse<bool>>;

public class CreateVendorCommandHandler(
    IDbConnectionFactory dbFactory,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateVendorCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(CreateVendorCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var body = request.Body;
        using var connection = dbFactory.CreateConnection();

        var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            INSERT INTO Vendors
                (TenantId, Name, Category, ContactPerson, ContactPhone, ContactEmail,
                 ProductsJson, Rating, IsPreferred, CreatedBy, CreatedAt)
            VALUES
                (@TenantId, @Name, @Category, @ContactPerson, @ContactPhone, @ContactEmail,
                 @ProductsJson, @Rating, @IsPreferred, @CreatedBy, GETUTCDATE());
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """, new
        {
            TenantId = tenantId,
            body.Name,
            body.Category,
            body.ContactPerson,
            body.ContactPhone,
            body.ContactEmail,
            ProductsJson = VendorHelper.SerializeProducts(body.Products),
            body.Rating,
            body.IsPreferred,
            CreatedBy = currentUser.UserId?.ToString() ?? "system"
        }, cancellationToken: cancellationToken));

        return ApiResponse<int>.SuccessResponse(id);
    }
}

public class UpdateVendorCommandHandler(
    IDbConnectionFactory dbFactory,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdateVendorCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdateVendorCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        using var connection = dbFactory.CreateConnection();

        var exists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM Vendors WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
            new { request.Id, TenantId = tenantId }, cancellationToken: cancellationToken));

        if (exists == 0) throw new NotFoundException("Vendor", request.Id);

        var body = request.Body;
        var rows = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE Vendors SET
                Name = COALESCE(@Name, Name),
                Category = COALESCE(@Category, Category),
                ContactPerson = COALESCE(@ContactPerson, ContactPerson),
                ContactPhone = COALESCE(@ContactPhone, ContactPhone),
                ContactEmail = COALESCE(@ContactEmail, ContactEmail),
                ProductsJson = COALESCE(@ProductsJson, ProductsJson),
                Rating = COALESCE(@Rating, Rating),
                IsPreferred = COALESCE(@IsPreferred, IsPreferred),
                IsActive = COALESCE(@IsActive, IsActive),
                UpdatedBy = @UpdatedBy,
                UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND TenantId = @TenantId
            """, new
        {
            request.Id,
            TenantId = tenantId,
            body.Name,
            body.Category,
            body.ContactPerson,
            body.ContactPhone,
            body.ContactEmail,
            ProductsJson = body.Products is null ? null : VendorHelper.SerializeProducts(body.Products),
            body.Rating,
            body.IsPreferred,
            body.IsActive,
            UpdatedBy = currentUser.UserId?.ToString() ?? "system"
        }, cancellationToken: cancellationToken));

        return ApiResponse<bool>.SuccessResponse(rows > 0);
    }
}

public class SetVendorActiveCommandHandler(
    IDbConnectionFactory dbFactory,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<SetVendorActiveCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(SetVendorActiveCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        using var connection = dbFactory.CreateConnection();

        var rows = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE Vendors SET IsActive = @IsActive, UpdatedBy = @UpdatedBy, UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0
            """, new
        {
            request.Id,
            TenantId = tenantId,
            request.IsActive,
            UpdatedBy = currentUser.UserId?.ToString() ?? "system"
        }, cancellationToken: cancellationToken));

        if (rows == 0) throw new NotFoundException("Vendor", request.Id);
        return ApiResponse<bool>.SuccessResponse(true);
    }
}
