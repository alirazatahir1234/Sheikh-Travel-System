using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Checklists.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Checklists.Commands;

public class SaveTravelChecklistTemplateDtoValidator : AbstractValidator<SaveTravelChecklistTemplateDto>
{
    public SaveTravelChecklistTemplateDtoValidator()
    {
        RuleFor(x => x.CountryCode).Must(TravelValues.IsCountryCode).WithMessage("Country must be an ISO alpha-2 code.");
        RuleFor(x => x.VisaTypeId).GreaterThan(0).When(x => x.VisaTypeId.HasValue);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Notes).MaximumLength(1000);
    }
}

/// <summary>Cross-record checks for checklists.</summary>
public sealed class TravelChecklistGuard(ITravelChecklistRepository checklists, ITravelConfigRepository config)
{
    public async Task<TravelChecklistTemplateDto> GetAsync(int id, int tenantId, CancellationToken ct)
        => await checklists.GetByIdAsync(id, tenantId, ct) ?? throw new NotFoundException("TravelChecklist", id);

    /// <summary>Null when the country / visa type pair is valid for the tenant.</summary>
    public async Task<string?> ValidateScopeAsync(int tenantId, SaveTravelChecklistTemplateDto dto, CancellationToken ct)
    {
        if (!await config.CountryExistsAsync(dto.CountryCode, ct)) return "Unknown country.";
        if (dto.VisaTypeId is int visaTypeId)
        {
            var visaType = await config.GetVisaTypeByIdAsync(visaTypeId, tenantId, ct);
            if (visaType is null) return "Visa type not found.";
            if (visaType.CountryCode != dto.CountryCode) return "The visa type belongs to a different country.";
        }
        return null;
    }
}
