using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Drivers.DTOs;

namespace SheikhTravelSystem.Application.Features.Drivers.Commands;

public record TriggerDriverProviderVerificationCommand(int DriverId, string? DocumentType = null)
    : IRequest<ApiResponse<IReadOnlyList<DriverProviderVerificationCheckDto>>>, IAuditableCommand
{
    public string AuditAction => "Verify";
    public string AuditEntityName => "Driver";
    public int? AuditEntityId => DriverId;
}

public class TriggerDriverProviderVerificationCommandHandler(
    IDriverVerificationOrchestrator orchestrator,
    ITenantContext tenantContext,
    IDriverRepository driverRepository)
    : IRequestHandler<TriggerDriverProviderVerificationCommand, ApiResponse<IReadOnlyList<DriverProviderVerificationCheckDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<DriverProviderVerificationCheckDto>>> Handle(
        TriggerDriverProviderVerificationCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        if (!await driverRepository.ExistsAsync(tenantId, request.DriverId, cancellationToken))
            return ApiResponse<IReadOnlyList<DriverProviderVerificationCheckDto>>.FailResponse("Driver not found.");

        var results = await orchestrator.RunForDriverAsync(
            tenantId, request.DriverId, request.DocumentType, manualTrigger: true, cancellationToken);

        var history = await driverRepository.GetProviderVerificationChecksAsync(
            tenantId, request.DriverId, limit: 20, cancellationToken);

        return ApiResponse<IReadOnlyList<DriverProviderVerificationCheckDto>>.SuccessResponse(
            history,
            results.Any(r => r.Status == "NotConfigured")
                ? "Provider verification attempted; some integrations are not configured — see missing keys."
                : "Provider verification completed.");
    }
}
