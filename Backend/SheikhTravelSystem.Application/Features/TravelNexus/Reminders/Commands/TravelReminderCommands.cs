using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Reminders.DTOs;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Reminders.Commands;

public record SaveTravelReminderRuleCommand(string RuleType, SaveTravelReminderRuleDto Rule) : IRequest<ApiResponse<TravelReminderRuleDto>>, IAuditableCommand
{
    public string AuditAction => "SaveReminderRule";
    public string AuditEntityName => "TravelReminderRule";
    public int? AuditEntityId => null;
}

public class SaveTravelReminderRuleCommandValidator : AbstractValidator<SaveTravelReminderRuleCommand>
{
    public SaveTravelReminderRuleCommandValidator()
    {
        RuleFor(x => x.RuleType).Must(TravelReminderCatalog.IsKnown).WithMessage("Unknown reminder rule.");
        RuleFor(x => x.Rule.Leads).NotNull().Must(l => l.Count <= 6).WithMessage("At most 6 lead times per rule.")
            .Must(l => l.Select(x => x.LeadDays).Distinct().Count() == l.Count).WithMessage("Each lead time once.");
        RuleForEach(x => x.Rule.Leads).ChildRules(lead => lead.RuleFor(l => l.LeadDays).InclusiveBetween(0, 365));
        RuleFor(x => x.Rule.CustomerTemplate).MaximumLength(1000);
        // Overdue and event rules fire on the trigger itself: a lead time makes no sense for them.
        RuleFor(x => x).Must(x => TravelReminderCatalog.Find(x.RuleType) is not { Kind: not TravelReminderKind.Upcoming } || x.Rule.Leads.All(l => l.LeadDays == 0))
            .WithMessage("This rule fires on the day; use a lead time of 0.");
    }
}

public class SaveTravelReminderRuleCommandHandler(ITravelReminderRepository repository, ITenantContext tenantContext, ICurrentUserService currentUser)
    : IRequestHandler<SaveTravelReminderRuleCommand, ApiResponse<TravelReminderRuleDto>>
{
    public async Task<ApiResponse<TravelReminderRuleDto>> Handle(SaveTravelReminderRuleCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var definition = TravelReminderCatalog.Find(request.RuleType)!;
        var leads = request.Rule.Leads.Select(l => new TravelReminderLeadDto
        {
            LeadDays = l.LeadDays, IsEnabled = l.IsEnabled,
            StaffInApp = l.StaffInApp && definition.SupportsStaff, StaffEmail = l.StaffEmail && definition.SupportsStaff,
            CustomerEmail = l.CustomerEmail && definition.SupportsCustomer, CustomerSms = l.CustomerSms && definition.SupportsCustomer,
            CustomerWhatsApp = l.CustomerWhatsApp && definition.SupportsCustomer
        }).OrderBy(l => l.LeadDays).ToList();
        var template = string.IsNullOrWhiteSpace(request.Rule.CustomerTemplate) ? null : request.Rule.CustomerTemplate.Trim();
        await repository.SaveRuleAsync(tenantId, definition.RuleType, new SaveTravelReminderRuleDto(leads, template), currentUser.UserId, cancellationToken);
        var saved = TravelReminderRules.Merge(await repository.GetRulesAsync(tenantId, cancellationToken)).First(r => r.RuleType == definition.RuleType);
        return ApiResponse<TravelReminderRuleDto>.SuccessResponse(saved, "Reminder rule saved.");
    }
}

/// <summary>Runs this tenant's reminders now (the hosted service does the same on its schedule).</summary>
public record RunTravelRemindersCommand : IRequest<ApiResponse<TravelReminderRunResultDto>>, IAuditableCommand
{
    public string AuditAction => "RunReminders";
    public string AuditEntityName => "TravelReminders";
    public int? AuditEntityId => null;
}

public class RunTravelRemindersCommandHandler(ITravelReminderRunner runner, ITenantContext tenantContext)
    : IRequestHandler<RunTravelRemindersCommand, ApiResponse<TravelReminderRunResultDto>>
{
    public async Task<ApiResponse<TravelReminderRunResultDto>> Handle(RunTravelRemindersCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var result = await runner.RunTenantAsync(tenantId, cancellationToken);
        var summary = $"{result.Sent} sent, {result.Skipped} skipped, {result.Failed} failed" + (result.Held > 0 ? $", {result.Held} held for quiet hours" : "");
        return ApiResponse<TravelReminderRunResultDto>.SuccessResponse(result, summary + ".");
    }
}
