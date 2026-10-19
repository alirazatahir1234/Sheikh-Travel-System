using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;

namespace SheikhTravelSystem.Application.Features.Settlements.Commands;

public record ApproveSettlementCommand(int Id) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Approve";
    public string AuditEntityName => "DriverSettlement";
    public int? AuditEntityId => Id;
}

public class ApproveSettlementCommandValidator : AbstractValidator<ApproveSettlementCommand>
{
    public ApproveSettlementCommandValidator() => RuleFor(x => x.Id).GreaterThan(0);
}

public class ApproveSettlementCommandHandler(
    IDriverSettlementRepository settlementRepository,
    ITenantContext tenantContext)
    : IRequestHandler<ApproveSettlementCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(ApproveSettlementCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var settlement = await settlementRepository.GetHeaderAsync(request.Id, tenantId, cancellationToken)
            ?? throw new NotFoundException("Settlement", request.Id);

        if (!string.Equals(settlement.Status, "Draft", StringComparison.OrdinalIgnoreCase))
            return ApiResponse<bool>.FailResponse("Only draft settlements can be approved.");

        await settlementRepository.ApproveAsync(request.Id, tenantId, cancellationToken);
        return ApiResponse<bool>.SuccessResponse(true, "Settlement approved.");
    }
}
