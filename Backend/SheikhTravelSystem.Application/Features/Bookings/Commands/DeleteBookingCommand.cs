using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;

namespace SheikhTravelSystem.Application.Features.Bookings.Commands;

public record DeleteBookingCommand(int Id) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Delete";
    public string AuditEntityName => "Booking";
    public int? AuditEntityId => Id;
}

public class DeleteBookingCommandValidator : AbstractValidator<DeleteBookingCommand>
{
    public DeleteBookingCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
    }
}

public class DeleteBookingCommandHandler(IBookingRepository bookingRepository)
    : IRequestHandler<DeleteBookingCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(DeleteBookingCommand request, CancellationToken cancellationToken)
    {
        await bookingRepository.SoftDeleteAsync(request.Id, cancellationToken);
        return ApiResponse<bool>.SuccessResponse(true, "Booking deleted successfully.");
    }
}
