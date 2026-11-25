using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Enquiries.DTOs;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Enquiries.Commands;

public record CreateTravelEnquiryCommand(SaveTravelEnquiryDto Enquiry) : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => "Create";
    public string AuditEntityName => "TravelEnquiry";
    public int? AuditEntityId => null;
}

public class CreateTravelEnquiryCommandValidator : AbstractValidator<CreateTravelEnquiryCommand>
{
    public CreateTravelEnquiryCommandValidator()
        => RuleFor(x => x.Enquiry).NotNull().SetValidator(new SaveTravelEnquiryDtoValidator());
}

public class CreateTravelEnquiryCommandHandler(
    ITravelEnquiryRepository repository,
    TravelEnquiryGuard guard,
    ITravelBranchScopeResolver scopeResolver,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateTravelEnquiryCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(CreateTravelEnquiryCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var scope = await scopeResolver.ResolveAsync(tenantId, cancellationToken);
        var (dto, error) = await guard.PrepareAsync(tenantId, scope, request.Enquiry, cancellationToken);
        if (error is not null) return ApiResponse<int>.FailResponse(error);

        var id = await repository.CreateAsync(tenantId, dto!, currentUser.UserId, DateTime.UtcNow, cancellationToken);
        return ApiResponse<int>.SuccessResponse(id, "Enquiry created.");
    }
}
