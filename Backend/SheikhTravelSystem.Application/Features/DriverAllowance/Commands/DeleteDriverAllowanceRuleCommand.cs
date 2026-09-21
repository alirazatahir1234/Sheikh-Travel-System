using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;

namespace SheikhTravelSystem.Application.Features.DriverAllowance.Commands;

public record DeleteDriverAllowanceRuleCommand(int Id) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Delete";
    public string AuditEntityName => "DriverAllowanceRule";
    public int? AuditEntityId => Id;
}

public class DeleteDriverAllowanceRuleCommandHandler(IDriverAllowanceRepository repository)
    : IRequestHandler<DeleteDriverAllowanceRuleCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(
        DeleteDriverAllowanceRuleCommand request, CancellationToken cancellationToken)
    {
        if (!await repository.ExistsAsync(request.Id, cancellationToken))
            throw new NotFoundException("DriverAllowanceRule", request.Id);

        await repository.SoftDeleteAsync(request.Id, cancellationToken);

        return ApiResponse<bool>.SuccessResponse(true, "Driver allowance rule deleted successfully.");
    }
}
