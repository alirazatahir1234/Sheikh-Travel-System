using Dapper;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.Bookings.Commands;

public record UpdateBookingStatusCommand(int Id, BookingStatus Status) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Update";
    public string AuditEntityName => "Booking";
    public int? AuditEntityId => Id;
}

public class UpdateBookingStatusCommandValidator : AbstractValidator<UpdateBookingStatusCommand>
{
    public UpdateBookingStatusCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Status).IsInEnum();
    }
}

public class UpdateBookingStatusCommandHandler(IDbConnectionFactory dbFactory, ILogger<UpdateBookingStatusCommandHandler> logger)
    : IRequestHandler<UpdateBookingStatusCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdateBookingStatusCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();

        var currentStatus = await connection.ExecuteScalarAsync<int?>(
            new CommandDefinition(
                "SELECT Status FROM Bookings WHERE Id = @Id AND IsDeleted = 0",
                new { request.Id },
                cancellationToken: cancellationToken));

        if (currentStatus is null)
            throw new NotFoundException("Booking", request.Id);

        var current = (BookingStatus)currentStatus.Value;

        // Validate status transitions
        var valid = (current, request.Status) switch
        {
            (BookingStatus.Pending, BookingStatus.Confirmed) => true,
            (BookingStatus.Pending, BookingStatus.Cancelled) => true,
            (BookingStatus.Confirmed, BookingStatus.Started) => true,
            (BookingStatus.Confirmed, BookingStatus.Cancelled) => true,
            (BookingStatus.Started, BookingStatus.Completed) => true,
            _ => false
        };

        if (!valid)
            return ApiResponse<bool>.FailResponse($"Cannot transition from {current} to {request.Status}.");

        await connection.ExecuteAsync(
            new CommandDefinition(
                @"UPDATE Bookings SET Status = @Status, UpdatedAt = @UpdatedAt,
                  DropoffTime = CASE WHEN @Status = @CompletedStatus THEN @Now ELSE DropoffTime END
                  WHERE Id = @Id",
                new
                {
                    Status = (int)request.Status, UpdatedAt = DateTime.UtcNow,
                    CompletedStatus = (int)BookingStatus.Completed, Now = DateTime.UtcNow, request.Id
                },
                cancellationToken: cancellationToken));

        logger.LogInformation("Booking {BookingId} status transitioned from {From} to {To}", request.Id, current, request.Status);
        return ApiResponse<bool>.SuccessResponse(true, $"Booking status updated to {request.Status}.");
    }
}
