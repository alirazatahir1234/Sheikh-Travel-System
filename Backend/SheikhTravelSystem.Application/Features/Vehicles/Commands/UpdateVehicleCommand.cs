using Dapper;
using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Vehicles.DTOs;

namespace SheikhTravelSystem.Application.Features.Vehicles.Commands;

public record UpdateVehicleCommand(int Id, UpdateVehicleDto Vehicle) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Update";
    public string AuditEntityName => "Vehicle";
    public int? AuditEntityId => Id;
}

public class UpdateVehicleCommandValidator : AbstractValidator<UpdateVehicleCommand>
{
    public UpdateVehicleCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Vehicle.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Vehicle.RegistrationNumber).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Vehicle.FuelAverage).GreaterThan(0);
    }
}

public class UpdateVehicleCommandHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<UpdateVehicleCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdateVehicleCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var dto = request.Vehicle;

        var exists = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT CASE WHEN EXISTS(SELECT 1 FROM Vehicles WHERE Id = @Id AND IsDeleted = 0) THEN 1 ELSE 0 END",
                new { request.Id },
                cancellationToken: cancellationToken));

        if (!exists)
            throw new NotFoundException("Vehicle", request.Id);

        var regConflict = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT CASE WHEN EXISTS(SELECT 1 FROM Vehicles WHERE RegistrationNumber = @Reg AND Id != @Id AND IsDeleted = 0) THEN 1 ELSE 0 END",
                new { Reg = dto.RegistrationNumber, request.Id },
                cancellationToken: cancellationToken));

        if (regConflict)
            throw new ConflictException($"Registration '{dto.RegistrationNumber}' is already in use.");

        await connection.ExecuteAsync(
            new CommandDefinition(
                @"UPDATE Vehicles SET Name = @Name, RegistrationNumber = @RegistrationNumber, Model = @Model,
                  Year = @Year, SeatingCapacity = @SeatingCapacity, FuelAverage = @FuelAverage, FuelType = @FuelType,
                  CurrentMileage = @CurrentMileage, InsuranceExpiryDate = @InsuranceExpiryDate, Status = @Status,
                  UpdatedAt = @UpdatedAt WHERE Id = @Id",
                new
                {
                    dto.Name, dto.RegistrationNumber, dto.Model, dto.Year, dto.SeatingCapacity,
                    dto.FuelAverage, FuelType = (int)dto.FuelType, dto.CurrentMileage,
                    dto.InsuranceExpiryDate, Status = (int)dto.Status, UpdatedAt = DateTime.UtcNow, request.Id
                },
                cancellationToken: cancellationToken));

        return ApiResponse<bool>.SuccessResponse(true, "Vehicle updated successfully.");
    }
}
