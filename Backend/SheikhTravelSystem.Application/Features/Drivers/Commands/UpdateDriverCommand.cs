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
        RuleFor(x => x.Driver.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Driver.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Driver.Phone).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Driver.Email).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(x => x.Driver.LicenseNumber).NotEmpty().MaximumLength(30);
        RuleFor(x => x.Driver.DateOfBirth)
            .NotNull()
            .WithMessage("Date of birth is required.");
        RuleFor(x => x.Driver.DateOfBirth)
            .Must(dob => dob!.Value.Date < DateTime.UtcNow.Date)
            .WithMessage("Date of birth cannot be in the future.")
            .When(x => x.Driver.DateOfBirth.HasValue);
        RuleFor(x => x.Driver.DateOfBirth)
            .Must(dob => dob!.Value.Date <= DateTime.UtcNow.Date.AddYears(-18))
            .WithMessage("Driver must be at least 18 years old.")
            .When(x => x.Driver.DateOfBirth.HasValue);
        RuleFor(x => x.Driver.Gender).MaximumLength(20).When(x => !string.IsNullOrEmpty(x.Driver.Gender));
        RuleFor(x => x.Driver.EmergencyContactName).MaximumLength(100).When(x => !string.IsNullOrEmpty(x.Driver.EmergencyContactName));
        RuleFor(x => x.Driver.EmergencyContact).MaximumLength(200).When(x => !string.IsNullOrEmpty(x.Driver.EmergencyContact));
    }
}

public class UpdateDriverCommandHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<UpdateDriverCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdateDriverCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var dto = request.Driver;
        var tenantId = tenantContext.GetRequiredTenantId();
        var fullName = DriverFieldHelper.BuildFullName(dto.FirstName, dto.LastName);

        var exists = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT CASE WHEN EXISTS(SELECT 1 FROM Drivers WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0) THEN 1 ELSE 0 END",
                new { request.Id, TenantId = tenantId },
                cancellationToken: cancellationToken));

        if (!exists)
            throw new NotFoundException("Driver", request.Id);

        await DriverUniquenessHelper.EnsureUniqueAsync(
            connection, tenantId, dto.Phone, dto.Email, dto.LicenseNumber, request.Id, cancellationToken);

        await connection.ExecuteAsync(
            new CommandDefinition(
                @"UPDATE Drivers SET FullName = @FullName, FirstName = @FirstName, LastName = @LastName,
                  Phone = @Phone, LicenseNumber = @LicenseNumber, LicenseExpiryDate = @LicenseExpiryDate,
                  CNIC = @CNIC, Address = @Address, Nationality = @Nationality, Email = @Email,
                  DateOfBirth = @DateOfBirth, Gender = @Gender,
                  EmergencyContactName = @EmergencyContactName, EmergencyContact = @EmergencyContact,
                  HireDate = @HireDate, BranchId = @BranchId, DepartmentId = @DepartmentId,
                  Status = @Status, IsActive = @IsActive, UpdatedAt = @UpdatedAt
                  WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
                new
                {
                    FullName = fullName,
                    FirstName = dto.FirstName.Trim(),
                    LastName = dto.LastName.Trim(),
                    dto.Phone,
                    dto.LicenseNumber,
                    dto.LicenseExpiryDate,
                    dto.CNIC,
                    dto.Address,
                    dto.Nationality,
                    dto.Email,
                    dto.DateOfBirth,
                    dto.Gender,
                    dto.EmergencyContactName,
                    dto.EmergencyContact,
                    dto.HireDate,
                    dto.BranchId,
                    dto.DepartmentId,
                    Status = (int)dto.Status,
                    dto.IsActive,
                    UpdatedAt = DateTime.UtcNow,
                    request.Id,
                    TenantId = tenantId
                },
                cancellationToken: cancellationToken));

        return ApiResponse<bool>.SuccessResponse(true, "Driver updated successfully.");
    }
}
