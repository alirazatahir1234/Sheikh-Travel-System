using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
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

public class ListPartsQueryHandler(IMaintenanceModuleRepository maintenanceModuleRepository)
    : IRequestHandler<ListPartsQuery, ApiResponse<IReadOnlyList<PartDto>>>
{
    public Task<ApiResponse<IReadOnlyList<PartDto>>> Handle(ListPartsQuery request, CancellationToken cancellationToken)
        => maintenanceModuleRepository.ListPartsAsync(request, cancellationToken);
}

public class GetPartsInventoryStatsQueryHandler(IMaintenanceModuleRepository maintenanceModuleRepository)
    : IRequestHandler<GetPartsInventoryStatsQuery, ApiResponse<PartsInventoryStatsDto>>
{
    public Task<ApiResponse<PartsInventoryStatsDto>> Handle(GetPartsInventoryStatsQuery request, CancellationToken cancellationToken)
        => maintenanceModuleRepository.GetPartsInventoryStatsAsync(request, cancellationToken);
}

public class CreatePartCommandHandler(IMaintenanceModuleRepository maintenanceModuleRepository)
    : IRequestHandler<CreatePartCommand, ApiResponse<int>>
{
    public Task<ApiResponse<int>> Handle(CreatePartCommand request, CancellationToken cancellationToken)
        => maintenanceModuleRepository.CreatePartAsync(request, cancellationToken);
}

public class AddPartStockCommandHandler(IMaintenanceModuleRepository maintenanceModuleRepository)
    : IRequestHandler<AddPartStockCommand, ApiResponse<bool>>
{
    public Task<ApiResponse<bool>> Handle(AddPartStockCommand request, CancellationToken cancellationToken)
        => maintenanceModuleRepository.AddPartStockAsync(request, cancellationToken);
}

public class IssuePartCommandHandler(IMaintenanceModuleRepository maintenanceModuleRepository)
    : IRequestHandler<IssuePartCommand, ApiResponse<int>>
{
    public Task<ApiResponse<int>> Handle(IssuePartCommand request, CancellationToken cancellationToken)
        => maintenanceModuleRepository.IssuePartAsync(request, cancellationToken);
}

public class TransferPartStockCommandHandler(IMaintenanceModuleRepository maintenanceModuleRepository)
    : IRequestHandler<TransferPartStockCommand, ApiResponse<bool>>
{
    public Task<ApiResponse<bool>> Handle(TransferPartStockCommand request, CancellationToken cancellationToken)
        => maintenanceModuleRepository.TransferPartStockAsync(request, cancellationToken);
}

public class RecordPartUsageCommandHandler(IMaintenanceModuleRepository maintenanceModuleRepository)
    : IRequestHandler<RecordPartUsageCommand, ApiResponse<int>>
{
    public Task<ApiResponse<int>> Handle(RecordPartUsageCommand request, CancellationToken cancellationToken)
        => maintenanceModuleRepository.RecordPartUsageAsync(request, cancellationToken);
}
