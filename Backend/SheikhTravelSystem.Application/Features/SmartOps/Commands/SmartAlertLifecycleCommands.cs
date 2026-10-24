using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Application.Features.SmartOps.Commands;

public record AcknowledgeSmartAlertCommand(int TenantId, long Id, string Actor)
    : IRequest<ApiResponse<SmartAlertDto>>;

public class AcknowledgeSmartAlertCommandHandler(ISmartAlertService smartAlerts)
    : IRequestHandler<AcknowledgeSmartAlertCommand, ApiResponse<SmartAlertDto>>
{
    public async Task<ApiResponse<SmartAlertDto>> Handle(AcknowledgeSmartAlertCommand request, CancellationToken cancellationToken)
    {
        var alert = await smartAlerts.AcknowledgeAlertAsync(request.TenantId, request.Id, request.Actor, cancellationToken);
        return alert is null
            ? ApiResponse<SmartAlertDto>.FailResponse("Alert not found or not actionable.")
            : ApiResponse<SmartAlertDto>.SuccessResponse(alert, "Alert acknowledged.");
    }
}

public record ResolveSmartAlertCommand(int TenantId, long Id, string Actor)
    : IRequest<ApiResponse<SmartAlertDto>>;

public class ResolveSmartAlertCommandHandler(ISmartAlertService smartAlerts)
    : IRequestHandler<ResolveSmartAlertCommand, ApiResponse<SmartAlertDto>>
{
    public async Task<ApiResponse<SmartAlertDto>> Handle(ResolveSmartAlertCommand request, CancellationToken cancellationToken)
    {
        var alert = await smartAlerts.ResolveAlertAsync(request.TenantId, request.Id, request.Actor, cancellationToken);
        return alert is null
            ? ApiResponse<SmartAlertDto>.FailResponse("Alert not found or not actionable.")
            : ApiResponse<SmartAlertDto>.SuccessResponse(alert, "Alert resolved.");
    }
}

public record DismissSmartAlertCommand(int TenantId, long Id, string Actor)
    : IRequest<ApiResponse<SmartAlertDto>>;

public class DismissSmartAlertCommandHandler(ISmartAlertService smartAlerts)
    : IRequestHandler<DismissSmartAlertCommand, ApiResponse<SmartAlertDto>>
{
    public async Task<ApiResponse<SmartAlertDto>> Handle(DismissSmartAlertCommand request, CancellationToken cancellationToken)
    {
        var alert = await smartAlerts.DismissAlertAsync(request.TenantId, request.Id, request.Actor, cancellationToken);
        return alert is null
            ? ApiResponse<SmartAlertDto>.FailResponse("Alert not found or not actionable.")
            : ApiResponse<SmartAlertDto>.SuccessResponse(alert, "Alert dismissed.");
    }
}
