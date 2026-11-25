using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Travelers.DTOs;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Travelers.Commands;

public record CreateTravelTravelerCommand(int CustomerId, SaveTravelTravelerDto Traveler)
    : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => "Create";
    public string AuditEntityName => "TravelTraveler";
    public int? AuditEntityId => null;
}

public class CreateTravelTravelerCommandValidator : AbstractValidator<CreateTravelTravelerCommand>
{
    public CreateTravelTravelerCommandValidator()
    {
        RuleFor(x => x.CustomerId).GreaterThan(0);
        RuleFor(x => x.Traveler).NotNull().SetValidator(new SaveTravelTravelerDtoValidator());
        RuleFor(x => x.Traveler.PassportNumber).Must(p => !TravelPii.IsMasked(p)).WithMessage("Enter the full passport number.");
        RuleFor(x => x.Traveler.Cnic).Must(c => !TravelPii.IsMasked(c)).WithMessage("Enter the full CNIC.");
    }
}

public class CreateTravelTravelerCommandHandler(
    ITravelTravelerRepository repository,
    ITravelConfigRepository config,
    TravelTravelerGuard guard,
    ITravelBranchScopeResolver scopeResolver,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateTravelTravelerCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(CreateTravelTravelerCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var scope = await scopeResolver.ResolveAsync(tenantId, cancellationToken);
        await guard.EnsureCustomerVisibleAsync(request.CustomerId, tenantId, scope, cancellationToken);

        var dto = TravelTravelerNormalizer.Normalize(request.Traveler, existing: null);
        var error = await guard.ValidateAsync(tenantId, request.CustomerId, null, dto, cancellationToken);
        if (error is not null)
            return ApiResponse<int>.FailResponse(error);

        var id = await repository.CreateAsync(tenantId, request.CustomerId, dto, currentUser.UserId, cancellationToken);
        if (id == 0)
            throw new NotFoundException("TravelCustomer", request.CustomerId);

        var settings = await config.GetSettingsAsync(tenantId, cancellationToken);
        var status = TravelPii.PassportStatus(dto.PassportNumber, dto.PassportExpiryDate, DateTime.UtcNow, settings.PassportExpiryWarningDays);
        return ApiResponse<int>.SuccessResponse(id, TravelTravelerMessages.Saved("Traveler added.", status));
    }
}

internal static class TravelTravelerMessages
{
    public static string Saved(string baseMessage, string passportStatus) => passportStatus switch
    {
        "Expired" => baseMessage + " Warning: the passport has expired.",
        "ExpiringSoon" => baseMessage + " Warning: the passport expires soon.",
        _ => baseMessage
    };
}
