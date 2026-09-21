using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.FuelLogs.DTOs;

namespace SheikhTravelSystem.Application.Features.FuelLogs.Commands;

public record UpdateFuelLogCommand(int Id, CreateFuelLogDto FuelLog) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Update";
    public string AuditEntityName => "FuelLog";
    public int? AuditEntityId => Id;
}

public class UpdateFuelLogCommandValidator : AbstractValidator<UpdateFuelLogCommand>
{
    public UpdateFuelLogCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.FuelLog.VehicleId).GreaterThan(0);
        RuleFor(x => x.FuelLog.Liters).GreaterThan(0);
        RuleFor(x => x.FuelLog.PricePerLiter).GreaterThan(0);
        RuleFor(x => x.FuelLog.OdometerReading).GreaterThan(0);
    }
}

public class UpdateFuelLogCommandHandler(IFuelLogRepository fuelLogRepository)
    : IRequestHandler<UpdateFuelLogCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdateFuelLogCommand request, CancellationToken cancellationToken)
    {
        var rowsAffected = await fuelLogRepository.UpdateAsync(request.Id, request.FuelLog, cancellationToken);

        if (rowsAffected == 0)
            throw new NotFoundException("FuelLog", request.Id);

        return ApiResponse<bool>.SuccessResponse(true, "Fuel log updated successfully.");
    }
}
