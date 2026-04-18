using Dapper;
using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Bookings.DTOs;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.Bookings.Commands;

public record CreateBookingCommand(CreateBookingDto Booking) : IRequest<ApiResponse<int>>;

public class CreateBookingCommandValidator : AbstractValidator<CreateBookingCommand>
{
    public CreateBookingCommandValidator()
    {
        RuleFor(x => x.Booking.CustomerId).GreaterThan(0);
        RuleFor(x => x.Booking.RouteId).GreaterThan(0);
        RuleFor(x => x.Booking.PickupTime).GreaterThan(DateTime.UtcNow)
            .WithMessage("Pickup time must be in the future.");
        RuleFor(x => x.Booking.PassengerCount).GreaterThan(0);
        RuleFor(x => x.Booking.TotalAmount).GreaterThan(0);
    }
}

public class CreateBookingCommandHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<CreateBookingCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(CreateBookingCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var dto = request.Booking;

        // Verify customer exists
        var customerExists = await connection.ExecuteScalarAsync<bool>(
            "SELECT CASE WHEN EXISTS(SELECT 1 FROM Customers WHERE Id = @Id AND IsDeleted = 0) THEN 1 ELSE 0 END",
            new { Id = dto.CustomerId });

        if (!customerExists)
            throw new NotFoundException("Customer", dto.CustomerId);

        // Verify route exists
        var routeExists = await connection.ExecuteScalarAsync<bool>(
            "SELECT CASE WHEN EXISTS(SELECT 1 FROM Routes WHERE Id = @Id AND IsDeleted = 0) THEN 1 ELSE 0 END",
            new { Id = dto.RouteId });

        if (!routeExists)
            throw new NotFoundException("Route", dto.RouteId);

        var id = await connection.ExecuteScalarAsync<int>(
            @"INSERT INTO Bookings (CustomerId, RouteId, PickupTime, PassengerCount, TotalAmount, Status, Notes, CreatedAt, IsDeleted)
              VALUES (@CustomerId, @RouteId, @PickupTime, @PassengerCount, @TotalAmount, @Status, @Notes, @CreatedAt, 0);
              SELECT SCOPE_IDENTITY();",
            new
            {
                dto.CustomerId, dto.RouteId, dto.PickupTime, dto.PassengerCount,
                dto.TotalAmount, Status = (int)BookingStatus.Pending, dto.Notes,
                CreatedAt = DateTime.UtcNow
            });

        return ApiResponse<int>.SuccessResponse(id, "Booking created successfully.");
    }
}
