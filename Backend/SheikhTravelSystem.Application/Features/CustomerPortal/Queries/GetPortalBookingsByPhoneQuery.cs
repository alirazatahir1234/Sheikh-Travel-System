using Dapper;
using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.CustomerPortal.DTOs;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.CustomerPortal.Queries;

public record GetPortalBookingsByPhoneQuery(string Phone) : IRequest<ApiResponse<IReadOnlyList<PortalBookingCardDto>>>;

public class GetPortalBookingsByPhoneQueryValidator : AbstractValidator<GetPortalBookingsByPhoneQuery>
{
    public GetPortalBookingsByPhoneQueryValidator()
    {
        RuleFor(x => x.Phone).NotEmpty().MaximumLength(20);
    }
}

public class GetPortalBookingsByPhoneQueryHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<GetPortalBookingsByPhoneQuery, ApiResponse<IReadOnlyList<PortalBookingCardDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<PortalBookingCardDto>>> Handle(GetPortalBookingsByPhoneQuery request, CancellationToken cancellationToken)
    {
        var phone = request.Phone.Trim();
        using var connection = dbFactory.CreateConnection();

        var rows = await connection.QueryAsync<(
            int Id,
            string BookingNumber,
            string RouteLabel,
            DateTime PickupTime,
            int Status,
            decimal TotalAmount,
            decimal PaidAmount)>(
            new CommandDefinition(
                @"SELECT b.Id,
                         b.BookingNumber,
                         ISNULL(r.Source + N' → ' + r.Destination, N'') AS RouteLabel,
                         b.PickupTime,
                         b.Status AS Status,
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
                  WHERE b.IsDeleted = 0 AND c.Phone = @Phone
                  ORDER BY b.PickupTime DESC",
                new
                {
                    Phone = phone,
                    Paid = (int)PaymentStatus.Paid,
                    Partial = (int)PaymentStatus.PartiallyPaid
                },
                cancellationToken: cancellationToken));

        var list = rows.Select(r =>
        {
            var remaining = r.TotalAmount - r.PaidAmount;
            if (remaining < 0) remaining = 0;
            var payState = PortalPayStateHelper.FromAmounts(r.TotalAmount, r.PaidAmount);
            return new PortalBookingCardDto(
                r.Id,
                r.BookingNumber,
                r.RouteLabel,
                r.PickupTime,
                (BookingStatus)r.Status,
                r.TotalAmount,
                r.PaidAmount,
                remaining,
                payState);
        }).ToList();

        return ApiResponse<IReadOnlyList<PortalBookingCardDto>>.SuccessResponse(list, "Bookings loaded.");
    }
}
