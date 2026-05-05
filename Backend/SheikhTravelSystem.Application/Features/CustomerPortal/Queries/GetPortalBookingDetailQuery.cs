using Dapper;
using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.CustomerPortal.DTOs;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.CustomerPortal.Queries;

public record GetPortalBookingDetailQuery(int BookingId, string Phone) : IRequest<ApiResponse<PortalBookingDetailDto>>;

public class GetPortalBookingDetailQueryValidator : AbstractValidator<GetPortalBookingDetailQuery>
{
    public GetPortalBookingDetailQueryValidator()
    {
        RuleFor(x => x.BookingId).GreaterThan(0);
        RuleFor(x => x.Phone).NotEmpty().MaximumLength(20);
    }
}

public class GetPortalBookingDetailQueryHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<GetPortalBookingDetailQuery, ApiResponse<PortalBookingDetailDto>>
{
    public async Task<ApiResponse<PortalBookingDetailDto>> Handle(GetPortalBookingDetailQuery request, CancellationToken cancellationToken)
    {
        var phone = request.Phone.Trim();
        using var connection = dbFactory.CreateConnection();

        var head = await connection.QuerySingleOrDefaultAsync<(
            int Id,
            string BookingNumber,
            string RouteLabel,
            DateTime PickupTime,
            int PassengerCount,
            string? VehicleName,
            int Status,
            decimal TotalAmount,
            decimal PaidAmount)?>(
            new CommandDefinition(
                @"SELECT b.Id,
                         b.BookingNumber,
                         ISNULL(r.Source + N' → ' + r.Destination, N'') AS RouteLabel,
                         b.PickupTime,
                         b.PassengerCount,
                         v.Name AS VehicleName,
                         b.Status,
                         b.TotalAmount,
                         ISNULL((
                           SELECT SUM(p.Amount)
                           FROM Payments p
                           WHERE p.BookingId = b.Id
                             AND p.Status IN (@Paid, @Partial)
                             AND p.IsDeleted = 0
                         ), 0) AS PaidAmount
                  FROM Bookings b
                  INNER JOIN Customers c ON c.Id = b.CustomerId AND c.IsDeleted = 0
                  LEFT JOIN Routes r ON r.Id = b.RouteId AND r.IsDeleted = 0
                  LEFT JOIN Vehicles v ON v.Id = b.VehicleId AND v.IsDeleted = 0
                  WHERE b.Id = @Id AND b.IsDeleted = 0 AND c.Phone = @Phone",
                new
                {
                    Id = request.BookingId,
                    Phone = phone,
                    Paid = (int)PaymentStatus.Paid,
                    Partial = (int)PaymentStatus.PartiallyPaid
                },
                cancellationToken: cancellationToken));

        if (head is null)
            return ApiResponse<PortalBookingDetailDto>.FailResponse("Booking not found for this phone number.");

        var h = head.Value;
        var remaining = h.TotalAmount - h.PaidAmount;
        if (remaining < 0) remaining = 0;
        var payState = PortalPayStateHelper.FromAmounts(h.TotalAmount, h.PaidAmount);

        var payments = await connection.QueryAsync<PortalPaymentLineDto>(
            new CommandDefinition(
                @"SELECT Id, Amount, Status, PaymentDate, PaymentMethod
                  FROM Payments
                  WHERE BookingId = @BookingId AND IsDeleted = 0
                  ORDER BY PaymentDate DESC",
                new { BookingId = request.BookingId },
                cancellationToken: cancellationToken));

        var dto = new PortalBookingDetailDto(
            h.Id,
            h.BookingNumber,
            h.RouteLabel,
            h.PickupTime,
            h.PassengerCount,
            h.VehicleName,
            (BookingStatus)h.Status,
            h.TotalAmount,
            h.PaidAmount,
            remaining,
            payState,
            payments.ToList());

        return ApiResponse<PortalBookingDetailDto>.SuccessResponse(dto, "Booking loaded.");
    }
}
