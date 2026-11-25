using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Configuration.DTOs;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Configuration.Commands;

/// <summary>Creates (Id null) or updates a visa type for the current tenant.</summary>
public record SaveTravelVisaTypeCommand(int? Id, SaveTravelVisaTypeDto VisaType)
    : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => Id.HasValue ? "Update" : "Create";
    public string AuditEntityName => "TravelVisaType";
    public int? AuditEntityId => Id;
}

public class SaveTravelVisaTypeCommandValidator : AbstractValidator<SaveTravelVisaTypeCommand>
{
    public SaveTravelVisaTypeCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0).When(x => x.Id.HasValue);
        RuleFor(x => x.VisaType).NotNull();
        RuleFor(x => x.VisaType.CountryCode).Must(TravelValues.IsCountryCode)
            .WithMessage("Country must be an ISO 3166 alpha-2 code.");
        RuleFor(x => x.VisaType.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.VisaType.Category).Must(v => TravelValues.IsOneOf(v, TravelValues.VisaCategories))
            .WithMessage("Unknown visa category.");
        RuleFor(x => x.VisaType.EntryType).Must(v => TravelValues.IsOneOf(v, TravelValues.VisaEntryTypes))
            .WithMessage("Unknown entry type.");
        RuleFor(x => x.VisaType.ValidityDays).InclusiveBetween(1, 3650).When(x => x.VisaType.ValidityDays.HasValue);
        RuleFor(x => x.VisaType.StayDays).InclusiveBetween(1, 3650).When(x => x.VisaType.StayDays.HasValue);
        RuleFor(x => x.VisaType.ProcessingDays).InclusiveBetween(0, 365).When(x => x.VisaType.ProcessingDays.HasValue);
        RuleFor(x => x.VisaType.DefaultFee).GreaterThanOrEqualTo(0).When(x => x.VisaType.DefaultFee.HasValue);
        RuleFor(x => x.VisaType.CurrencyCode).Must(TravelValues.IsCurrencyCode)
            .When(x => x.VisaType.DefaultFee.HasValue || !string.IsNullOrEmpty(x.VisaType.CurrencyCode))
            .WithMessage("A fee needs an ISO 4217 currency code (e.g. PKR, SAR).");
        RuleFor(x => x.VisaType.Notes).MaximumLength(1000);
    }
}

public class SaveTravelVisaTypeCommandHandler(ITravelConfigRepository repository, ITenantContext tenantContext, ICurrentUserService currentUser)
    : IRequestHandler<SaveTravelVisaTypeCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(SaveTravelVisaTypeCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var dto = request.VisaType with { Name = request.VisaType.Name.Trim() };

        if (!await repository.CountryExistsAsync(dto.CountryCode, cancellationToken))
            return ApiResponse<int>.FailResponse("Unknown country.");
        if (await repository.VisaTypeNameExistsAsync(tenantId, dto.CountryCode, dto.Name, request.Id, cancellationToken))
            return ApiResponse<int>.FailResponse("A visa type with this name already exists for the country.");

        if (request.Id is int id)
        {
            if (!await repository.UpdateVisaTypeAsync(id, tenantId, dto, cancellationToken))
                throw new NotFoundException("TravelVisaType", id);
            return ApiResponse<int>.SuccessResponse(id, "Visa type updated.");
        }

        var newId = await repository.CreateVisaTypeAsync(tenantId, dto, currentUser.UserId, cancellationToken);
        return ApiResponse<int>.SuccessResponse(newId, "Visa type created.");
    }
}
