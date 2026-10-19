using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;

namespace SheikhTravelSystem.Application.Features.Settlements.Commands;

public record MarkSettlementPaidCommand(int Id, string? Reference = null)
    : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "MarkPaid";
    public string AuditEntityName => "DriverSettlement";
    public int? AuditEntityId => Id;
}

public class MarkSettlementPaidCommandValidator : AbstractValidator<MarkSettlementPaidCommand>
{
    public MarkSettlementPaidCommandValidator() => RuleFor(x => x.Id).GreaterThan(0);
}

public class MarkSettlementPaidCommandHandler(
    IDriverSettlementRepository settlementRepository,
    ITenantContext tenantContext)
    : IRequestHandler<MarkSettlementPaidCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(
        MarkSettlementPaidCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var settlement = await settlementRepository.GetHeaderAsync(request.Id, tenantId, cancellationToken)
            ?? throw new NotFoundException("Settlement", request.Id);

        if (string.Equals(settlement.Status, "Paid", StringComparison.OrdinalIgnoreCase))
            return ApiResponse<bool>.SuccessResponse(true, "Settlement is already paid.");

        if (!string.Equals(settlement.Status, "Approved", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(settlement.Status, "Draft", StringComparison.OrdinalIgnoreCase))
            return ApiResponse<bool>.FailResponse("Settlement cannot be marked paid in its current status.");

        await settlementRepository.MarkPaidAsync(request.Id, tenantId, request.Reference, cancellationToken);
        return ApiResponse<bool>.SuccessResponse(true, "Settlement marked as paid.");
    }
}
