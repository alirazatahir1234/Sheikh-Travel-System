using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;

namespace SheikhTravelSystem.Application.Features.Bookings.Commands;

public record BulkDeleteBookingsCommand(IReadOnlyList<int> Ids) : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => "BulkDelete";
    public string AuditEntityName => "Booking";
    public int? AuditEntityId => null;
}

public class BulkDeleteBookingsCommandValidator : AbstractValidator<BulkDeleteBookingsCommand>
{
    public BulkDeleteBookingsCommandValidator()
    {
        RuleFor(x => x.Ids).NotNull().NotEmpty().WithMessage("Select at least one booking.");
        RuleFor(x => x.Ids.Count).LessThanOrEqualTo(500).WithMessage("You can delete at most 500 bookings at once.");
        RuleForEach(x => x.Ids).GreaterThan(0);
    }
}

public class BulkDeleteBookingsCommandHandler(IBookingRepository bookingRepository)
    : IRequestHandler<BulkDeleteBookingsCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(BulkDeleteBookingsCommand request, CancellationToken cancellationToken)
    {
        var ids = request.Ids.Distinct().Where(id => id > 0).ToArray();
        if (ids.Length == 0)
            return ApiResponse<int>.FailResponse("No valid booking ids were provided.");

        var affected = await bookingRepository.SoftDeleteManyAsync(ids, cancellationToken);

        return ApiResponse<int>.SuccessResponse(
            affected,
            affected == 1 ? "1 booking deleted." : $"{affected} bookings deleted.");
    }
}
