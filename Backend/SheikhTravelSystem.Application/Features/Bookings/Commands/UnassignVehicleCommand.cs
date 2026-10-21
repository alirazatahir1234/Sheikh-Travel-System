using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;

namespace SheikhTravelSystem.Application.Features.Bookings.Commands;

public record UnassignVehicleCommand(int BookingId) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "UnassignVehicle";
    public string AuditEntityName => "Booking";
    public int? AuditEntityId => BookingId;
}

public class UnassignVehicleCommandHandler(IBookingRepository bookingRepository)
    : IRequestHandler<UnassignVehicleCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UnassignVehicleCommand request, CancellationToken cancellationToken)
    {
        var result = await bookingRepository.UnassignVehicleAsync(request.BookingId, cancellationToken);
        if (!result.Success)
            return ApiResponse<bool>.FailResponse(result.ErrorMessage!);

        return ApiResponse<bool>.SuccessResponse(true, "Vehicle unassigned successfully.");
    }
}
