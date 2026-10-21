using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;

namespace SheikhTravelSystem.Application.Features.Bookings.Commands;

public record UnassignDriverCommand(int BookingId) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "UnassignDriver";
    public string AuditEntityName => "Booking";
    public int? AuditEntityId => BookingId;
}

public class UnassignDriverCommandHandler(IBookingRepository bookingRepository)
    : IRequestHandler<UnassignDriverCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UnassignDriverCommand request, CancellationToken cancellationToken)
    {
        var result = await bookingRepository.UnassignDriverAsync(request.BookingId, cancellationToken);
        if (!result.Success)
            return ApiResponse<bool>.FailResponse(result.ErrorMessage!);

        return ApiResponse<bool>.SuccessResponse(true, "Driver unassigned successfully.");
    }
}
