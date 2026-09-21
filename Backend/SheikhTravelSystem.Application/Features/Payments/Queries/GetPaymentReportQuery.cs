using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Payments.DTOs;

namespace SheikhTravelSystem.Application.Features.Payments.Queries;

public record GetPaymentReportQuery(DateTime? FromDate, DateTime? ToDate) : IRequest<ApiResponse<PaymentReportDto>>;

public class GetPaymentReportQueryHandler(IPaymentRepository paymentRepository)
    : IRequestHandler<GetPaymentReportQuery, ApiResponse<PaymentReportDto>>
{
    public async Task<ApiResponse<PaymentReportDto>> Handle(GetPaymentReportQuery request, CancellationToken cancellationToken)
    {
        var fromDate = request.FromDate ?? DateTime.UtcNow.AddMonths(-1);
        var toDate = request.ToDate ?? DateTime.UtcNow;

        var report = await paymentRepository.GetReportAsync(fromDate, toDate, cancellationToken);
        return ApiResponse<PaymentReportDto>.SuccessResponse(report);
    }
}
