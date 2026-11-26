using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Reminders.DTOs;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Reminders.Queries;

public record GetTravelReminderRulesQuery : IRequest<ApiResponse<IReadOnlyList<TravelReminderRuleDto>>>;

public class GetTravelReminderRulesQueryHandler(ITravelReminderRepository repository, ITenantContext tenantContext)
    : IRequestHandler<GetTravelReminderRulesQuery, ApiResponse<IReadOnlyList<TravelReminderRuleDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<TravelReminderRuleDto>>> Handle(GetTravelReminderRulesQuery request, CancellationToken cancellationToken)
        => ApiResponse<IReadOnlyList<TravelReminderRuleDto>>.SuccessResponse(
            TravelReminderRules.Merge(await repository.GetRulesAsync(tenantContext.GetRequiredTenantId(), cancellationToken)));
}

public class GetTravelReminderLogQuery : PagedRequest, IRequest<ApiResponse<PagedResult<TravelReminderLogItemDto>>>
{
    public string? RuleType { get; set; }
    public string? Status { get; set; }
    public string? Audience { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
}

public class GetTravelReminderLogQueryValidator : AbstractValidator<GetTravelReminderLogQuery>
{
    public GetTravelReminderLogQueryValidator()
    {
        RuleFor(x => x.RuleType).Must(TravelReminderCatalog.IsKnown).When(x => !string.IsNullOrEmpty(x.RuleType));
        RuleFor(x => x.Status).Must(s => TravelValues.IsOneOf(s, TravelValues.ReminderStatuses)).When(x => !string.IsNullOrEmpty(x.Status));
        RuleFor(x => x.Audience).Must(a => TravelValues.IsOneOf(a, TravelValues.ReminderAudiences)).When(x => !string.IsNullOrEmpty(x.Audience));
    }
}

public class GetTravelReminderLogQueryHandler(ITravelReminderRepository repository, ITenantContext tenantContext)
    : IRequestHandler<GetTravelReminderLogQuery, ApiResponse<PagedResult<TravelReminderLogItemDto>>>
{
    public async Task<ApiResponse<PagedResult<TravelReminderLogItemDto>>> Handle(GetTravelReminderLogQuery request, CancellationToken cancellationToken)
    {
        if (request.From.HasValue && request.To.HasValue && request.To.Value.Date < request.From.Value.Date)
            return ApiResponse<PagedResult<TravelReminderLogItemDto>>.SuccessResponse(new PagedResult<TravelReminderLogItemDto>
                { Items = [], TotalCount = 0, Page = request.Page, PageSize = request.PageSize });
        var tenantId = tenantContext.GetRequiredTenantId();
        var filter = new TravelReminderLogFilter(string.IsNullOrEmpty(request.RuleType) ? null : request.RuleType,
            string.IsNullOrEmpty(request.Status) ? null : request.Status, string.IsNullOrEmpty(request.Audience) ? null : request.Audience,
            request.From?.Date, request.To?.Date);
        return ApiResponse<PagedResult<TravelReminderLogItemDto>>.SuccessResponse(
            await repository.GetLogAsync(tenantId, filter, request.Page, request.PageSize, cancellationToken));
    }
}
