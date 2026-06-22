using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Application.Features.MaintenanceModule;

public record GetWorkshopByIdQuery(int Id) : IRequest<ApiResponse<WorkshopDto>>;

public record UpdateWorkshopCommand(int Id, UpdateWorkshopDto Body) : IRequest<ApiResponse<bool>>;

public record SetWorkshopActiveCommand(int Id, bool IsActive) : IRequest<ApiResponse<bool>>;

public class GetWorkshopByIdQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<GetWorkshopByIdQuery, ApiResponse<WorkshopDto>>
{
    public async Task<ApiResponse<WorkshopDto>> Handle(GetWorkshopByIdQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        using var connection = dbFactory.CreateConnection();

        var row = await connection.QuerySingleOrDefaultAsync<WorkshopDto>(new CommandDefinition(
            $"""
            SELECT {MaintenanceSql.WorkshopSelect}
            FROM Workshops w
            WHERE w.Id = @Id AND w.TenantId = @TenantId AND w.IsDeleted = 0
            """,
            new { request.Id, TenantId = tenantId },
            cancellationToken: cancellationToken));

        if (row is null)
            throw new NotFoundException("Workshop", request.Id);

        return ApiResponse<WorkshopDto>.SuccessResponse(row);
    }
}

public class UpdateWorkshopCommandHandler(
    IDbConnectionFactory dbFactory,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdateWorkshopCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdateWorkshopCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        using var connection = dbFactory.CreateConnection();

        var exists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM Workshops WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
            new { request.Id, TenantId = tenantId }, cancellationToken: cancellationToken));

        if (exists == 0)
            throw new NotFoundException("Workshop", request.Id);

        var body = request.Body;
        var rows = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE Workshops SET
                Name = COALESCE(@Name, Name),
                WorkshopType = COALESCE(@WorkshopType, WorkshopType),
                Location = COALESCE(@Location, Location),
                ContactPerson = COALESCE(@ContactPerson, ContactPerson),
                ContactPhone = COALESCE(@ContactPhone, ContactPhone),
                ContactEmail = COALESCE(@ContactEmail, ContactEmail),
                Capacity = COALESCE(@Capacity, Capacity),
                VendorType = COALESCE(@VendorType, VendorType),
                ContractDetails = COALESCE(@ContractDetails, ContractDetails),
                SLA = COALESCE(@SLA, SLA),
                Rating = COALESCE(@Rating, Rating),
                IsActive = COALESCE(@IsActive, IsActive),
                UpdatedBy = @UpdatedBy,
                UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND TenantId = @TenantId
            """, new
        {
            request.Id,
            TenantId = tenantId,
            body.Name,
            body.WorkshopType,
            body.Location,
            body.ContactPerson,
            body.ContactPhone,
            body.ContactEmail,
            body.Capacity,
            body.VendorType,
            body.ContractDetails,
            body.SLA,
            body.Rating,
            body.IsActive,
            UpdatedBy = currentUser.UserId?.ToString() ?? "system"
        }, cancellationToken: cancellationToken));

        if (rows == 0) throw new NotFoundException("Workshop", request.Id);
        return ApiResponse<bool>.SuccessResponse(true);
    }
}

public class SetWorkshopActiveCommandHandler(
    IDbConnectionFactory dbFactory,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<SetWorkshopActiveCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(SetWorkshopActiveCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        using var connection = dbFactory.CreateConnection();

        var rows = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE Workshops SET IsActive = @IsActive, UpdatedBy = @UpdatedBy, UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0
            """, new
        {
            request.Id,
            TenantId = tenantId,
            request.IsActive,
            UpdatedBy = currentUser.UserId?.ToString() ?? "system"
        }, cancellationToken: cancellationToken));

        if (rows == 0) throw new NotFoundException("Workshop", request.Id);
        return ApiResponse<bool>.SuccessResponse(true);
    }
}
