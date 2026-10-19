using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;

namespace SheikhTravelSystem.Application.Features.Settlements.Commands;

public record AdjustSettlementCommand(int Id, decimal Adjustments, string? Notes = null)
    : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Adjust";
    public string AuditEntityName => "DriverSettlement";
    public int? AuditEntityId => Id;
}

public class AdjustSettlementCommandValidator : AbstractValidator<AdjustSettlementCommand>
{
    public AdjustSettlementCommandValidator() => RuleFor(x => x.Id).GreaterThan(0);
}

public class AdjustSettlementCommandHandler(
    IDriverSettlementRepository settlementRepository,
    ITenantContext tenantContext)
    : IRequestHandler<AdjustSettlementCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(
        AdjustSettlementCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var settlement = await settlementRepository.GetHeaderAsync(request.Id, tenantId, cancellationToken)
            ?? throw new NotFoundException("Settlement", request.Id);

        if (!string.Equals(settlement.Status, "Draft", StringComparison.OrdinalIgnoreCase))
            return ApiResponse<bool>.FailResponse("Only draft settlements can be adjusted.");

        var payable = settlement.GrossCollected - settlement.FuelDeducted
            + settlement.AllowanceAmount + request.Adjustments;

        await settlementRepository.AdjustAsync(
            request.Id, tenantId, request.Adjustments, payable, request.Notes, cancellationToken);

        return ApiResponse<bool>.SuccessResponse(true, "Settlement adjusted.");
    }
}
