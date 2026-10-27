using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;

namespace SheikhTravelSystem.Application.Features.Platform;

public class GetSubscriptionRevenueDashboardQueryHandler(IPlatformRepository platformRepository)
    : IRequestHandler<GetSubscriptionRevenueDashboardQuery, ApiResponse<SubscriptionRevenueDashboardDto>>
{
    public async Task<ApiResponse<SubscriptionRevenueDashboardDto>> Handle(
        GetSubscriptionRevenueDashboardQuery request, CancellationToken cancellationToken)
    {
        var dashboard = await platformRepository.LoadSubscriptionRevenueDashboardAsync(cancellationToken);
        return ApiResponse<SubscriptionRevenueDashboardDto>.SuccessResponse(dashboard);
    }
}

public class MarkTenantSubscriptionInvoicePaidCommandHandler(
    ISubscriptionBillingService billing,
    IPlatformScope platformScope)
    : IRequestHandler<MarkTenantSubscriptionInvoicePaidCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(
        MarkTenantSubscriptionInvoicePaidCommand request, CancellationToken cancellationToken)
    {
        platformScope.EnsureTenantAccess(request.TenantId);
        var ok = await billing.MarkInvoicePaidAsync(
            request.TenantId, request.InvoiceId, request.PaymentMethod, request.Reference, cancellationToken);
        return ok
            ? ApiResponse<bool>.SuccessResponse(true, "Invoice marked paid.")
            : ApiResponse<bool>.FailResponse("Invoice not found.");
    }
}

public class SendTenantSubscriptionInvoiceEmailCommandHandler(
    ISubscriptionBillingService billing,
    IPlatformScope platformScope)
    : IRequestHandler<SendTenantSubscriptionInvoiceEmailCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(
        SendTenantSubscriptionInvoiceEmailCommand request, CancellationToken cancellationToken)
    {
        platformScope.EnsureTenantAccess(request.TenantId);
        var ok = await billing.SendInvoiceEmailAsync(request.TenantId, request.InvoiceId, cancellationToken);
        return ok
            ? ApiResponse<bool>.SuccessResponse(true, "Invoice emailed.")
            : ApiResponse<bool>.FailResponse("Could not email invoice (missing invoice or billing email).");
    }
}
