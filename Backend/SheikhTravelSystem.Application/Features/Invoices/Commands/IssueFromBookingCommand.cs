using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Invoices.Services;

namespace SheikhTravelSystem.Application.Features.Invoices.Commands;

public record IssueFromBookingCommand(int BookingId) : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => "Create";
    public string AuditEntityName => "CustomerInvoice";
    public int? AuditEntityId => null;
}

public class IssueFromBookingCommandValidator : AbstractValidator<IssueFromBookingCommand>
{
    public IssueFromBookingCommandValidator()
    {
        RuleFor(x => x.BookingId).GreaterThan(0);
    }
}

public class IssueFromBookingCommandHandler(
    IInvoiceIssueService invoiceIssueService,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<IssueFromBookingCommand, ApiResponse<int>>
{
    public Task<ApiResponse<int>> Handle(IssueFromBookingCommand request, CancellationToken cancellationToken)
        => invoiceIssueService.IssueFromBookingAsync(
            tenantContext.GetRequiredTenantId(),
            currentUser.UserId,
            request.BookingId,
            cancellationToken);
}
