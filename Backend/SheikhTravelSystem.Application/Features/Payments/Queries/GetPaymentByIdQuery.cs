using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Payments.DTOs;

namespace SheikhTravelSystem.Application.Features.Payments.Queries;

public record GetPaymentByIdQuery(int Id) : IRequest<ApiResponse<PaymentDetailDto>>;

public class GetPaymentByIdQueryHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<GetPaymentByIdQuery, ApiResponse<PaymentDetailDto>>
{
    public async Task<ApiResponse<PaymentDetailDto>> Handle(GetPaymentByIdQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();

        var payment = await connection.QuerySingleOrDefaultAsync<PaymentDetailDto>(
            new CommandDefinition(
                @"SELECT p.Id, p.BookingId, b.BookingNumber, c.FullName AS CustomerName,
                  r.Source + ' -> ' + r.Destination AS RouteName,
                  p.Amount, p.PaymentMethod, p.Status, p.PaymentDate,
                  p.TransactionReference, p.Notes, p.CreatedAt, b.TotalAmount AS TotalBookingAmount,
                  CASE WHEN COL_LENGTH('Payments', 'ReceiptImageData') IS NOT NULL THEN p.ReceiptImageData ELSE NULL END AS ReceiptImageData
                  FROM Payments p
                  INNER JOIN Bookings b ON p.BookingId = b.Id
                  LEFT JOIN Customers c ON b.CustomerId = c.Id
                  LEFT JOIN Routes r ON b.RouteId = r.Id
                  WHERE p.Id = @Id AND p.IsDeleted = 0",
                new { request.Id },
                cancellationToken: cancellationToken));

        if (payment is null)
            throw new NotFoundException("Payment", request.Id);

        return ApiResponse<PaymentDetailDto>.SuccessResponse(payment);
    }
}
