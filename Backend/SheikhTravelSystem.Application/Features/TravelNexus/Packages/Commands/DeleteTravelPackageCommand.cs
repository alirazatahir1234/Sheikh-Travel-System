using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Packages.Commands;

/// <summary>Deletes a Draft package that no group uses.</summary>
public record DeleteTravelPackageCommand(int Id) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Delete";
    public string AuditEntityName => "TravelPackage";
    public int? AuditEntityId => Id;
}

public class DeleteTravelPackageCommandValidator : AbstractValidator<DeleteTravelPackageCommand>
{
    public DeleteTravelPackageCommandValidator() => RuleFor(x => x.Id).GreaterThan(0);
}

public class DeleteTravelPackageCommandHandler(ITravelPackageRepository repository, ITenantContext tenantContext)
    : IRequestHandler<DeleteTravelPackageCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(DeleteTravelPackageCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var package = await repository.GetByIdAsync(request.Id, tenantId, cancellationToken) ?? throw new NotFoundException("TravelPackage", request.Id);
        if (package.Status != "Draft" || package.GroupCount > 0)
            return ApiResponse<bool>.FailResponse("Only unused draft packages can be deleted. Archive it instead.");
        if (!await repository.DeleteDraftAsync(request.Id, tenantId, cancellationToken))
            throw new ConflictException("The package was changed by someone else. Reload and try again.");
        return ApiResponse<bool>.SuccessResponse(true, "Package deleted.");
    }
}
