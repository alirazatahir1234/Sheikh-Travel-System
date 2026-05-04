using Dapper;
using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;

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

public class DeleteBookingCommandHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<DeleteBookingCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(DeleteBookingCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();

        var rowsAffected = await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE Bookings SET IsDeleted = 1, UpdatedAt = @UpdatedAt WHERE Id = @Id AND IsDeleted = 0",
                new { request.Id, UpdatedAt = DateTime.UtcNow },
                cancellationToken: cancellationToken));

        if (rowsAffected == 0)
            throw new NotFoundException("Booking", request.Id);

        return ApiResponse<bool>.SuccessResponse(true, "Booking deleted successfully.");
    }
}
