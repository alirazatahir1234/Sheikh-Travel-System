using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
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

public class CreateDriverCommandHandler(IDriverRepository driverRepository, ITenantContext tenantContext)
    : IRequestHandler<CreateDriverCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(CreateDriverCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Driver;
        var tenantId = tenantContext.GetRequiredTenantId();
        var fullName = DriverFieldHelper.BuildFullName(dto.FirstName, dto.LastName);
        var id = await driverRepository.CreateAsync(
            tenantId, dto, fullName, DriverFieldHelper.GenerateDriverCode(), cancellationToken);
        return ApiResponse<int>.SuccessResponse(id, "Driver created successfully.");
    }
}
