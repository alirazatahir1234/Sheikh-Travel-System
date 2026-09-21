using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;

namespace SheikhTravelSystem.Application.Features.FuelLogs.Commands;

public record DeleteFuelLogCommand(int Id) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Delete";
    public string AuditEntityName => "FuelLog";
    public int? AuditEntityId => Id;
}

public class DeleteFuelLogCommandValidator : AbstractValidator<DeleteFuelLogCommand>
{
    public DeleteFuelLogCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
    }
}

public class DeleteFuelLogCommandHandler(IFuelLogRepository fuelLogRepository)
    : IRequestHandler<DeleteFuelLogCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(DeleteFuelLogCommand request, CancellationToken cancellationToken)
    {
        var rowsAffected = await fuelLogRepository.DeleteAsync(request.Id, cancellationToken);

        if (rowsAffected == 0)
            throw new NotFoundException("FuelLog", request.Id);

        return ApiResponse<bool>.SuccessResponse(true, "Fuel log deleted successfully.");
    }
}
