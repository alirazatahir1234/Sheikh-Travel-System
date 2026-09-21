using MediatR;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Application.Features.MaintenanceModule;

public record ListVendorsQuery : IRequest<ApiResponse<IReadOnlyList<VendorDto>>>;

public record GetVendorByIdQuery(int Id) : IRequest<ApiResponse<VendorDto>>;

public class ListVendorsQueryHandler(IMaintenanceModuleRepository maintenanceModuleRepository)
    : IRequestHandler<ListVendorsQuery, ApiResponse<IReadOnlyList<VendorDto>>>
{
    public Task<ApiResponse<IReadOnlyList<VendorDto>>> Handle(ListVendorsQuery request, CancellationToken cancellationToken)
        => maintenanceModuleRepository.ListVendorsAsync(request, cancellationToken);
}

public class GetVendorByIdQueryHandler(IMaintenanceModuleRepository maintenanceModuleRepository)
    : IRequestHandler<GetVendorByIdQuery, ApiResponse<VendorDto>>
{
    public Task<ApiResponse<VendorDto>> Handle(GetVendorByIdQuery request, CancellationToken cancellationToken)
        => maintenanceModuleRepository.GetVendorByIdAsync(request, cancellationToken);
}
