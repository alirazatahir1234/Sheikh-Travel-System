using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Enquiries.DTOs;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Enquiries.Commands;

/// <summary>Follow-up / communication history entry. An outbound contact on a New enquiry moves it to Contacted.</summary>
public record LogTravelEnquiryActivityCommand(int EnquiryId, LogTravelEnquiryActivityDto Activity) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "LogActivity";
    public string AuditEntityName => "TravelEnquiry";
    public int? AuditEntityId => EnquiryId;
}

public class LogTravelEnquiryActivityCommandValidator : AbstractValidator<LogTravelEnquiryActivityCommand>
{
    public LogTravelEnquiryActivityCommandValidator()
    {
        RuleFor(x => x.EnquiryId).GreaterThan(0);
        RuleFor(x => x.Activity).NotNull();
        RuleFor(x => x.Activity.ActivityType).Must(v => TravelValues.IsOneOf(v, TravelValues.UserActivityTypes)).WithMessage("Unknown activity type.");
        RuleFor(x => x.Activity.Direction).Must(v => TravelValues.IsOneOf(v, TravelValues.ActivityDirections)).WithMessage("Unknown direction.");
        RuleFor(x => x.Activity.Summary).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.Activity.Outcome).MaximumLength(500);
        RuleFor(x => x.Activity.OccurredAt).LessThanOrEqualTo(_ => DateTime.UtcNow.AddMinutes(5))
            .When(x => x.Activity.OccurredAt.HasValue).WithMessage("An activity cannot be in the future.");
    }
}

public class LogTravelEnquiryActivityCommandHandler(
    ITravelEnquiryRepository repository,
    TravelEnquiryGuard guard,
    ITravelBranchScopeResolver scopeResolver,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<LogTravelEnquiryActivityCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(LogTravelEnquiryActivityCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var scope = await scopeResolver.ResolveAsync(tenantId, cancellationToken);
        var enquiry = await guard.GetVisibleAsync(request.EnquiryId, tenantId, scope, cancellationToken);

        var a = request.Activity;
        var promote = enquiry.Status == "New" && a.Direction == "Outbound";
        var occurredAt = a.OccurredAt ?? DateTime.UtcNow;
        var activity = a with { Summary = a.Summary.Trim(), Outcome = string.IsNullOrWhiteSpace(a.Outcome) ? null : a.Outcome.Trim() };

        if (!await repository.LogActivityAsync(tenantId, request.EnquiryId, activity, occurredAt, promote, currentUser.UserId, cancellationToken))
            throw new NotFoundException("TravelEnquiry", request.EnquiryId);

        return ApiResponse<bool>.SuccessResponse(true, promote ? "Activity logged; enquiry marked Contacted." : "Activity logged.");
    }
}
