using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Invoices.DTOs;

namespace SheikhTravelSystem.Application.Features.Invoices.Queries;

public record GetInvoicesQuery(
    int Page = 1,
    int PageSize = 20,
    string? Status = null,
    string? PaymentStatus = null,
    int? BookingId = null,
    int? TripId = null,
    int? CustomerId = null
) : IRequest<ApiResponse<PagedResult<InvoiceDto>>>;

public class GetInvoicesQueryHandler(
    ICustomerInvoiceRepository invoiceRepository,
    ITenantContext tenantContext,
    ICurrentUserService currentUser,
    IDataScopeEngine dataScopeEngine)
    : IRequestHandler<GetInvoicesQuery, ApiResponse<PagedResult<InvoiceDto>>>
{
    public async Task<ApiResponse<PagedResult<InvoiceDto>>> Handle(
        GetInvoicesQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        DataScopeResult? scope = null;
        if (currentUser.UserId is int userId)
            scope = await dataScopeEngine.ResolveAsync(userId, tenantId, cancellationToken);

        var result = await invoiceRepository.GetPagedAsync(
            tenantId,
            request.Page,
            request.PageSize,
            request.Status,
            request.PaymentStatus,
            request.BookingId,
            request.TripId,
            request.CustomerId,
            scope,
            cancellationToken);
        return ApiResponse<PagedResult<InvoiceDto>>.SuccessResponse(result);
    }
}
