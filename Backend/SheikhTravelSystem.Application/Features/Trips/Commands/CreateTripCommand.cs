using Dapper;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Drivers;
using SheikhTravelSystem.Application.Features.Notifications;
using SheikhTravelSystem.Application.Features.Trips.DTOs;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.Trips.Commands;

public record CreateTripCommand(CreateTripDto Trip) : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => "Create";
    public string AuditEntityName => "Trip";
    public int? AuditEntityId => null;
}

public class CreateTripCommandValidator : AbstractValidator<CreateTripCommand>
{
    public CreateTripCommandValidator()
    {
        RuleFor(x => x.Trip.TripName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Trip.CustomerId).GreaterThan(0);
        RuleFor(x => x.Trip.PassengerCount).GreaterThan(0);
        RuleFor(x => x.Trip.TripType).IsInEnum();
        RuleFor(x => x.Trip.Priority).IsInEnum();
        RuleFor(x => x.Trip.PlannedStart).NotEmpty();
    }
}

public class CreateTripCommandHandler(
    IDbConnectionFactory dbFactory,
    ITenantContext tenantContext,
    ICurrentUserService currentUser,
    ILogger<CreateTripCommandHandler> logger)
    : IRequestHandler<CreateTripCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(CreateTripCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        DriverAssignmentValidation.OpenConnection(connection);
        using var tx = connection.BeginTransaction();
        var tenantId = tenantContext.GetRequiredTenantId();
        var dto = request.Trip;
        var actor = currentUser.UserId?.ToString();

        try
        {
            var customerOk = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
                "SELECT CASE WHEN EXISTS(SELECT 1 FROM Customers WHERE Id = @Id AND IsDeleted = 0) THEN 1 ELSE 0 END",
                new { Id = dto.CustomerId }, transaction: tx, cancellationToken: cancellationToken));
            if (!customerOk)
                throw new NotFoundException("Customer", dto.CustomerId);

            if (dto.RouteId is int routeId)
            {
                var routeOk = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
                    "SELECT CASE WHEN EXISTS(SELECT 1 FROM Routes WHERE Id = @Id AND IsDeleted = 0) THEN 1 ELSE 0 END",
                    new { Id = routeId }, transaction: tx, cancellationToken: cancellationToken));
                if (!routeOk)
                    throw new NotFoundException("Route", routeId);
            }

            if (dto.BookingId is int bookingId)
            {
                var bookingOk = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
                    "SELECT CASE WHEN EXISTS(SELECT 1 FROM Bookings WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0) THEN 1 ELSE 0 END",
                    new { Id = bookingId, TenantId = tenantId }, transaction: tx, cancellationToken: cancellationToken));
                if (!bookingOk)
                    throw new NotFoundException("Booking", bookingId);
            }

            var initialStatus = dto.DriverId.HasValue || dto.VehicleId.HasValue
                ? TripLifecycle.ResolveAssignmentStatus(TripStatus.Scheduled, dto.DriverId.HasValue, dto.VehicleId.HasValue)
                : TripStatus.Scheduled;

            var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
                INSERT INTO Trips (
                    TenantId, TripNumber, BookingId, CustomerId, RouteId, TripName, TripType,
                    PickupAddress, PickupLatitude, PickupLongitude,
                    DestinationAddress, DestinationLatitude, DestinationLongitude,
                    TripDate, PlannedStart, PlannedEnd, EstimatedDurationMinutes,
                    DriverId, AssistantDriverId, VehicleId, PassengerCount, Priority, Status,
                    DriverNotes, PlannedDistanceKm, CreatedAt, CreatedBy, IsDeleted)
                VALUES (
                    @TenantId, '', @BookingId, @CustomerId, @RouteId, @TripName, @TripType,
                    @PickupAddress, @PickupLatitude, @PickupLongitude,
                    @DestinationAddress, @DestinationLatitude, @DestinationLongitude,
                    @TripDate, @PlannedStart, @PlannedEnd, @EstimatedDurationMinutes,
                    @DriverId, @AssistantDriverId, @VehicleId, @PassengerCount, @Priority, @Status,
                    @DriverNotes, @PlannedDistanceKm, GETUTCDATE(), @CreatedBy, 0);
                SELECT CAST(SCOPE_IDENTITY() AS INT);
                """,
                new
                {
                    TenantId = tenantId,
                    dto.BookingId,
                    dto.CustomerId,
                    dto.RouteId,
                    dto.TripName,
                    TripType = (int)dto.TripType,
                    dto.PickupAddress,
                    dto.PickupLatitude,
                    dto.PickupLongitude,
                    dto.DestinationAddress,
                    dto.DestinationLatitude,
                    dto.DestinationLongitude,
                    TripDate = dto.TripDate.Date,
                    dto.PlannedStart,
                    dto.PlannedEnd,
                    dto.EstimatedDurationMinutes,
                    dto.DriverId,
                    dto.AssistantDriverId,
                    dto.VehicleId,
                    dto.PassengerCount,
                    Priority = (int)dto.Priority,
                    Status = (int)initialStatus,
                    dto.DriverNotes,
                    dto.PlannedDistanceKm,
                    CreatedBy = actor
                },
                transaction: tx,
                cancellationToken: cancellationToken));

            var tripNumber = $"TR-{DateTime.UtcNow.Year}-{id:D4}";
            await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE Trips SET TripNumber = @TripNumber WHERE Id = @Id",
                new { TripNumber = tripNumber, Id = id },
                transaction: tx,
                cancellationToken: cancellationToken));

            await TripLifecycle.RecordStatusAsync(connection, tx, id, null, initialStatus, actor, "Trip created", cancellationToken);

            if (dto.Stops is { Count: > 0 })
            {
                await TripLifecycle.ReplaceStopsAsync(
                    connection, tx, id,
                    dto.Stops.Select(s => (s.Sequence, s.Location, s.Latitude, s.Longitude, s.Eta)).ToList(),
                    cancellationToken);
            }

            if (dto.DriverId is int driverId && dto.VehicleId is int vehicleId)
            {
                await TripLifecycle.EnsureAssignmentHistoryAsync(
                    connection, tx, tenantId, id, dto.BookingId, driverId, vehicleId,
                    dto.PickupAddress, dto.DestinationAddress, actor, cancellationToken);
            }

            tx.Commit();
            logger.LogInformation("Trip {TripId} ({TripNumber}) created", id, tripNumber);
            return ApiResponse<int>.SuccessResponse(id, "Trip created successfully.");
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }
}

public record UpdateTripCommand(int Id, UpdateTripDto Trip) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Update";
    public string AuditEntityName => "Trip";
    public int? AuditEntityId => Id;
}

public class UpdateTripCommandValidator : AbstractValidator<UpdateTripCommand>
{
    public UpdateTripCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Trip.TripName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Trip.CustomerId).GreaterThan(0);
        RuleFor(x => x.Trip.PassengerCount).GreaterThan(0);
    }
}

public class UpdateTripCommandHandler(
    IDbConnectionFactory dbFactory,
    ITenantContext tenantContext,
    ICurrentUserService currentUser,
    INotificationDecisionEngine decisionEngine)
    : IRequestHandler<UpdateTripCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdateTripCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        DriverAssignmentValidation.OpenConnection(connection);
        using var tx = connection.BeginTransaction();
        var tenantId = tenantContext.GetRequiredTenantId();
        var dto = request.Trip;

        try
        {
            var existing = await connection.QuerySingleOrDefaultAsync<(int Status, string TripNumber)>(new CommandDefinition(
                "SELECT Status, TripNumber FROM Trips WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
                new { request.Id, TenantId = tenantId }, transaction: tx, cancellationToken: cancellationToken));

            if (existing.TripNumber is null)
                throw new NotFoundException("Trip", request.Id);

            var current = (TripStatus)existing.Status;
            if (TripLifecycle.IsTerminal(current) || current is TripStatus.Started or TripStatus.AtPickup or TripStatus.Enroute)
                return ApiResponse<bool>.FailResponse("Cannot edit a trip that is in progress or completed.");

            var rows = await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE Trips SET
                    TripName = @TripName, TripType = @TripType, CustomerId = @CustomerId, RouteId = @RouteId,
                    PassengerCount = @PassengerCount, Priority = @Priority,
                    PickupAddress = @PickupAddress, PickupLatitude = @PickupLatitude, PickupLongitude = @PickupLongitude,
                    DestinationAddress = @DestinationAddress, DestinationLatitude = @DestinationLatitude, DestinationLongitude = @DestinationLongitude,
                    TripDate = @TripDate, PlannedStart = @PlannedStart, PlannedEnd = @PlannedEnd,
                    EstimatedDurationMinutes = @EstimatedDurationMinutes, PlannedDistanceKm = @PlannedDistanceKm,
                    DriverNotes = @DriverNotes, UpdatedAt = GETUTCDATE(), UpdatedBy = @UpdatedBy
                WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0
                """,
                new
                {
                    request.Id,
                    TenantId = tenantId,
                    dto.TripName,
                    TripType = (int)dto.TripType,
                    dto.CustomerId,
                    dto.RouteId,
                    dto.PassengerCount,
                    Priority = (int)dto.Priority,
                    dto.PickupAddress,
                    dto.PickupLatitude,
                    dto.PickupLongitude,
                    dto.DestinationAddress,
                    dto.DestinationLatitude,
                    dto.DestinationLongitude,
                    TripDate = dto.TripDate.Date,
                    dto.PlannedStart,
                    dto.PlannedEnd,
                    dto.EstimatedDurationMinutes,
                    dto.PlannedDistanceKm,
                    dto.DriverNotes,
                    UpdatedBy = currentUser.UserId?.ToString()
                },
                transaction: tx,
                cancellationToken: cancellationToken));

            if (dto.Stops is not null)
            {
                await TripLifecycle.ReplaceStopsAsync(
                    connection, tx, request.Id,
                    dto.Stops.Select(s => (s.Sequence, s.Location, s.Latitude, s.Longitude, s.Eta)).ToList(),
                    cancellationToken);
            }

            var tripNumber = existing.TripNumber;
            tx.Commit();

            await decisionEngine.DispatchIfAllowedAsync(new NotificationDecisionRequest(
                "trip_updated",
                $"Trip Updated: {tripNumber}",
                $"Trip {tripNumber} details were updated.",
                NotificationType.TripUpdated,
                ReferenceId: request.Id,
                SuggestedPriority: 2,
                RequestedChannels:
                [
                    NotificationChannels.InApp, NotificationChannels.Browser, NotificationChannels.Email
                ]), cancellationToken);

            return ApiResponse<bool>.SuccessResponse(rows > 0, "Trip updated.");
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }
}

public record DeleteTripCommand(int Id) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Delete";
    public string AuditEntityName => "Trip";
    public int? AuditEntityId => Id;
}

public class DeleteTripCommandHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<DeleteTripCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(DeleteTripCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        var status = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            "SELECT Status FROM Trips WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
            new { request.Id, TenantId = tenantId },
            cancellationToken: cancellationToken));

        if (status is null)
            throw new NotFoundException("Trip", request.Id);

        if ((TripStatus)status.Value is TripStatus.Started or TripStatus.AtPickup or TripStatus.Enroute or TripStatus.Delayed)
            return ApiResponse<bool>.FailResponse("Cannot delete an active trip. Cancel it first.");

        var rows = await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE Trips SET IsDeleted = 1, UpdatedAt = GETUTCDATE() WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
            new { request.Id, TenantId = tenantId },
            cancellationToken: cancellationToken));

        return ApiResponse<bool>.SuccessResponse(rows > 0, "Trip deleted.");
    }
}
