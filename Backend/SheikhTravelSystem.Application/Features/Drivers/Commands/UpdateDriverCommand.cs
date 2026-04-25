using Dapper;
using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Drivers.DTOs;

namespace SheikhTravelSystem.Application.Features.Drivers.Commands;

public record UpdateDriverCommand(int Id, UpdateDriverDto Driver) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Update";
    public string AuditEntityName => "Driver";
    public int? AuditEntityId => Id;
}

public class UpdateDriverCommandValidator : AbstractValidator<UpdateDriverCommand>
{
    public UpdateDriverCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Driver.FullName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Driver.Phone).NotEmpty();
        RuleFor(x => x.Driver.LicenseNumber).NotEmpty();
        RuleFor(x => x.Driver.LicenseExpiryDate).GreaterThan(DateTime.UtcNow)
            .WithMessage("License must not be expired.");
    }
}

public class UpdateDriverCommandHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<UpdateDriverCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdateDriverCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var dto = request.Driver;

        var exists = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT CASE WHEN EXISTS(SELECT 1 FROM Drivers WHERE Id = @Id AND IsDeleted = 0) THEN 1 ELSE 0 END",
                new { request.Id },
                cancellationToken: cancellationToken));

        if (!exists)
            throw new NotFoundException("Driver", request.Id);

        var licenseConflict = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT CASE WHEN EXISTS(SELECT 1 FROM Drivers WHERE LicenseNumber = @License AND Id != @Id AND IsDeleted = 0) THEN 1 ELSE 0 END",
                new { License = dto.LicenseNumber, request.Id },
                cancellationToken: cancellationToken));

        if (licenseConflict)
            throw new ConflictException($"License '{dto.LicenseNumber}' is already in use.");

        await connection.ExecuteAsync(
            new CommandDefinition(
                @"UPDATE Drivers SET FullName = @FullName, Phone = @Phone, LicenseNumber = @LicenseNumber,
                  LicenseExpiryDate = @LicenseExpiryDate, CNIC = @CNIC, Address = @Address,
                  Status = @Status, IsActive = @IsActive, UpdatedAt = @UpdatedAt WHERE Id = @Id",
                new
                {
                    dto.FullName, dto.Phone, dto.LicenseNumber, dto.LicenseExpiryDate,
                    dto.CNIC, dto.Address, Status = (int)dto.Status, dto.IsActive,
                    UpdatedAt = DateTime.UtcNow, request.Id
                },
                cancellationToken: cancellationToken));

        return ApiResponse<bool>.SuccessResponse(true, "Driver updated successfully.");
    }
}
