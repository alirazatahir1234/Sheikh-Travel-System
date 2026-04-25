using Dapper;
using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
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

public class CreateFuelLogCommandHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<CreateFuelLogCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(CreateFuelLogCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var dto = request.FuelLog;
        var totalCost = dto.Liters * dto.PricePerLiter;

        var id = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                @"INSERT INTO FuelLogs (VehicleId, DriverId, Liters, PricePerLiter, TotalCost,
                  OdometerReading, FuelType, FuelDate, Station, CreatedAt, IsDeleted)
                  VALUES (@VehicleId, @DriverId, @Liters, @PricePerLiter, @TotalCost,
                  @OdometerReading, @FuelType, @FuelDate, @Station, @CreatedAt, 0);
                  SELECT SCOPE_IDENTITY();",
                new
                {
                    dto.VehicleId, dto.DriverId, dto.Liters, dto.PricePerLiter, TotalCost = totalCost,
                    dto.OdometerReading, FuelType = (int)dto.FuelType, dto.FuelDate,
                    dto.Station, CreatedAt = DateTime.UtcNow
                },
                cancellationToken: cancellationToken));

        return ApiResponse<int>.SuccessResponse(id, "Fuel log created successfully.");
    }
}
