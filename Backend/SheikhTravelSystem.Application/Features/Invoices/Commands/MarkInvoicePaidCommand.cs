using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;

namespace SheikhTravelSystem.Application.Features.Invoices.Commands;

public record MarkInvoicePaidCommand(int Id) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "MarkPaid";
    public string AuditEntityName => "CustomerInvoice";
    public int? AuditEntityId => Id;
}

public class MarkInvoicePaidCommandValidator : AbstractValidator<MarkInvoicePaidCommand>
{
    public MarkInvoicePaidCommandValidator() => RuleFor(x => x.Id).GreaterThan(0);
}

public class MarkInvoicePaidCommandHandler(
    ICustomerInvoiceRepository invoiceRepository,
    ITenantContext tenantContext)
    : IRequestHandler<MarkInvoicePaidCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(MarkInvoicePaidCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var invoice = await invoiceRepository.GetByIdAsync(request.Id, tenantId, cancellationToken)
            ?? throw new NotFoundException("Invoice", request.Id);

        if (string.Equals(invoice.Status, "Void", StringComparison.OrdinalIgnoreCase))
            return ApiResponse<bool>.FailResponse("Cannot mark a void invoice as paid.");

        if (string.Equals(invoice.Status, "Paid", StringComparison.OrdinalIgnoreCase) && invoice.Balance <= 0)
            return ApiResponse<bool>.SuccessResponse(true, "Invoice is already paid.");

        await invoiceRepository.MarkPaidAsync(request.Id, tenantId, invoice.TotalAmount, cancellationToken);
        return ApiResponse<bool>.SuccessResponse(true, "Invoice marked as paid.");
    }
}
