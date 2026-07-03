using Dapper;
using FluentValidation;
using MediatR;
using Microsoft.Data.SqlClient;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Vehicles.DTOs;
using SheikhTravelSystem.Domain.Enums;
using static SheikhTravelSystem.Application.Features.Vehicles.VehicleDescriptiveTextRules;

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
        RuleFor(x => x.Vehicle.RegistrationNumber)
            .NotEmpty()
            .MaximumLength(20)
            .When(x => x.Vehicle.Status != VehicleStatus.Draft);
        RuleFor(x => x.Vehicle.RegistrationNumber)
            .MaximumLength(20)
            .When(x => x.Vehicle.Status == VehicleStatus.Draft);
        RuleFor(x => x.Vehicle.FuelAverage).GreaterThan(0);
        RuleFor(x => x.Vehicle.SeatingCapacity).GreaterThan(0);

        RuleFor(x => x.Vehicle.Name)
            .Must(IsValid)
            .When(x => !string.IsNullOrWhiteSpace(x.Vehicle.Name))
            .WithMessage("Vehicle name must be descriptive text, not numbers only.");
        RuleFor(x => x.Vehicle.Make)
            .Must(IsValid)
            .When(x => !string.IsNullOrWhiteSpace(x.Vehicle.Make))
            .WithMessage("Make must be a valid manufacturer name.");
        RuleFor(x => x.Vehicle.Model)
            .Must(IsValid)
            .When(x => !string.IsNullOrWhiteSpace(x.Vehicle.Model))
            .WithMessage("Model must be a valid model name.");
        RuleFor(x => x.Vehicle.Color)
            .Must(IsValid)
            .When(x => !string.IsNullOrWhiteSpace(x.Vehicle.Color))
            .WithMessage("Color must be a valid color name.");
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

        var registration = string.IsNullOrWhiteSpace(dto.RegistrationNumber)
            ? null
            : dto.RegistrationNumber.Trim();

        if (!string.IsNullOrWhiteSpace(registration))
        {
            var regConflict = await connection.ExecuteScalarAsync<bool>(
                new CommandDefinition(
                    @"SELECT CASE WHEN EXISTS(
                        SELECT 1 FROM Vehicles
                        WHERE RegistrationNumber = @Reg AND Id != @Id AND TenantId = @TenantId AND IsDeleted = 0
                      ) THEN 1 ELSE 0 END",
                    new { Reg = registration, request.Id, TenantId = tenantId },
                    cancellationToken: cancellationToken));

            if (regConflict)
                throw new ConflictException($"Registration '{registration}' is already in use.");
        }

        int rows;
        try
        {
            rows = await connection.ExecuteAsync(
                new CommandDefinition(
                    @"UPDATE Vehicles SET Name = @Name,
                      RegistrationNumber = COALESCE(@RegistrationNumber, RegistrationNumber),
                      VehicleCode = @VehicleCode, VIN = @VIN, Make = @Make, Model = @Model, Year = @Year,
                      Color = @Color, VehicleType = @VehicleType,
                      SeatingCapacity = @SeatingCapacity, FuelAverage = @FuelAverage, FuelType = @FuelType,
                      EngineNo = @EngineNo, ChassisNo = @ChassisNo,
                      CurrentMileage = @CurrentMileage, InsuranceExpiryDate = @InsuranceExpiryDate,
                      PurchaseDate = @PurchaseDate, PurchasePrice = @PurchasePrice,
                      PurchaseCurrencyCode = @PurchaseCurrencyCode,
                      BranchId = @BranchId, DepartmentId = @DepartmentId, Status = @Status,
                      UpdatedAt = @UpdatedAt
                      WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
                    new
                    {
                        dto.Name,
                        RegistrationNumber = registration,
                        dto.VehicleCode, dto.VIN, dto.Make, dto.Model, dto.Year,
                        dto.Color, dto.VehicleType, dto.SeatingCapacity, dto.FuelAverage,
                        FuelType = (int)dto.FuelType, dto.EngineNo, dto.ChassisNo,
                        dto.CurrentMileage, dto.InsuranceExpiryDate, dto.PurchaseDate, dto.PurchasePrice,
                        PurchaseCurrencyCode = string.IsNullOrWhiteSpace(dto.PurchaseCurrencyCode)
                            ? null
                            : dto.PurchaseCurrencyCode.Trim().ToUpperInvariant(),
                        dto.BranchId, dto.DepartmentId, Status = (int)dto.Status,
                        UpdatedAt = DateTime.UtcNow, request.Id, TenantId = tenantId
                    },
                    cancellationToken: cancellationToken));
        }
        catch (SqlException ex) when (ex.Number is 2627 or 2601
            && ex.Message.Contains("UQ_Vehicles_Registration", StringComparison.OrdinalIgnoreCase))
        {
            // The pre-check above only looks at active, same-tenant rows, but the DB constraint
            // doesn't share that scoping (e.g. a soft-deleted or cross-tenant vehicle can still
            // hold the registration number) — surface a proper conflict instead of a 500.
            throw new ConflictException($"Registration '{dto.RegistrationNumber}' is already in use.");
        }

        if (rows == 0)
            throw new NotFoundException("Vehicle", request.Id);

        return ApiResponse<bool>.SuccessResponse(true, "Vehicle updated successfully.");
    }
}
