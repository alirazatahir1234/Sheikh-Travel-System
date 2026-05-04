using Dapper;
using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
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

public class UpdateFuelLogCommandHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<UpdateFuelLogCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdateFuelLogCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var dto = request.FuelLog;
        var totalCost = dto.Liters * dto.PricePerLiter;

        var rowsAffected = await connection.ExecuteAsync(
            new CommandDefinition(
                @"UPDATE FuelLogs 
                  SET VehicleId = @VehicleId, DriverId = @DriverId, Liters = @Liters,
                      PricePerLiter = @PricePerLiter, TotalCost = @TotalCost,
                      OdometerReading = @OdometerReading, FuelType = @FuelType,
                      FuelDate = @FuelDate, Station = @Station, UpdatedAt = @UpdatedAt
                  WHERE Id = @Id AND IsDeleted = 0",
                new
                {
                    request.Id,
                    dto.VehicleId, dto.DriverId, dto.Liters, dto.PricePerLiter, TotalCost = totalCost,
                    dto.OdometerReading, FuelType = (int)dto.FuelType, dto.FuelDate,
                    dto.Station, UpdatedAt = DateTime.UtcNow
                },
                cancellationToken: cancellationToken));

        if (rowsAffected == 0)
            throw new NotFoundException("FuelLog", request.Id);

        return ApiResponse<bool>.SuccessResponse(true, "Fuel log updated successfully.");
    }
}
