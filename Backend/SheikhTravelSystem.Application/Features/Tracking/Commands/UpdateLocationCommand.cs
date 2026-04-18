using Dapper;
using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Tracking.DTOs;

namespace SheikhTravelSystem.Application.Features.Tracking.Commands;

public record UpdateLocationCommand(UpdateLocationDto Location) : IRequest<ApiResponse<bool>>;

public class UpdateLocationCommandValidator : AbstractValidator<UpdateLocationCommand>
{
    public UpdateLocationCommandValidator()
    {
        RuleFor(x => x.Location.VehicleId).GreaterThan(0);
        RuleFor(x => x.Location.Latitude).InclusiveBetween(-90, 90);
        RuleFor(x => x.Location.Longitude).InclusiveBetween(-180, 180);
    }
}

public class UpdateLocationCommandHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<UpdateLocationCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdateLocationCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var dto = request.Location;

        await connection.ExecuteAsync(
            new CommandDefinition(
                @"INSERT INTO VehicleTracking (VehicleId, DriverId, BookingId, Latitude, Longitude, Speed, Timestamp, CreatedAt, IsDeleted)
                  VALUES (@VehicleId, @DriverId, @BookingId, @Latitude, @Longitude, @Speed, @Timestamp, @CreatedAt, 0)",
                new
                {
                    dto.VehicleId, dto.DriverId, dto.BookingId,
                    dto.Latitude, dto.Longitude, dto.Speed,
                    Timestamp = DateTime.UtcNow, CreatedAt = DateTime.UtcNow
                },
                cancellationToken: cancellationToken));

        return ApiResponse<bool>.SuccessResponse(true, "Location updated.");
    }
}
