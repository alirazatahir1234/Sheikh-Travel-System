using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Drivers.DTOs;

namespace SheikhTravelSystem.Application.Features.Drivers.Queries;

public record GetDriverDocumentsQuery(int DriverId)
    : IRequest<ApiResponse<IReadOnlyList<DriverDocumentDetailedDto>>>;

public class GetDriverDocumentsQueryHandler(IDriverRepository driverRepository, ITenantContext tenantContext)
    : IRequestHandler<GetDriverDocumentsQuery, ApiResponse<IReadOnlyList<DriverDocumentDetailedDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<DriverDocumentDetailedDto>>> Handle(
        GetDriverDocumentsQuery request, CancellationToken cancellationToken)
    {
        var rows = await driverRepository.GetDocumentsAsync(
            tenantContext.GetRequiredTenantId(), request.DriverId, cancellationToken);
        return ApiResponse<IReadOnlyList<DriverDocumentDetailedDto>>.SuccessResponse(rows);
    }
}

public record GetDriverReviewNotesQuery(int DriverId)
    : IRequest<ApiResponse<IReadOnlyList<DriverReviewNoteDto>>>;

public class GetDriverReviewNotesQueryHandler(IDriverRepository driverRepository, ITenantContext tenantContext)
    : IRequestHandler<GetDriverReviewNotesQuery, ApiResponse<IReadOnlyList<DriverReviewNoteDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<DriverReviewNoteDto>>> Handle(
        GetDriverReviewNotesQuery request, CancellationToken cancellationToken)
    {
        var rows = await driverRepository.GetReviewNotesAsync(
            tenantContext.GetRequiredTenantId(), request.DriverId, cancellationToken);
        return ApiResponse<IReadOnlyList<DriverReviewNoteDto>>.SuccessResponse(rows);
    }
}
