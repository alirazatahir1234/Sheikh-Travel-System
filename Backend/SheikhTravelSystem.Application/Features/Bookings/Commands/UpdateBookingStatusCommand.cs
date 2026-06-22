using Dapper;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Drivers;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.Bookings.Commands;

public record UpdateBookingStatusCommand(int Id, BookingStatus Status, string? CancellationReason = null) : IRequest<ApiResponse<bool>>, IAuditableCommand
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

public class UpdateBookingStatusCommandHandler(
    IDbConnectionFactory dbFactory,
    ITenantContext tenantContext,
    ILogger<UpdateBookingStatusCommandHandler> logger)
    : IRequestHandler<UpdateBookingStatusCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdateBookingStatusCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        DriverAssignmentValidation.OpenConnection(connection);
        using var transaction = connection.BeginTransaction();
        var tenantId = tenantContext.GetRequiredTenantId();

        try
        {
            var currentStatus = await connection.ExecuteScalarAsync<int?>(
                new CommandDefinition(
                    "SELECT Status FROM Bookings WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
                    new { request.Id, TenantId = tenantId },
                    transaction: transaction,
                    cancellationToken: cancellationToken));

            if (currentStatus is null)
                throw new NotFoundException("Booking", request.Id);

            var current = (BookingStatus)currentStatus.Value;

            var valid = (current, request.Status) switch
            {
                (BookingStatus.Pending, BookingStatus.Confirmed) => true,
                (BookingStatus.Pending, BookingStatus.Cancelled) => true,
                (BookingStatus.Confirmed, BookingStatus.Started) => true,
                (BookingStatus.Confirmed, BookingStatus.Cancelled) => true,
                (BookingStatus.Started, BookingStatus.Completed) => true,
                (BookingStatus.Started, BookingStatus.Cancelled) => true,
                _ => false
            };

            if (!valid)
                return ApiResponse<bool>.FailResponse($"Cannot transition from {current} to {request.Status}.");

            if (request.Status == BookingStatus.Cancelled && string.IsNullOrWhiteSpace(request.CancellationReason))
                return ApiResponse<bool>.FailResponse("Cancellation reason is required.");

            var booking = await connection.QuerySingleOrDefaultAsync<(int? DriverId, int? VehicleId)>(
                new CommandDefinition(
                    "SELECT DriverId, VehicleId FROM Bookings WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
                    new { request.Id, TenantId = tenantId },
                    transaction: transaction,
                    cancellationToken: cancellationToken));

            await connection.ExecuteAsync(
                new CommandDefinition(
                    @"UPDATE Bookings SET Status = @Status, UpdatedAt = @UpdatedAt,
                      CancellationReason = @CancellationReason,
                      DropoffTime = CASE WHEN @Status = @CompletedStatus THEN @Now ELSE DropoffTime END
                      WHERE Id = @Id AND TenantId = @TenantId",
                    new
                    {
                        Status = (int)request.Status,
                        UpdatedAt = DateTime.UtcNow,
                        CancellationReason = request.CancellationReason,
                        CompletedStatus = (int)BookingStatus.Completed,
                        Now = DateTime.UtcNow,
                        request.Id,
                        TenantId = tenantId
                    },
                    transaction: transaction,
                    cancellationToken: cancellationToken));

            if (booking.DriverId is int driverId)
            {
                var driverStatus = request.Status switch
                {
                    BookingStatus.Started => DriverStatus.OnTrip,
                    BookingStatus.Completed or BookingStatus.Cancelled => DriverStatus.Available,
                    _ => (DriverStatus?)null
                };

                if (driverStatus.HasValue)
                {
                    await connection.ExecuteAsync(
                        new CommandDefinition(
                            @"UPDATE Drivers SET Status = @Status, UpdatedAt = GETUTCDATE()
                              WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
                            new { Status = (int)driverStatus.Value, Id = driverId, TenantId = tenantId },
                            transaction: transaction,
                            cancellationToken: cancellationToken));
                }
            }

            if (booking.VehicleId is int vehicleId)
            {
                var vehicleStatus = request.Status switch
                {
                    BookingStatus.Started => VehicleStatus.OnTrip,
                    BookingStatus.Completed or BookingStatus.Cancelled => VehicleStatus.Available,
                    _ => (VehicleStatus?)null
                };

                if (vehicleStatus.HasValue)
                {
                    await connection.ExecuteAsync(
                        new CommandDefinition(
                            @"UPDATE Vehicles SET Status = @Status, UpdatedAt = GETUTCDATE()
                              WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
                            new { Status = (int)vehicleStatus.Value, Id = vehicleId, TenantId = tenantId },
                            transaction: transaction,
                            cancellationToken: cancellationToken));
                }
            }

            transaction.Commit();
            logger.LogInformation("Booking {BookingId} status transitioned from {From} to {To}", request.Id, current, request.Status);
            return ApiResponse<bool>.SuccessResponse(true, $"Booking status updated to {request.Status}.");
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }
}
