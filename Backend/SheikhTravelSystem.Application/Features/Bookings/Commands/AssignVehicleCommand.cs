using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.Bookings.Commands;

public record AssignVehicleCommand(int BookingId, int VehicleId) : IRequest<ApiResponse<bool>>;

public class AssignVehicleCommandHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<AssignVehicleCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(AssignVehicleCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();

        var bookingStatus = await connection.ExecuteScalarAsync<int?>(
            new CommandDefinition(
                "SELECT Status FROM Bookings WHERE Id = @Id AND IsDeleted = 0",
                new { Id = request.BookingId },
                cancellationToken: cancellationToken));

        if (bookingStatus is null)
            throw new NotFoundException("Booking", request.BookingId);

        if (bookingStatus != (int)BookingStatus.Pending && bookingStatus != (int)BookingStatus.Confirmed)
            return ApiResponse<bool>.FailResponse("Can only assign vehicle to pending or confirmed bookings.");

        var vehicleStatus = await connection.ExecuteScalarAsync<int?>(
            new CommandDefinition(
                "SELECT Status FROM Vehicles WHERE Id = @Id AND IsDeleted = 0",
                new { Id = request.VehicleId },
                cancellationToken: cancellationToken));

        if (vehicleStatus is null)
            throw new NotFoundException("Vehicle", request.VehicleId);

        if (vehicleStatus != (int)VehicleStatus.Available)
            return ApiResponse<bool>.FailResponse("Vehicle is not available.");

        // Check for double booking
        var booking = await connection.QuerySingleAsync<(DateTime PickupTime, DateTime? DropoffTime)>(
            new CommandDefinition(
                "SELECT PickupTime, DropoffTime FROM Bookings WHERE Id = @Id",
                new { Id = request.BookingId },
                cancellationToken: cancellationToken));

        var vehicleConflict = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                @"SELECT CASE WHEN EXISTS(
                    SELECT 1 FROM Bookings
                    WHERE VehicleId = @VehicleId AND IsDeleted = 0
                    AND Status IN (@Confirmed, @Started)
                    AND Id != @BookingId
                    AND PickupTime < DATEADD(HOUR, 4, @PickupTime)
                    AND DATEADD(HOUR, 4, PickupTime) > @PickupTime
                  ) THEN 1 ELSE 0 END",
                new
                {
                    request.VehicleId, request.BookingId,
                    Confirmed = (int)BookingStatus.Confirmed,
                    Started = (int)BookingStatus.Started,
                    booking.PickupTime
                },
                cancellationToken: cancellationToken));

        if (vehicleConflict)
            return ApiResponse<bool>.FailResponse("Vehicle has a conflicting booking at this time.");

        await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE Bookings SET VehicleId = @VehicleId, UpdatedAt = @Now WHERE Id = @Id",
                new { request.VehicleId, Now = DateTime.UtcNow, Id = request.BookingId },
                cancellationToken: cancellationToken));

        return ApiResponse<bool>.SuccessResponse(true, "Vehicle assigned successfully.");
    }
}
