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
        RuleFor(x => x.Vehicle.SeatingCapacity).GreaterThan(0);
    }
}

public class UpdateVehicleCommandHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<UpdateVehicleCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdateVehicleCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var dto = request.Vehicle;
        var tenantId = tenantContext.GetRequiredTenantId();

        var exists = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT CASE WHEN EXISTS(SELECT 1 FROM Vehicles WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0) THEN 1 ELSE 0 END",
                new { request.Id, TenantId = tenantId },
                cancellationToken: cancellationToken));

        if (!exists)
            throw new NotFoundException("Vehicle", request.Id);

        var regConflict = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                @"SELECT CASE WHEN EXISTS(
                    SELECT 1 FROM Vehicles
                    WHERE RegistrationNumber = @Reg AND Id != @Id AND TenantId = @TenantId AND IsDeleted = 0
                  ) THEN 1 ELSE 0 END",
                new { Reg = dto.RegistrationNumber.Trim(), request.Id, TenantId = tenantId },
                cancellationToken: cancellationToken));

        if (regConflict)
            throw new ConflictException($"Registration '{dto.RegistrationNumber}' is already in use.");

        var rows = await connection.ExecuteAsync(
            new CommandDefinition(
                @"UPDATE Vehicles SET Name = @Name, RegistrationNumber = @RegistrationNumber,
                  VehicleCode = @VehicleCode, VIN = @VIN, Make = @Make, Model = @Model, Year = @Year,
                  Color = @Color, VehicleType = @VehicleType,
                  SeatingCapacity = @SeatingCapacity, FuelAverage = @FuelAverage, FuelType = @FuelType,
                  EngineNo = @EngineNo, ChassisNo = @ChassisNo,
                  CurrentMileage = @CurrentMileage, InsuranceExpiryDate = @InsuranceExpiryDate,
                  PurchaseDate = @PurchaseDate, PurchasePrice = @PurchasePrice,
                  BranchId = @BranchId, DepartmentId = @DepartmentId, Status = @Status,
                  UpdatedAt = @UpdatedAt
                  WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
                new
                {
                    dto.Name, dto.RegistrationNumber, dto.VehicleCode, dto.VIN, dto.Make, dto.Model, dto.Year,
                    dto.Color, dto.VehicleType, dto.SeatingCapacity, dto.FuelAverage,
                    FuelType = (int)dto.FuelType, dto.EngineNo, dto.ChassisNo,
                    dto.CurrentMileage, dto.InsuranceExpiryDate, dto.PurchaseDate, dto.PurchasePrice,
                    dto.BranchId, dto.DepartmentId, Status = (int)dto.Status,
                    UpdatedAt = DateTime.UtcNow, request.Id, TenantId = tenantId
                },
                cancellationToken: cancellationToken));

        if (rows == 0)
            throw new NotFoundException("Vehicle", request.Id);

        return ApiResponse<bool>.SuccessResponse(true, "Vehicle updated successfully.");
    }
}
