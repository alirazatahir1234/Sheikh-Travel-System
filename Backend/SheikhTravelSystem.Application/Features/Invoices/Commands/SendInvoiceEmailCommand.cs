using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Invoices.Services;

namespace SheikhTravelSystem.Application.Features.Invoices.Commands;

public record SendInvoiceEmailCommand(int InvoiceId) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Email";
    public string AuditEntityName => "CustomerInvoice";
    public int? AuditEntityId => InvoiceId;
}

public class SendInvoiceEmailCommandValidator : AbstractValidator<SendInvoiceEmailCommand>
{
    public SendInvoiceEmailCommandValidator() => RuleFor(x => x.InvoiceId).GreaterThan(0);
}

public class SendInvoiceEmailCommandHandler(IInvoiceEmailService invoiceEmailService)
    : IRequestHandler<SendInvoiceEmailCommand, ApiResponse<bool>>
{
    public Task<ApiResponse<bool>> Handle(SendInvoiceEmailCommand request, CancellationToken cancellationToken)
        => invoiceEmailService.SendAsync(request.InvoiceId, cancellationToken);
}
