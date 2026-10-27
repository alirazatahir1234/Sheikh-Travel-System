using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Invoices.DTOs;

namespace SheikhTravelSystem.Application.Features.Invoices.Queries;

public record GetInvoiceBillingSummaryQuery : IRequest<ApiResponse<InvoiceBillingSummaryDto>>;

public class GetInvoiceBillingSummaryQueryHandler(
    ICustomerInvoiceRepository invoiceRepository,
    ITenantContext tenantContext,
    ICurrentUserService currentUser,
    IDataScopeEngine dataScopeEngine)
    : IRequestHandler<GetInvoiceBillingSummaryQuery, ApiResponse<InvoiceBillingSummaryDto>>
{
    public async Task<ApiResponse<InvoiceBillingSummaryDto>> Handle(
        GetInvoiceBillingSummaryQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        DataScopeResult? scope = null;
        if (currentUser.UserId is int userId)
            scope = await dataScopeEngine.ResolveAsync(userId, tenantId, cancellationToken);

        var summary = await invoiceRepository.GetBillingSummaryAsync(
            tenantId, scope, DateTime.UtcNow, cancellationToken);
        return ApiResponse<InvoiceBillingSummaryDto>.SuccessResponse(summary);
    }
}
