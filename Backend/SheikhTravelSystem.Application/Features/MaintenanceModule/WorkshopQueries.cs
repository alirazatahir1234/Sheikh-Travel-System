using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Application.Features.MaintenanceModule;

public record ListWorkshopsQuery : IRequest<ApiResponse<IReadOnlyList<WorkshopDto>>>;

public record CreateWorkshopCommand(CreateWorkshopDto Body) : IRequest<ApiResponse<int>>;

public class ListWorkshopsQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<ListWorkshopsQuery, ApiResponse<IReadOnlyList<WorkshopDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<WorkshopDto>>> Handle(ListWorkshopsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        using var connection = dbFactory.CreateConnection();

        var rows = await connection.QueryAsync<WorkshopDto>(new CommandDefinition($"""
            SELECT {MaintenanceSql.WorkshopSelect}
            FROM Workshops w
            WHERE w.IsDeleted = 0 AND w.TenantId = @TenantId
            ORDER BY w.Name
            """, new { TenantId = tenantId }, cancellationToken: cancellationToken));

        return ApiResponse<IReadOnlyList<WorkshopDto>>.SuccessResponse(rows.ToList());
    }
}

public class CreateWorkshopCommandHandler(
    IDbConnectionFactory dbFactory,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateWorkshopCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(CreateWorkshopCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var body = request.Body;
        using var connection = dbFactory.CreateConnection();

        var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            INSERT INTO Workshops
                (TenantId, Name, WorkshopType, Location, ContactPerson, ContactPhone, ContactEmail,
                 Capacity, VendorType, ContractDetails, SLA, Rating, CreatedBy, CreatedAt)
            VALUES
                (@TenantId, @Name, @WorkshopType, @Location, @ContactPerson, @ContactPhone, @ContactEmail,
                 @Capacity, @VendorType, @ContractDetails, @SLA, @Rating, @CreatedBy, GETUTCDATE());
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """, new
        {
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
            CreatedBy = currentUser.UserId?.ToString() ?? "system"
        }, cancellationToken: cancellationToken));

        return ApiResponse<int>.SuccessResponse(id);
    }
}

public record ListServiceTypesQuery : IRequest<ApiResponse<IReadOnlyList<ServiceTypeDto>>>;

public class ListServiceTypesQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<ListServiceTypesQuery, ApiResponse<IReadOnlyList<ServiceTypeDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<ServiceTypeDto>>> Handle(ListServiceTypesQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        using var connection = dbFactory.CreateConnection();

        var rows = await connection.QueryAsync<ServiceTypeDto>(new CommandDefinition("""
            SELECT Id, Code, Name, IsPreventive FROM ServiceTypes
            WHERE IsDeleted = 0 AND (TenantId IS NULL OR TenantId = @TenantId) AND IsActive = 1
            ORDER BY SortOrder, Name
            """, new { TenantId = tenantId }, cancellationToken: cancellationToken));

        return ApiResponse<IReadOnlyList<ServiceTypeDto>>.SuccessResponse(rows.ToList());
    }
}
