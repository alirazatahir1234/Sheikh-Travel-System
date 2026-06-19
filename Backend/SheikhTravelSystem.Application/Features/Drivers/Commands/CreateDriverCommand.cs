using Dapper;
using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Drivers.DTOs;

namespace SheikhTravelSystem.Application.Features.Drivers.Commands;

public record CreateDriverCommand(CreateDriverDto Driver) : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => "Create";
    public string AuditEntityName => "Driver";
    public int? AuditEntityId => null;
}

public class CreateDriverCommandValidator : AbstractValidator<CreateDriverCommand>
{
    public CreateDriverCommandValidator()
    {
        RuleFor(x => x.Driver.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Driver.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Driver.Phone).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Driver.Email).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(x => x.Driver.LicenseNumber).NotEmpty().MaximumLength(30);
        RuleFor(x => x.Driver.LicenseExpiryDate).GreaterThan(DateTime.UtcNow)
            .WithMessage("License must not be expired.");
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

public class CreateDriverCommandHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<CreateDriverCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(CreateDriverCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var dto = request.Driver;
        var tenantId = tenantContext.GetRequiredTenantId();
        var fullName = DriverFieldHelper.BuildFullName(dto.FirstName, dto.LastName);

        await DriverUniquenessHelper.EnsureUniqueAsync(
            connection, tenantId, dto.Phone, dto.Email, dto.LicenseNumber, excludeId: null, cancellationToken);

        var id = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                @"INSERT INTO Drivers (TenantId, FullName, FirstName, LastName, Phone, LicenseNumber, LicenseExpiryDate,
                  CNIC, Address, DriverCode, Nationality, Email, DateOfBirth, Gender, EmergencyContactName, EmergencyContact,
                  HireDate, BranchId, DepartmentId, VerificationStatus, Status, IsActive, CreatedAt, IsDeleted)
                  VALUES (@TenantId, @FullName, @FirstName, @LastName, @Phone, @LicenseNumber, @LicenseExpiryDate,
                  @CNIC, @Address, @DriverCode, @Nationality, @Email, @DateOfBirth, @Gender, @EmergencyContactName, @EmergencyContact,
                  @HireDate, @BranchId, @DepartmentId, N'Pending', @Status, 1, @CreatedAt, 0);
                  SELECT CAST(SCOPE_IDENTITY() AS INT);",
                new
                {
                    TenantId = tenantId,
                    FullName = fullName,
                    FirstName = dto.FirstName.Trim(),
                    LastName = dto.LastName.Trim(),
                    dto.Phone,
                    dto.LicenseNumber,
                    dto.LicenseExpiryDate,
                    dto.CNIC,
                    dto.Address,
                    DriverCode = DriverFieldHelper.GenerateDriverCode(),
                    dto.Nationality,
                    dto.Email,
                    dto.DateOfBirth,
                    dto.Gender,
                    dto.EmergencyContactName,
                    dto.EmergencyContact,
                    dto.HireDate,
                    dto.BranchId,
                    dto.DepartmentId,
                    Status = (int)Domain.Enums.DriverStatus.Available,
                    CreatedAt = DateTime.UtcNow
                },
                cancellationToken: cancellationToken));

        return ApiResponse<int>.SuccessResponse(id, "Driver created successfully.");
    }
}
