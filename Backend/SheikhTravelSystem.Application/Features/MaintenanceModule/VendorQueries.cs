using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Application.Features.MaintenanceModule;

public record ListVendorsQuery : IRequest<ApiResponse<IReadOnlyList<VendorDto>>>;

public record GetVendorByIdQuery(int Id) : IRequest<ApiResponse<VendorDto>>;

public class ListVendorsQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<ListVendorsQuery, ApiResponse<IReadOnlyList<VendorDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<VendorDto>>> Handle(ListVendorsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        using var connection = dbFactory.CreateConnection();

        var rows = await connection.QueryAsync<VendorRow>(new CommandDefinition(
            $"""
            SELECT {MaintenanceSql.VendorSelect}
            FROM Vendors v
            WHERE v.IsDeleted = 0 AND v.TenantId = @TenantId
            ORDER BY v.Name
            """,
            new { TenantId = tenantId },
            cancellationToken: cancellationToken));

        return ApiResponse<IReadOnlyList<VendorDto>>.SuccessResponse(rows.Select(r => r.ToDto()).ToList());
    }
}

public class GetVendorByIdQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<GetVendorByIdQuery, ApiResponse<VendorDto>>
{
    public async Task<ApiResponse<VendorDto>> Handle(GetVendorByIdQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        using var connection = dbFactory.CreateConnection();

        var row = await connection.QuerySingleOrDefaultAsync<VendorRow>(new CommandDefinition(
            $"""
            SELECT {MaintenanceSql.VendorSelect}
            FROM Vendors v
            WHERE v.Id = @Id AND v.TenantId = @TenantId AND v.IsDeleted = 0
            """,
            new { request.Id, TenantId = tenantId },
            cancellationToken: cancellationToken));

        if (row is null)
            throw new NotFoundException("Vendor", request.Id);

        return ApiResponse<VendorDto>.SuccessResponse(row.ToDto());
    }
}
