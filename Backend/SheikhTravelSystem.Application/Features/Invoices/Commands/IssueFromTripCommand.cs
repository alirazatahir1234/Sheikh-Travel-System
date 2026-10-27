using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Invoices.Services;

namespace SheikhTravelSystem.Application.Features.Invoices.Commands;

public record IssueFromTripCommand(int TripId) : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => "Create";
    public string AuditEntityName => "CustomerInvoice";
    public int? AuditEntityId => null;
}

public class IssueFromTripCommandValidator : AbstractValidator<IssueFromTripCommand>
{
    public IssueFromTripCommandValidator() => RuleFor(x => x.TripId).GreaterThan(0);
}

public class IssueFromTripCommandHandler(
    IInvoiceIssueService invoiceIssueService,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<IssueFromTripCommand, ApiResponse<int>>
{
    public Task<ApiResponse<int>> Handle(IssueFromTripCommand request, CancellationToken cancellationToken)
        => invoiceIssueService.IssueFromTripAsync(
            tenantContext.GetRequiredTenantId(),
            currentUser.UserId,
            request.TripId,
            cancellationToken);
}
