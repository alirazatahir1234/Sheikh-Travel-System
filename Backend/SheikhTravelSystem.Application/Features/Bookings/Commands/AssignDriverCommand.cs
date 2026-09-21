using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;

namespace SheikhTravelSystem.Application.Features.Bookings.Commands;

public record AssignDriverCommand(int BookingId, int DriverId) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "AssignDriver";
    public string AuditEntityName => "Booking";
    public int? AuditEntityId => BookingId;
}

public class AssignDriverCommandHandler(IBookingRepository bookingRepository)
    : IRequestHandler<AssignDriverCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(AssignDriverCommand request, CancellationToken cancellationToken)
    {
        var result = await bookingRepository.AssignDriverAsync(request.BookingId, request.DriverId, cancellationToken);
        if (!result.Success)
            return ApiResponse<bool>.FailResponse(result.ErrorMessage!);

        return ApiResponse<bool>.SuccessResponse(true, "Driver assigned successfully.");
    }
}
