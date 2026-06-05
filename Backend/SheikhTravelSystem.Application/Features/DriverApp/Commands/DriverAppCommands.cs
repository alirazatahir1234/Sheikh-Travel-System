using Dapper;
using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Bookings.Commands;
using SheikhTravelSystem.Application.Features.DriverApp.DTOs;
using SheikhTravelSystem.Application.Features.FuelLogs.Commands;
using SheikhTravelSystem.Application.Features.FuelLogs.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Commands;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.DriverApp.Commands;

public record DriverLoginCommand(string Phone, string Password) : IRequest<ApiResponse<DriverAuthResultDto>>;

public class DriverLoginCommandValidator : AbstractValidator<DriverLoginCommand>
{
    public DriverLoginCommandValidator()
    {
        RuleFor(x => x.Phone).NotEmpty();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public class DriverLoginCommandHandler(
    IDbConnectionFactory dbFactory,
    IPasswordHasher passwordHasher,
    IJwtTokenService jwtTokenService,
    ITenantContext tenantContext)
    : IRequestHandler<DriverLoginCommand, ApiResponse<DriverAuthResultDto>>
{
    public async Task<ApiResponse<DriverAuthResultDto>> Handle(DriverLoginCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        var row = await connection.QuerySingleOrDefaultAsync<DriverLoginRow>(new CommandDefinition(
            @"SELECT d.Id AS DriverId, u.Id AS UserId, d.TenantId, d.FullName, d.Phone, u.PasswordHash
              FROM Drivers d
              INNER JOIN Users u ON u.Id = d.UserId AND u.IsDeleted = 0 AND u.IsActive = 1 AND u.Role = @DriverRole
              WHERE (d.Phone = @Phone OR u.Phone = @Phone)
                AND d.IsDeleted = 0 AND d.IsActive = 1 AND d.TenantId = @TenantId",
            new { Phone = request.Phone.Trim(), TenantId = tenantId, DriverRole = (int)UserRole.Driver },
            cancellationToken: cancellationToken));

        if (row is null || !passwordHasher.Verify(request.Password, row.PasswordHash))
        {
            return ApiResponse<DriverAuthResultDto>.FailResponse("Invalid phone or password.");
        }

        var token = jwtTokenService.GenerateDriverAccessToken(
            row.DriverId, row.UserId, row.TenantId, row.FullName, row.Phone);

        return ApiResponse<DriverAuthResultDto>.SuccessResponse(
            new DriverAuthResultDto(token, row.DriverId, row.FullName, row.Phone),
            "Login successful.");
    }
}

public record DriverStartTripCommand(int BookingId) : IRequest<ApiResponse<bool>>;
public record DriverCompleteTripCommand(int BookingId) : IRequest<ApiResponse<bool>>;
public record DriverRejectTripCommand(int BookingId, string Reason) : IRequest<ApiResponse<bool>>;

public class DriverStartTripCommandHandler(
    IDbConnectionFactory dbFactory,
    ICurrentUserService currentUser,
    IMediator mediator)
    : IRequestHandler<DriverStartTripCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(DriverStartTripCommand request, CancellationToken cancellationToken)
    {
        if (!await OwnsBookingAsync(request.BookingId, cancellationToken))
            return ApiResponse<bool>.FailResponse("Trip not found or not assigned to you.");

        return await mediator.Send(new UpdateBookingStatusCommand(request.BookingId, BookingStatus.Started), cancellationToken);
    }

    private async Task<bool> OwnsBookingAsync(int bookingId, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var driverId = currentUser.DriverId;
        if (!driverId.HasValue) return false;
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT CASE WHEN EXISTS(SELECT 1 FROM Bookings WHERE Id = @Id AND DriverId = @DriverId AND IsDeleted = 0) THEN 1 ELSE 0 END",
            new { Id = bookingId, DriverId = driverId.Value },
            cancellationToken: cancellationToken));
    }
}

public class DriverCompleteTripCommandHandler(
    IDbConnectionFactory dbFactory,
    ICurrentUserService currentUser,
    IMediator mediator)
    : IRequestHandler<DriverCompleteTripCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(DriverCompleteTripCommand request, CancellationToken cancellationToken)
    {
        if (!await OwnsBookingAsync(request.BookingId, cancellationToken))
            return ApiResponse<bool>.FailResponse("Trip not found or not assigned to you.");

        return await mediator.Send(new UpdateBookingStatusCommand(request.BookingId, BookingStatus.Completed), cancellationToken);
    }

    private async Task<bool> OwnsBookingAsync(int bookingId, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var driverId = currentUser.DriverId;
        if (!driverId.HasValue) return false;
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT CASE WHEN EXISTS(SELECT 1 FROM Bookings WHERE Id = @Id AND DriverId = @DriverId AND IsDeleted = 0) THEN 1 ELSE 0 END",
            new { Id = bookingId, DriverId = driverId.Value },
            cancellationToken: cancellationToken));
    }
}

public class DriverRejectTripCommandHandler(
    IDbConnectionFactory dbFactory,
    ICurrentUserService currentUser,
    IMediator mediator)
    : IRequestHandler<DriverRejectTripCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(DriverRejectTripCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            return ApiResponse<bool>.FailResponse("Rejection reason is required.");

        if (!await OwnsBookingAsync(request.BookingId, cancellationToken))
            return ApiResponse<bool>.FailResponse("Trip not found or not assigned to you.");

        return await mediator.Send(
            new UpdateBookingStatusCommand(request.BookingId, BookingStatus.Cancelled, request.Reason.Trim()),
            cancellationToken);
    }

    private async Task<bool> OwnsBookingAsync(int bookingId, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var driverId = currentUser.DriverId;
        if (!driverId.HasValue) return false;
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT CASE WHEN EXISTS(SELECT 1 FROM Bookings WHERE Id = @Id AND DriverId = @DriverId AND IsDeleted = 0) THEN 1 ELSE 0 END",
            new { Id = bookingId, DriverId = driverId.Value },
            cancellationToken: cancellationToken));
    }
}

public record DriverPostLocationCommand(DriverLocationDto Location) : IRequest<ApiResponse<bool>>;

public class DriverPostLocationCommandHandler(
    IDbConnectionFactory dbFactory,
    ICurrentUserService currentUser,
    IMediator mediator)
    : IRequestHandler<DriverPostLocationCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(DriverPostLocationCommand request, CancellationToken cancellationToken)
    {
        var driverId = currentUser.DriverId;
        if (!driverId.HasValue)
            return ApiResponse<bool>.FailResponse("Driver identity required.");

        using var connection = dbFactory.CreateConnection();
        var active = await connection.QuerySingleOrDefaultAsync<(int VehicleId, int BookingId)?>(new CommandDefinition(
            @"SELECT TOP 1 VehicleId, Id FROM Bookings
              WHERE DriverId = @DriverId AND Status = @Started AND VehicleId IS NOT NULL AND IsDeleted = 0
              ORDER BY PickupTime DESC",
            new { DriverId = driverId.Value, Started = (int)BookingStatus.Started },
            cancellationToken: cancellationToken));

        if (active is null)
            return ApiResponse<bool>.FailResponse("No active started trip with a vehicle.");

        var loc = request.Location;
        var dto = new IngestPositionDto(
            active.Value.VehicleId,
            driverId.Value,
            active.Value.BookingId,
            null,
            loc.Latitude,
            loc.Longitude,
            loc.Speed,
            null,
            null,
            true);

        return await mediator.Send(new IngestPositionCommand(dto), cancellationToken);
    }
}

public record DriverSubmitFuelReceiptCommand(CreateFuelLogDto FuelLog) : IRequest<ApiResponse<int>>;

public class DriverSubmitFuelReceiptCommandHandler(IMediator mediator)
    : IRequestHandler<DriverSubmitFuelReceiptCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(DriverSubmitFuelReceiptCommand request, CancellationToken cancellationToken)
        => await mediator.Send(new CreateFuelLogCommand(request.FuelLog), cancellationToken);
}

file class DriverLoginRow
{
    public int DriverId { get; set; }
    public int UserId { get; set; }
    public int TenantId { get; set; }
    public string FullName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string PasswordHash { get; set; } = "";
}
