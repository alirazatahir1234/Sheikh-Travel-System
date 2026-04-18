using Dapper;
using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Vehicles.DTOs;

namespace SheikhTravelSystem.Application.Features.Vehicles.Commands;

public record CreateVehicleCommand(CreateVehicleDto Vehicle) : IRequest<ApiResponse<int>>;

public class CreateVehicleCommandValidator : AbstractValidator<CreateVehicleCommand>
{
    public CreateVehicleCommandValidator()
    {
        RuleFor(x => x.Vehicle.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Vehicle.RegistrationNumber).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Vehicle.FuelAverage).GreaterThan(0);
        RuleFor(x => x.Vehicle.SeatingCapacity).GreaterThan(0);
    }
}

public class CreateVehicleCommandHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<CreateVehicleCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(CreateVehicleCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var dto = request.Vehicle;

        var exists = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT CASE WHEN EXISTS(SELECT 1 FROM Vehicles WHERE RegistrationNumber = @Reg AND IsDeleted = 0) THEN 1 ELSE 0 END",
                new { Reg = dto.RegistrationNumber },
                cancellationToken: cancellationToken));

        if (exists)
            throw new ConflictException($"Vehicle with registration '{dto.RegistrationNumber}' already exists.");

        var id = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                @"INSERT INTO Vehicles (Name, RegistrationNumber, Model, Year, SeatingCapacity, FuelAverage, FuelType,
                  CurrentMileage, InsuranceExpiryDate, Status, CreatedAt, IsDeleted)
                  VALUES (@Name, @RegistrationNumber, @Model, @Year, @SeatingCapacity, @FuelAverage, @FuelType,
                  @CurrentMileage, @InsuranceExpiryDate, @Status, @CreatedAt, 0);
                  SELECT SCOPE_IDENTITY();",
                new
                {
                    dto.Name, dto.RegistrationNumber, dto.Model, dto.Year, dto.SeatingCapacity,
                    dto.FuelAverage, FuelType = (int)dto.FuelType, dto.CurrentMileage,
                    dto.InsuranceExpiryDate, Status = (int)Domain.Enums.VehicleStatus.Available,
                    CreatedAt = DateTime.UtcNow
                },
                cancellationToken: cancellationToken));

        return ApiResponse<int>.SuccessResponse(id, "Vehicle created successfully.");
    }
}
