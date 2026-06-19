using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Drivers.DTOs;

namespace SheikhTravelSystem.Application.Features.Drivers.Queries;

public record CheckDriverAvailabilityQuery(
    string? Phone,
    string? Email,
    string? LicenseNumber,
    int? ExcludeDriverId = null) : IRequest<ApiResponse<DriverAvailabilityDto>>;

public class CheckDriverAvailabilityQueryHandler(
    IDbConnectionFactory dbFactory,
    ITenantContext tenantContext)
    : IRequestHandler<CheckDriverAvailabilityQuery, ApiResponse<DriverAvailabilityDto>>
{
    public async Task<ApiResponse<DriverAvailabilityDto>> Handle(
        CheckDriverAvailabilityQuery request,
        CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        var result = await DriverUniquenessHelper.CheckAvailabilityAsync(
            connection,
            tenantId,
            request.Phone,
            request.Email,
            request.LicenseNumber,
            request.ExcludeDriverId,
            cancellationToken);

        return ApiResponse<DriverAvailabilityDto>.SuccessResponse(result);
    }
}
