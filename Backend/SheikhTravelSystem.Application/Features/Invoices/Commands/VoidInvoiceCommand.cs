using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;

namespace SheikhTravelSystem.Application.Features.Invoices.Commands;

public record VoidInvoiceCommand(int Id) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Void";
    public string AuditEntityName => "CustomerInvoice";
    public int? AuditEntityId => Id;
}

public class VoidInvoiceCommandValidator : AbstractValidator<VoidInvoiceCommand>
{
    public VoidInvoiceCommandValidator() => RuleFor(x => x.Id).GreaterThan(0);
}

public class VoidInvoiceCommandHandler(
    ICustomerInvoiceRepository invoiceRepository,
    ITenantContext tenantContext)
    : IRequestHandler<VoidInvoiceCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(VoidInvoiceCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var invoice = await invoiceRepository.GetByIdAsync(request.Id, tenantId, cancellationToken)
            ?? throw new NotFoundException("Invoice", request.Id);

        if (string.Equals(invoice.Status, "Void", StringComparison.OrdinalIgnoreCase))
            return ApiResponse<bool>.FailResponse("Invoice is already void.");

        if (string.Equals(invoice.Status, "Paid", StringComparison.OrdinalIgnoreCase))
            return ApiResponse<bool>.FailResponse("Paid invoices cannot be voided.");

        await invoiceRepository.VoidAsync(request.Id, tenantId, cancellationToken);
        return ApiResponse<bool>.SuccessResponse(true, "Invoice voided.");
    }
}
