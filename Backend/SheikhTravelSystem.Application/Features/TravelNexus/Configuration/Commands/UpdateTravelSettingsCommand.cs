using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Configuration.DTOs;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Configuration.Commands;

public record UpdateTravelSettingsCommand(UpdateTravelSettingsDto Settings)
    : IRequest<ApiResponse<TravelSettingsDto>>, IAuditableCommand
{
    public string AuditAction => "Update";
    public string AuditEntityName => "TravelSettings";
    public int? AuditEntityId => null;
}

public class UpdateTravelSettingsCommandValidator : AbstractValidator<UpdateTravelSettingsCommand>
{
    private const string PrefixPattern = "^[A-Z0-9-]+$";
    private const string PrefixMessage = "Prefixes may contain only A-Z, 0-9 and '-'.";

    public UpdateTravelSettingsCommandValidator()
    {
        RuleFor(x => x.Settings).NotNull();
        RuleFor(x => x.Settings.DefaultCurrencyCode).Must(TravelValues.IsCurrencyCode)
            .When(x => !string.IsNullOrEmpty(x.Settings.DefaultCurrencyCode))
            .WithMessage("Currency must be an ISO 4217 code (e.g. PKR, SAR).");
        RuleFor(x => x.Settings.EnquiryPrefix).NotEmpty().MaximumLength(10).Matches(PrefixPattern).WithMessage(PrefixMessage);
        RuleFor(x => x.Settings.VisaCasePrefix).NotEmpty().MaximumLength(10).Matches(PrefixPattern).WithMessage(PrefixMessage);
        RuleFor(x => x.Settings.InvoicePrefix).NotEmpty().MaximumLength(10).Matches(PrefixPattern).WithMessage(PrefixMessage);
        RuleFor(x => x.Settings.GroupPrefix).MaximumLength(10).Matches(PrefixPattern).WithMessage(PrefixMessage)
            .When(x => !string.IsNullOrEmpty(x.Settings.GroupPrefix));
        RuleFor(x => x.Settings.BookingPrefix).MaximumLength(10).Matches(PrefixPattern).WithMessage(PrefixMessage)
            .When(x => !string.IsNullOrEmpty(x.Settings.BookingPrefix));
        RuleFor(x => x.Settings.PassportExpiryWarningDays).InclusiveBetween(0, 730);
    }
}

public class UpdateTravelSettingsCommandHandler(ITravelConfigRepository repository, ITenantContext tenantContext, ICurrentUserService currentUser)
    : IRequestHandler<UpdateTravelSettingsCommand, ApiResponse<TravelSettingsDto>>
{
    public async Task<ApiResponse<TravelSettingsDto>> Handle(UpdateTravelSettingsCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var dto = request.Settings with
        {
            DefaultCurrencyCode = string.IsNullOrWhiteSpace(request.Settings.DefaultCurrencyCode) ? null : request.Settings.DefaultCurrencyCode
        };
        await repository.UpsertSettingsAsync(tenantId, dto, currentUser.UserId, cancellationToken);
        var saved = await repository.GetSettingsAsync(tenantId, cancellationToken);
        return ApiResponse<TravelSettingsDto>.SuccessResponse(saved, "Settings saved.");
    }
}
