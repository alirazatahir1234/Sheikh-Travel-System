using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Packages.Commands;

/// <summary>Publish (Draft → Published; needs prices, stays and a visa type) or Archive (→ Archived).</summary>
public record ChangeTravelPackageStatusCommand(int Id, string Action) : IRequest<ApiResponse<string>>, IAuditableCommand
{
    public string AuditAction => Action;
    public string AuditEntityName => "TravelPackage";
    public int? AuditEntityId => Id;
}

public class ChangeTravelPackageStatusCommandValidator : AbstractValidator<ChangeTravelPackageStatusCommand>
{
    public ChangeTravelPackageStatusCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Action).Must(a => a is "Publish" or "Archive").WithMessage("Action must be Publish or Archive.");
    }
}

public class ChangeTravelPackageStatusCommandHandler(ITravelPackageRepository repository, ITenantContext tenantContext)
    : IRequestHandler<ChangeTravelPackageStatusCommand, ApiResponse<string>>
{
    public async Task<ApiResponse<string>> Handle(ChangeTravelPackageStatusCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var package = await repository.GetByIdAsync(request.Id, tenantId, cancellationToken) ?? throw new NotFoundException("TravelPackage", request.Id);

        string target;
        if (request.Action == "Publish")
        {
            if (package.Status != "Draft") return ApiResponse<string>.FailResponse($"A {package.Status} package cannot be published.");
            if (package.Prices.Count == 0) return ApiResponse<string>.FailResponse("Add at least one room price before publishing.");
            if (package.Stays.Count == 0) return ApiResponse<string>.FailResponse("Add at least one hotel stay before publishing.");
            if (package.VisaTypeId is null) return ApiResponse<string>.FailResponse("Choose the visa type pilgrims will apply for.");
            target = "Published";
        }
        else
        {
            if (package.Status == "Archived") return ApiResponse<string>.FailResponse("The package is already archived.");
            target = "Archived";
        }

        if (!await repository.SetStatusAsync(request.Id, tenantId, package.Status, target, cancellationToken))
            throw new ConflictException("The package was changed by someone else. Reload and try again.");
        return ApiResponse<string>.SuccessResponse(target, $"Package {target.ToLowerInvariant()}.");
    }
}
