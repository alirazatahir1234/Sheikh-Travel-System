using Dapper;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Bookings.DTOs;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.Bookings.Commands;

/// <summary>
/// Creates a new booking from the provided booking DTO.
/// </summary>
public record CreateBookingCommand(CreateBookingDto Booking) : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => "Create";
    public string AuditEntityName => "Booking";
    public int? AuditEntityId => null;
}

/// <summary>
/// Validates booking creation inputs.
/// </summary>
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

/// <summary>
/// Handles booking creation and prerequisite checks.
/// </summary>
public class CreateBookingCommandHandler(IDbConnectionFactory dbFactory, ILogger<CreateBookingCommandHandler> logger)
    : IRequestHandler<CreateBookingCommand, ApiResponse<int>>
{
    /// <summary>
    /// Validates related entities and inserts the booking record.
    /// </summary>
    public async Task<ApiResponse<int>> Handle(CreateBookingCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var dto = request.Booking;

        // Verify customer exists
        var customerExists = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT CASE WHEN EXISTS(SELECT 1 FROM Customers WHERE Id = @Id AND IsDeleted = 0) THEN 1 ELSE 0 END",
                new { Id = dto.CustomerId },
                cancellationToken: cancellationToken));

        if (!customerExists)
            throw new NotFoundException("Customer", dto.CustomerId);

        // Verify route exists
        var routeExists = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT CASE WHEN EXISTS(SELECT 1 FROM Routes WHERE Id = @Id AND IsDeleted = 0) THEN 1 ELSE 0 END",
                new { Id = dto.RouteId },
                cancellationToken: cancellationToken));

        if (!routeExists)
            throw new NotFoundException("Route", dto.RouteId);

        var id = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                @"INSERT INTO Bookings (CustomerId, RouteId, PickupTime, PassengerCount, TotalAmount, Status, Notes, CreatedAt, IsDeleted)
                  VALUES (@CustomerId, @RouteId, @PickupTime, @PassengerCount, @TotalAmount, @Status, @Notes, @CreatedAt, 0);
                  SELECT SCOPE_IDENTITY();",
                new
                {
                    dto.CustomerId, dto.RouteId, dto.PickupTime, dto.PassengerCount,
                    dto.TotalAmount, Status = (int)BookingStatus.Pending, dto.Notes,
                    CreatedAt = DateTime.UtcNow
                },
                cancellationToken: cancellationToken));

        logger.LogInformation("Booking {BookingId} created for customer {CustomerId} on route {RouteId}", id, dto.CustomerId, dto.RouteId);
        return ApiResponse<int>.SuccessResponse(id, "Booking created successfully.");
    }
}
