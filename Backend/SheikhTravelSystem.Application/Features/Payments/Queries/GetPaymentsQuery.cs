using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Payments.DTOs;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.Payments.Queries;

public record GetPaymentsQuery(
    int Page = 1,
    int PageSize = 20,
    int? BookingId = null,
    PaymentStatus? Status = null,
    DateTime? DateFrom = null,
    DateTime? DateTo = null
) : IRequest<ApiResponse<PagedResult<PaymentDto>>>;

public class GetPaymentsQueryHandler(
    IPaymentRepository paymentRepository,
    ITenantContext tenantContext,
    ICurrentUserService currentUser,
    IDataScopeEngine dataScopeEngine)
    : IRequestHandler<GetPaymentsQuery, ApiResponse<PagedResult<PaymentDto>>>
{
    public async Task<ApiResponse<PagedResult<PaymentDto>>> Handle(GetPaymentsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        DataScopeResult? scope = null;

        if (currentUser.UserId is int userId)
            scope = await dataScopeEngine.ResolveAsync(userId, tenantId, cancellationToken);

        var result = await paymentRepository.GetPagedAsync(
            request.Page,
            request.PageSize,
            tenantId,
            request.BookingId,
            request.Status,
            request.DateFrom,
            request.DateTo,
            scope,
            cancellationToken);

        return ApiResponse<PagedResult<PaymentDto>>.SuccessResponse(result);
    }
}
