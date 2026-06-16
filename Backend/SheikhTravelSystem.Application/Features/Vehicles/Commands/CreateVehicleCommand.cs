using Dapper;
using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Vehicles.DTOs;

namespace SheikhTravelSystem.Application.Features.Vehicles.Commands;

public record CreateVehicleCommand(CreateVehicleDto Vehicle, bool SaveAsDraft = false) : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => "Create";
    public string AuditEntityName => "Vehicle";
    public int? AuditEntityId => null;
}

public class CreateVehicleCommandValidator : AbstractValidator<CreateVehicleCommand>
{
    public CreateVehicleCommandValidator()
    {
        When(x => !x.SaveAsDraft, () =>
        {
            RuleFor(x => x.Vehicle.Name).NotEmpty().MaximumLength(100);
            RuleFor(x => x.Vehicle.RegistrationNumber).NotEmpty().MaximumLength(20);
            RuleFor(x => x.Vehicle.FuelAverage).GreaterThan(0);
            RuleFor(x => x.Vehicle.SeatingCapacity).GreaterThan(0);
        });
        RuleFor(x => x.Vehicle.VehicleCode).MaximumLength(40).When(x => x.Vehicle.VehicleCode != null);
        RuleFor(x => x.Vehicle.VIN).MaximumLength(64).When(x => x.Vehicle.VIN != null);
    }
}

public class CreateVehicleCommandHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<CreateVehicleCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(CreateVehicleCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var dto = request.Vehicle;
        var tenantId = tenantContext.GetRequiredTenantId();

        var registration = string.IsNullOrWhiteSpace(dto.RegistrationNumber)
            ? $"DRAFT-{tenantId}-{DateTime.UtcNow.Ticks}"
            : dto.RegistrationNumber.Trim();

        if (!request.SaveAsDraft || !registration.StartsWith("DRAFT-", StringComparison.Ordinal))
        {
            var exists = await connection.ExecuteScalarAsync<bool>(
                new CommandDefinition(
                    "SELECT CASE WHEN EXISTS(SELECT 1 FROM Vehicles WHERE RegistrationNumber = @Reg AND IsDeleted = 0 AND TenantId = @TenantId) THEN 1 ELSE 0 END",
                    new { Reg = registration, TenantId = tenantId },
                    cancellationToken: cancellationToken));

            if (exists)
                throw new ConflictException($"Vehicle with registration '{registration}' already exists.");
        }

        var status = request.SaveAsDraft
            ? Domain.Enums.VehicleStatus.Draft
            : Domain.Enums.VehicleStatus.Available;

        var name = string.IsNullOrWhiteSpace(dto.Name) ? "Draft Vehicle" : dto.Name.Trim();
        var seating = dto.SeatingCapacity > 0 ? dto.SeatingCapacity : 1;
        var fuelAverage = dto.FuelAverage > 0 ? dto.FuelAverage : 1m;

        var id = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                @"INSERT INTO Vehicles (TenantId, Name, RegistrationNumber, VehicleCode, VIN, Make, Model, Year,
                  Color, VehicleType, SeatingCapacity, FuelAverage, FuelType, EngineNo, ChassisNo,
                  CurrentMileage, InsuranceExpiryDate, PurchaseDate, PurchasePrice, BranchId, DepartmentId,
                  Status, CreatedAt, IsDeleted)
                  VALUES (@TenantId, @Name, @RegistrationNumber, @VehicleCode, @VIN, @Make, @Model, @Year,
                  @Color, @VehicleType, @SeatingCapacity, @FuelAverage, @FuelType, @EngineNo, @ChassisNo,
                  @CurrentMileage, @InsuranceExpiryDate, @PurchaseDate, @PurchasePrice, @BranchId, @DepartmentId,
                  @Status, @CreatedAt, 0);
                  SELECT SCOPE_IDENTITY();",
                new
                {
                    TenantId = tenantId,
                    Name = name,
                    RegistrationNumber = registration,
                    dto.VehicleCode, dto.VIN, dto.Make, dto.Model, dto.Year,
                    dto.Color, dto.VehicleType,
                    SeatingCapacity = seating,
                    FuelAverage = fuelAverage,
                    FuelType = (int)dto.FuelType, dto.EngineNo, dto.ChassisNo,
                    dto.CurrentMileage, dto.InsuranceExpiryDate, dto.PurchaseDate, dto.PurchasePrice,
                    dto.BranchId, dto.DepartmentId,
                    Status = (int)status,
                    CreatedAt = DateTime.UtcNow
                },
                cancellationToken: cancellationToken));

        var message = request.SaveAsDraft ? "Vehicle draft saved." : "Vehicle created successfully.";
        return ApiResponse<int>.SuccessResponse(id, message);
    }
}
