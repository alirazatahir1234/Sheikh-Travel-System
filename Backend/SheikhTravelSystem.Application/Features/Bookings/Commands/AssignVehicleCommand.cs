using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;

namespace SheikhTravelSystem.Application.Features.Bookings.Commands;

public record AssignVehicleCommand(int BookingId, int VehicleId) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "AssignVehicle";
    public string AuditEntityName => "Booking";
    public int? AuditEntityId => BookingId;
}

public class AssignVehicleCommandHandler(IBookingRepository bookingRepository)
    : IRequestHandler<AssignVehicleCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(AssignVehicleCommand request, CancellationToken cancellationToken)
    {
        var result = await bookingRepository.AssignVehicleAsync(request.BookingId, request.VehicleId, cancellationToken);
        if (!result.Success)
            return ApiResponse<bool>.FailResponse(result.ErrorMessage!);

        return ApiResponse<bool>.SuccessResponse(true, "Vehicle assigned successfully.");
    }
}
