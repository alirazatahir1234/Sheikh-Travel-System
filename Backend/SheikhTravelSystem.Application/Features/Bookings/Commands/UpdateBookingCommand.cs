using Dapper;
using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Bookings.DTOs;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.Bookings.Commands;

public record UpdateBookingCommand(int Id, UpdateBookingDto Booking) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Update";
    public string AuditEntityName => "Booking";
    public int? AuditEntityId => Id;
}

public class UpdateBookingCommandValidator : AbstractValidator<UpdateBookingCommand>
{
    public UpdateBookingCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Booking.CustomerId).GreaterThan(0);
        RuleFor(x => x.Booking.RouteId).GreaterThan(0);
        RuleFor(x => x.Booking.PickupTime).NotEmpty();
        RuleFor(x => x.Booking.PassengerCount).GreaterThan(0);
        RuleFor(x => x.Booking.TotalAmount).GreaterThan(0);
    }
}

public class UpdateBookingCommandHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<UpdateBookingCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdateBookingCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var dto = request.Booking;

        var currentStatus = await connection.ExecuteScalarAsync<int?>(
            new CommandDefinition(
                "SELECT Status FROM Bookings WHERE Id = @Id",
                new { request.Id },
                cancellationToken: cancellationToken));

        if (currentStatus is null)
            throw new NotFoundException("Booking", request.Id);

        var status = (BookingStatus)currentStatus.Value;
        if (status == BookingStatus.Completed || status == BookingStatus.Cancelled)
            return ApiResponse<bool>.FailResponse($"Cannot edit a {status} booking.");

        var customerExists = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT CASE WHEN EXISTS(SELECT 1 FROM Customers WHERE Id = @Id AND IsDeleted = 0) THEN 1 ELSE 0 END",
                new { Id = dto.CustomerId },
                cancellationToken: cancellationToken));

        if (!customerExists)
            throw new NotFoundException("Customer", dto.CustomerId);

        var routeExists = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT CASE WHEN EXISTS(SELECT 1 FROM Routes WHERE Id = @Id AND IsDeleted = 0) THEN 1 ELSE 0 END",
                new { Id = dto.RouteId },
                cancellationToken: cancellationToken));

        if (!routeExists)
            throw new NotFoundException("Route", dto.RouteId);

        var normalizedVehicleId = dto.VehicleId;
        if (dto.VehicleId.HasValue)
        {
            var vehicleExists = await connection.ExecuteScalarAsync<bool>(
                new CommandDefinition(
                    "SELECT CASE WHEN EXISTS(SELECT 1 FROM Vehicles WHERE Id = @Id AND IsDeleted = 0) THEN 1 ELSE 0 END",
                    new { Id = dto.VehicleId },
                    cancellationToken: cancellationToken));

            // Keep edit flow resilient when a previously assigned vehicle was archived.
            if (!vehicleExists)
                normalizedVehicleId = null;
        }

        var normalizedDriverId = dto.DriverId;
        if (dto.DriverId.HasValue)
        {
            var driverExists = await connection.ExecuteScalarAsync<bool>(
                new CommandDefinition(
                    "SELECT CASE WHEN EXISTS(SELECT 1 FROM Drivers WHERE Id = @Id AND IsDeleted = 0) THEN 1 ELSE 0 END",
                    new { Id = dto.DriverId },
                    cancellationToken: cancellationToken));

            // Keep edit flow resilient when a previously assigned driver was archived.
            if (!driverExists)
                normalizedDriverId = null;
        }

        await connection.ExecuteAsync(
            new CommandDefinition(
                @"UPDATE Bookings SET
                    CustomerId = @CustomerId,
                    RouteId = @RouteId,
                    PickupTime = @PickupTime,
                    PassengerCount = @PassengerCount,
                    TotalAmount = @TotalAmount,
                    VehicleId = @VehicleId,
                    DriverId = @DriverId,
                    Notes = @Notes,
                    IsDeleted = 0,
                    UpdatedAt = @UpdatedAt
                  WHERE Id = @Id",
                new
                {
                    request.Id,
                    dto.CustomerId,
                    dto.RouteId,
                    dto.PickupTime,
                    dto.PassengerCount,
                    dto.TotalAmount,
                    VehicleId = normalizedVehicleId,
                    DriverId = normalizedDriverId,
                    dto.Notes,
                    UpdatedAt = DateTime.UtcNow
                },
                cancellationToken: cancellationToken));

        return ApiResponse<bool>.SuccessResponse(true, "Booking updated successfully.");
    }
}
