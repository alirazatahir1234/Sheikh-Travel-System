using MediatR;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Payments.DTOs;

namespace SheikhTravelSystem.Application.Features.Payments.Queries;

public record GetPaymentsByBookingQuery(int BookingId) : IRequest<List<PaymentDto>>;

public class GetPaymentsByBookingQueryHandler(IPaymentRepository paymentRepository)
    : IRequestHandler<GetPaymentsByBookingQuery, List<PaymentDto>>
{
    public async Task<List<PaymentDto>> Handle(GetPaymentsByBookingQuery request, CancellationToken cancellationToken)
    {
        return await paymentRepository.GetByBookingIdAsync(request.BookingId, cancellationToken);
    }
}
