using System.Text.Json;
using MediatR;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Application.Features.MaintenanceModule;

public record GetMaintenanceRequestStatsQuery : IRequest<ApiResponse<MaintenanceRequestStatsDto>>;

public record ApproveMaintenanceRequestCommand(int Id) : IRequest<ApiResponse<bool>>;

public record RejectMaintenanceRequestCommand(int Id, RejectMaintenanceRequestDto Body) : IRequest<ApiResponse<bool>>;

public record SearchMaintenanceQuery(string Q, int Limit = 15) : IRequest<ApiResponse<IReadOnlyList<MaintenanceSearchResultDto>>>;

public record DismissMaintenanceAlertCommand(int Id) : IRequest<ApiResponse<bool>>;

public record GetMaintenanceComplianceSummaryQuery : IRequest<ApiResponse<ComplianceSummaryDto>>;

public record UploadMaintenanceRequestAttachmentCommand(int RequestId, Stream FileStream, string FileName, string ContentType, long FileLength)
    : IRequest<ApiResponse<string>>;

public class GetMaintenanceRequestStatsQueryHandler(IMaintenanceModuleRepository maintenanceModuleRepository)
    : IRequestHandler<GetMaintenanceRequestStatsQuery, ApiResponse<MaintenanceRequestStatsDto>>
{
    public Task<ApiResponse<MaintenanceRequestStatsDto>> Handle(GetMaintenanceRequestStatsQuery request, CancellationToken cancellationToken)
        => maintenanceModuleRepository.GetMaintenanceRequestStatsAsync(request, cancellationToken);
}

public class ApproveMaintenanceRequestCommandHandler(IMaintenanceModuleRepository maintenanceModuleRepository)
    : IRequestHandler<ApproveMaintenanceRequestCommand, ApiResponse<bool>>
{
    public Task<ApiResponse<bool>> Handle(ApproveMaintenanceRequestCommand request, CancellationToken cancellationToken)
        => maintenanceModuleRepository.ApproveMaintenanceRequestAsync(request, cancellationToken);
}

public class RejectMaintenanceRequestCommandHandler(IMaintenanceModuleRepository maintenanceModuleRepository)
    : IRequestHandler<RejectMaintenanceRequestCommand, ApiResponse<bool>>
{
    public Task<ApiResponse<bool>> Handle(RejectMaintenanceRequestCommand request, CancellationToken cancellationToken)
        => maintenanceModuleRepository.RejectMaintenanceRequestAsync(request, cancellationToken);
}

public class SearchMaintenanceQueryHandler(IMaintenanceModuleRepository maintenanceModuleRepository)
    : IRequestHandler<SearchMaintenanceQuery, ApiResponse<IReadOnlyList<MaintenanceSearchResultDto>>>
{
    public Task<ApiResponse<IReadOnlyList<MaintenanceSearchResultDto>>> Handle(SearchMaintenanceQuery request, CancellationToken cancellationToken)
        => maintenanceModuleRepository.SearchMaintenanceAsync(request, cancellationToken);
}

public class DismissMaintenanceAlertCommandHandler(IMaintenanceModuleRepository maintenanceModuleRepository)
    : IRequestHandler<DismissMaintenanceAlertCommand, ApiResponse<bool>>
{
    public Task<ApiResponse<bool>> Handle(DismissMaintenanceAlertCommand request, CancellationToken cancellationToken)
        => maintenanceModuleRepository.DismissMaintenanceAlertAsync(request, cancellationToken);
}

public class GetMaintenanceComplianceSummaryQueryHandler(IMaintenanceModuleRepository maintenanceModuleRepository)
    : IRequestHandler<GetMaintenanceComplianceSummaryQuery, ApiResponse<ComplianceSummaryDto>>
{
    public Task<ApiResponse<ComplianceSummaryDto>> Handle(GetMaintenanceComplianceSummaryQuery request, CancellationToken cancellationToken)
        => maintenanceModuleRepository.GetMaintenanceComplianceSummaryAsync(request, cancellationToken);
}

public class UploadMaintenanceRequestAttachmentCommandHandler(IMaintenanceModuleRepository maintenanceModuleRepository)
    : IRequestHandler<UploadMaintenanceRequestAttachmentCommand, ApiResponse<string>>
{
    public Task<ApiResponse<string>> Handle(UploadMaintenanceRequestAttachmentCommand request, CancellationToken cancellationToken)
        => maintenanceModuleRepository.UploadMaintenanceRequestAttachmentAsync(request, cancellationToken);
}
