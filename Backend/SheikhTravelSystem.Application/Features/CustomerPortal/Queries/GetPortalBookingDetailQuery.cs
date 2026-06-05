using Dapper;
using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.CustomerPortal.DTOs;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.CustomerPortal.Queries;

public record GetPortalBookingDetailQuery(int BookingId, string Phone, int? CustomerId = null)
    : IRequest<ApiResponse<PortalBookingDetailDto>>;

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
        var customerIds = await PortalBookingAccess.ResolvePortalCustomerIdsAsync(
            dbFactory, request.Phone, request.CustomerId, cancellationToken);
        if (customerIds.Count == 0)
            return ApiResponse<PortalBookingDetailDto>.FailResponse("Booking not found for this phone number.");

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
            decimal PaidAmount,
            string? PickupAddress,
            string? DropoffAddress,
            int? DriverId,
            string? DriverName,
            decimal? DriverRating,
            int? DriverYears)?>(
            new CommandDefinition(
                @"SELECT b.Id,
                         b.BookingNumber,
                         ISNULL(NULLIF(b.PickupAddress + N' → ' + b.DropoffAddress, N' → '), ISNULL(r.Source + N' → ' + r.Destination, N'')) AS RouteLabel,
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
                         ), 0) AS PaidAmount,
                         b.PickupAddress,
                         b.DropoffAddress,
                         b.DriverId,
                         d.FullName AS DriverName,
                         d.Rating AS DriverRating,
                         d.YearsExperience AS DriverYears
                  FROM Bookings b
                  LEFT JOIN Routes r ON r.Id = b.RouteId AND r.IsDeleted = 0
                  LEFT JOIN Vehicles v ON v.Id = b.VehicleId AND v.IsDeleted = 0
                  LEFT JOIN Drivers d ON d.Id = b.DriverId AND d.IsDeleted = 0
                  WHERE b.Id = @Id AND b.IsDeleted = 0 AND b.CustomerId IN @CustomerIds",
                new
                {
                    Id = request.BookingId,
                    CustomerIds = customerIds,
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
                  FROM Payments WHERE BookingId = @BookingId AND IsDeleted = 0 ORDER BY PaymentDate DESC",
                new { BookingId = request.BookingId },
                cancellationToken: cancellationToken));

        var seats = await connection.QueryAsync<string>(
            new CommandDefinition(
                "SELECT SeatLabel FROM BookingSeats WHERE BookingId = @BookingId ORDER BY SeatLabel",
                new { BookingId = request.BookingId },
                cancellationToken: cancellationToken));

        PortalDriverPreviewDto? driver = null;
        if (h.DriverId.HasValue && !string.IsNullOrWhiteSpace(h.DriverName))
        {
            driver = new PortalDriverPreviewDto(h.DriverName, h.DriverRating, h.DriverYears, true);
        }

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
            payments.ToList(),
            h.PickupAddress,
            h.DropoffAddress,
            driver,
            seats.ToList());

        return ApiResponse<PortalBookingDetailDto>.SuccessResponse(dto, "Booking loaded.");
    }
}
