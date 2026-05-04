using Dapper;
using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

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

public class BulkDeleteBookingsCommandHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<BulkDeleteBookingsCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(BulkDeleteBookingsCommand request, CancellationToken cancellationToken)
    {
        var ids = request.Ids.Distinct().Where(id => id > 0).ToArray();
        if (ids.Length == 0)
            return ApiResponse<int>.FailResponse("No valid booking ids were provided.");

        using var connection = dbFactory.CreateConnection();

        var affected = await connection.ExecuteAsync(
            new CommandDefinition(
                @"UPDATE Bookings SET IsDeleted = 1, UpdatedAt = @UpdatedAt
                  WHERE IsDeleted = 0 AND Id IN @Ids",
                new { Ids = ids, UpdatedAt = DateTime.UtcNow },
                cancellationToken: cancellationToken));

        return ApiResponse<int>.SuccessResponse(
            affected,
            affected == 1 ? "1 booking deleted." : $"{affected} bookings deleted.");
    }
}
