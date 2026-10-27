using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Drivers.DTOs;

namespace SheikhTravelSystem.Application.Features.Drivers.Queries;

public record GetDriverProviderVerificationChecksQuery(int DriverId, int Limit = 30)
    : IRequest<ApiResponse<IReadOnlyList<DriverProviderVerificationCheckDto>>>;

public class GetDriverProviderVerificationChecksQueryHandler(
    IDriverRepository driverRepository,
    ITenantContext tenantContext)
    : IRequestHandler<GetDriverProviderVerificationChecksQuery, ApiResponse<IReadOnlyList<DriverProviderVerificationCheckDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<DriverProviderVerificationCheckDto>>> Handle(
        GetDriverProviderVerificationChecksQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        if (!await driverRepository.ExistsAsync(tenantId, request.DriverId, cancellationToken))
            return ApiResponse<IReadOnlyList<DriverProviderVerificationCheckDto>>.FailResponse("Driver not found.");

        var rows = await driverRepository.GetProviderVerificationChecksAsync(
            tenantId, request.DriverId, request.Limit, cancellationToken);
        return ApiResponse<IReadOnlyList<DriverProviderVerificationCheckDto>>.SuccessResponse(rows);
    }
}

public record GetDriverVerificationConfigStatusQuery : IRequest<ApiResponse<DriverVerificationConfigStatusDto>>;

public class GetDriverVerificationConfigStatusQueryHandler(IDriverVerificationOrchestrator orchestrator)
    : IRequestHandler<GetDriverVerificationConfigStatusQuery, ApiResponse<DriverVerificationConfigStatusDto>>
{
    public Task<ApiResponse<DriverVerificationConfigStatusDto>> Handle(
        GetDriverVerificationConfigStatusQuery request, CancellationToken cancellationToken)
    {
        var nadraMissing = orchestrator.DescribeMissingNadraConfiguration();
        var uaeMissing = orchestrator.DescribeMissingUaeKycConfiguration();
        var dto = new DriverVerificationConfigStatusDto(
            NadraEnabled: nadraMissing.Count == 0,
            NadraMissingKeys: nadraMissing,
            UaeKycEnabled: uaeMissing.Count == 0,
            UaeKycMissingKeys: uaeMissing);
        return Task.FromResult(ApiResponse<DriverVerificationConfigStatusDto>.SuccessResponse(dto));
    }
}
