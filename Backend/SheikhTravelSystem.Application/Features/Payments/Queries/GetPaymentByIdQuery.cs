using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Payments.DTOs;

namespace SheikhTravelSystem.Application.Features.Payments.Queries;

public record GetPaymentByIdQuery(int Id) : IRequest<ApiResponse<PaymentDetailDto>>;

public class GetPaymentByIdQueryHandler(IPaymentRepository paymentRepository)
    : IRequestHandler<GetPaymentByIdQuery, ApiResponse<PaymentDetailDto>>
{
    public async Task<ApiResponse<PaymentDetailDto>> Handle(GetPaymentByIdQuery request, CancellationToken cancellationToken)
    {
        var payment = await paymentRepository.GetByIdAsync(request.Id, cancellationToken);

        if (payment is null)
            throw new NotFoundException("Payment", request.Id);

        return ApiResponse<PaymentDetailDto>.SuccessResponse(payment);
    }
}
