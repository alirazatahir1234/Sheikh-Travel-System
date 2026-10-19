using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;

namespace SheikhTravelSystem.Application.Features.Settlements.Commands;

public record GenerateDraftSettlementCommand(
    int DriverId,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    decimal Adjustments = 0
) : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => "Create";
    public string AuditEntityName => "DriverSettlement";
    public int? AuditEntityId => null;
}

public class GenerateDraftSettlementCommandValidator : AbstractValidator<GenerateDraftSettlementCommand>
{
    public GenerateDraftSettlementCommandValidator()
    {
        RuleFor(x => x.DriverId).GreaterThan(0);
        RuleFor(x => x.PeriodEnd).GreaterThanOrEqualTo(x => x.PeriodStart);
    }
}

public class GenerateDraftSettlementCommandHandler(
    IDriverSettlementRepository settlementRepository,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<GenerateDraftSettlementCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(
        GenerateDraftSettlementCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        if (!await settlementRepository.DriverExistsAsync(request.DriverId, tenantId, cancellationToken))
            throw new NotFoundException("Driver", request.DriverId);

        var totals = await settlementRepository.CalculateTotalsAsync(
            tenantId, request.DriverId, request.PeriodStart, request.PeriodEnd, cancellationToken);

        var payable = totals.GrossCollected - totals.FuelDeducted + totals.AllowanceAmount + request.Adjustments;
        var lines = await settlementRepository.BuildLinesAsync(
            tenantId, request.DriverId, request.PeriodStart, request.PeriodEnd, cancellationToken);

        var id = await settlementRepository.CreateDraftAsync(
            tenantId,
            currentUser.UserId,
            request.DriverId,
            request.PeriodStart,
            request.PeriodEnd,
            totals,
            request.Adjustments,
            payable,
            lines,
            cancellationToken);

        return ApiResponse<int>.SuccessResponse(id, "Settlement draft generated.");
    }
}
