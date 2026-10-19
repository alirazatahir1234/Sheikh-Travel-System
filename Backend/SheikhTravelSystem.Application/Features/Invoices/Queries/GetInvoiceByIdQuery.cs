using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Invoices.DTOs;

namespace SheikhTravelSystem.Application.Features.Invoices.Queries;

public record GetInvoiceByIdQuery(int Id) : IRequest<ApiResponse<InvoiceDetailDto>>;

public class GetInvoiceByIdQueryHandler(
    ICustomerInvoiceRepository invoiceRepository,
    ITenantContext tenantContext)
    : IRequestHandler<GetInvoiceByIdQuery, ApiResponse<InvoiceDetailDto>>
{
    public async Task<ApiResponse<InvoiceDetailDto>> Handle(
        GetInvoiceByIdQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var invoice = await invoiceRepository.GetByIdAsync(request.Id, tenantId, cancellationToken)
            ?? throw new NotFoundException("Invoice", request.Id);
        return ApiResponse<InvoiceDetailDto>.SuccessResponse(invoice);
    }
}
