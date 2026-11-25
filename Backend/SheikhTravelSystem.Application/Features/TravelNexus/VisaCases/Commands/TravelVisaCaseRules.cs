using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.VisaCases.DTOs;

namespace SheikhTravelSystem.Application.Features.TravelNexus.VisaCases.Commands;

internal static class TravelVisaCaseRules
{
    public const string ConflictMessage = "The visa case was changed by someone else. Reload and try again.";

    public static void ApplyCommon<T>(AbstractValidator<T> v,
        System.Linq.Expressions.Expression<Func<T, string>> priority,
        System.Linq.Expressions.Expression<Func<T, decimal?>> serviceFee,
        System.Linq.Expressions.Expression<Func<T, decimal?>> embassyFee,
        Func<T, bool> hasFee,
        System.Linq.Expressions.Expression<Func<T, string?>> currency,
        System.Linq.Expressions.Expression<Func<T, string?>> notes)
    {
        v.RuleFor(priority).Must(p => TravelValues.IsOneOf(p, TravelValues.Priorities)).WithMessage("Priority must be Low, Normal or High.");
        v.RuleFor(serviceFee).GreaterThanOrEqualTo(0);
        v.RuleFor(embassyFee).GreaterThanOrEqualTo(0);
        v.RuleFor(currency).Must(TravelValues.IsCurrencyCode).When(hasFee).WithMessage("Fees need an ISO 4217 currency code (e.g. PKR, SAR).");
        v.RuleFor(notes).MaximumLength(2000);
    }
}

/// <summary>Visibility checks shared by visa case commands.</summary>
public sealed class TravelVisaCaseGuard(ITravelVisaCaseRepository cases)
{
    /// <summary>Case facts, or NotFound for other tenants / out-of-scope branches.</summary>
    public async Task<TravelVisaCaseState> GetVisibleAsync(int id, int tenantId, TravelBranchScope scope, CancellationToken ct)
        => await cases.GetStateAsync(id, tenantId, scope, ct) ?? throw new NotFoundException("TravelVisaCase", id);

    public async Task<int> ActiveApplicantCountAsync(int caseId, int tenantId, CancellationToken ct)
        => (await ActiveApplicantIdsAsync(caseId, tenantId, ct)).Count;

    public async Task<IReadOnlySet<int>> ActiveApplicantIdsAsync(int caseId, int tenantId, CancellationToken ct)
        => (await cases.GetApplicantsAsync(caseId, tenantId, ct)).Where(a => a.Decision != "Withdrawn").Select(a => a.Id).ToHashSet();
}
