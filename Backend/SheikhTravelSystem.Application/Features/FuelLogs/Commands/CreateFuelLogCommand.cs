using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.FuelLogs.DTOs;

namespace SheikhTravelSystem.Application.Features.FuelLogs.Commands;

public record CreateFuelLogCommand(CreateFuelLogDto FuelLog) : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => "Create";
    public string AuditEntityName => "FuelLog";
    public int? AuditEntityId => null;
}

public class CreateFuelLogCommandValidator : AbstractValidator<CreateFuelLogCommand>
{
    public CreateFuelLogCommandValidator()
    {
        RuleFor(x => x.FuelLog.VehicleId).GreaterThan(0);
        RuleFor(x => x.FuelLog.Liters).GreaterThan(0);
        RuleFor(x => x.FuelLog.PricePerLiter).GreaterThan(0);
        RuleFor(x => x.FuelLog.OdometerReading).GreaterThan(0);
    }
}

public class CreateFuelLogCommandHandler(IFuelLogRepository fuelLogRepository)
    : IRequestHandler<CreateFuelLogCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(CreateFuelLogCommand request, CancellationToken cancellationToken)
    {
        var id = await fuelLogRepository.CreateAsync(request.FuelLog, cancellationToken);
        return ApiResponse<int>.SuccessResponse(id, "Fuel log created successfully.");
    }
}
