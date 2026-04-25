using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.Bookings.Commands;

public record AssignDriverCommand(int BookingId, int DriverId) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "AssignDriver";
    public string AuditEntityName => "Booking";
    public int? AuditEntityId => BookingId;
}

public class AssignDriverCommandHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<AssignDriverCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(AssignDriverCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();

        // Verify booking exists and is in valid state
        var bookingStatus = await connection.ExecuteScalarAsync<int?>(
            new CommandDefinition(
                "SELECT Status FROM Bookings WHERE Id = @Id AND IsDeleted = 0",
                new { Id = request.BookingId },
                cancellationToken: cancellationToken));

        if (bookingStatus is null)
            throw new NotFoundException("Booking", request.BookingId);

        if (bookingStatus != (int)BookingStatus.Pending && bookingStatus != (int)BookingStatus.Confirmed)
            return ApiResponse<bool>.FailResponse("Can only assign driver to pending or confirmed bookings.");

        // Verify driver exists and is available
        var driverStatus = await connection.ExecuteScalarAsync<int?>(
            new CommandDefinition(
                "SELECT Status FROM Drivers WHERE Id = @Id AND IsDeleted = 0 AND IsActive = 1",
                new { Id = request.DriverId },
                cancellationToken: cancellationToken));

        if (driverStatus is null)
            throw new NotFoundException("Driver", request.DriverId);

        if (driverStatus != (int)DriverStatus.Available)
            return ApiResponse<bool>.FailResponse("Driver is not available.");

        // Check for double booking - driver already assigned to active trip at same time
        var booking = await connection.QuerySingleAsync<(DateTime PickupTime, DateTime? DropoffTime)>(
            new CommandDefinition(
                "SELECT PickupTime, DropoffTime FROM Bookings WHERE Id = @Id",
                new { Id = request.BookingId },
                cancellationToken: cancellationToken));

        var driverConflict = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                @"SELECT CASE WHEN EXISTS(
                    SELECT 1 FROM Bookings
                    WHERE DriverId = @DriverId AND IsDeleted = 0
                    AND Status IN (@Confirmed, @Started)
                    AND Id != @BookingId
                    AND PickupTime < DATEADD(HOUR, 4, @PickupTime)
                    AND DATEADD(HOUR, 4, PickupTime) > @PickupTime
                  ) THEN 1 ELSE 0 END",
                new
                {
                    request.DriverId, request.BookingId,
                    Confirmed = (int)BookingStatus.Confirmed,
                    Started = (int)BookingStatus.Started,
                    booking.PickupTime
                },
                cancellationToken: cancellationToken));

        if (driverConflict)
            return ApiResponse<bool>.FailResponse("Driver has a conflicting booking at this time.");

        await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE Bookings SET DriverId = @DriverId, UpdatedAt = @Now WHERE Id = @Id",
                new { request.DriverId, Now = DateTime.UtcNow, Id = request.BookingId },
                cancellationToken: cancellationToken));

        return ApiResponse<bool>.SuccessResponse(true, "Driver assigned successfully.");
    }
}
