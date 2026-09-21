using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
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

public class UpdateBookingCommandHandler(IBookingRepository bookingRepository)
    : IRequestHandler<UpdateBookingCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdateBookingCommand request, CancellationToken cancellationToken)
    {
        var result = await bookingRepository.UpdateAsync(request.Id, request.Booking, cancellationToken);
        if (!result.Success)
            return ApiResponse<bool>.FailResponse(result.ErrorMessage!);

        return ApiResponse<bool>.SuccessResponse(true, "Booking updated successfully.");
    }
}
