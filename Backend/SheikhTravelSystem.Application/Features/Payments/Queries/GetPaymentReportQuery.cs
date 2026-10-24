using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Payments.DTOs;
using SheikhTravelSystem.Application.Features.Reports.Fleet;

namespace SheikhTravelSystem.Application.Features.Payments.Queries;

public record GetPaymentReportQuery(DateTime? FromDate, DateTime? ToDate) : IRequest<ApiResponse<PaymentReportDto>>;

public class GetPaymentReportQueryHandler(
    IPaymentRepository paymentRepository,
    ITenantContext tenantContext)
    : IRequestHandler<GetPaymentReportQuery, ApiResponse<PaymentReportDto>>
{
    public async Task<ApiResponse<PaymentReportDto>> Handle(GetPaymentReportQuery request, CancellationToken cancellationToken)
    {
        var (fromDate, toExclusive) = FleetReportHelper.ResolveDateRange(request.FromDate, request.ToDate);
        var tenantId = tenantContext.GetRequiredTenantId();

        var report = await paymentRepository.GetReportAsync(tenantId, fromDate, toExclusive, cancellationToken);
        return ApiResponse<PaymentReportDto>.SuccessResponse(report);
    }
}
