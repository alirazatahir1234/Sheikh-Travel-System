using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Payments.DTOs;

namespace SheikhTravelSystem.Application.Features.Payments.Queries;

public record GetPaymentsByBookingQuery(int BookingId) : IRequest<List<PaymentDto>>;

public class GetPaymentsByBookingQueryHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<GetPaymentsByBookingQuery, List<PaymentDto>>
{
    public async Task<List<PaymentDto>> Handle(GetPaymentsByBookingQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();

        var payments = await connection.QueryAsync<PaymentDto>(
            new CommandDefinition(
                @"SELECT Id, BookingId, Amount, PaymentMethod, Status, PaymentDate,
                  TransactionReference, Notes, CreatedAt
                  FROM Payments 
                  WHERE BookingId = @BookingId AND IsDeleted = 0
                  ORDER BY PaymentDate DESC",
                new { request.BookingId },
                cancellationToken: cancellationToken));

        return payments.ToList();
    }
}
