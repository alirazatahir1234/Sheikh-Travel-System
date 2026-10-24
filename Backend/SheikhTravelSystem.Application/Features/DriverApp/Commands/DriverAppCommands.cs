using FluentValidation;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Auth.Commands;
using SheikhTravelSystem.Application.Features.Bookings.Commands;
using SheikhTravelSystem.Application.Features.DriverApp.DTOs;
using SheikhTravelSystem.Application.Features.FuelLogs.Commands;
using SheikhTravelSystem.Application.Features.FuelLogs.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Commands;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.Notifications;
using SheikhTravelSystem.Domain.Enums;
using System.Security.Cryptography;

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
    IDriverAppRepository repository,
    IPasswordHasher passwordHasher,
    IJwtTokenService jwtTokenService,
    IAuthSessionService authSessions,
    ITenantContext tenantContext)
    : IRequestHandler<DriverLoginCommand, ApiResponse<DriverAuthResultDto>>
{
    public async Task<ApiResponse<DriverAuthResultDto>> Handle(DriverLoginCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var row = await repository.GetDriverLoginByPhoneAsync(request.Phone, tenantId, cancellationToken);

        if (row is null || !passwordHasher.Verify(request.Password, row.PasswordHash))
            return ApiResponse<DriverAuthResultDto>.FailResponse("Invalid phone or password.");

        var refreshToken = jwtTokenService.GenerateRefreshToken();
        var refreshExpiry = DateTime.UtcNow.AddDays(30);
        // Tenant-wide AuthSessions cap (web + driver share; 0 = unlimited).
        var session = await authSessions.CreateSessionAsync(
            row.TenantId,
            row.UserId,
            refreshToken,
            refreshExpiry,
            AuthSessionLimits.ClientKindDriver,
            cancellationToken);
        if (session.LimitReached)
        {
            return ApiResponse<DriverAuthResultDto>.FailResponse(
                AuthSessionLimits.LimitReachedMessage,
                code: AuthSessionLimits.LimitReachedCode);
        }

        var accessToken = jwtTokenService.GenerateDriverAccessToken(
            row.DriverId, row.UserId, row.TenantId, row.FullName, row.Phone, session.SessionId);

        return ApiResponse<DriverAuthResultDto>.SuccessResponse(
            new DriverAuthResultDto(accessToken, refreshToken, row.DriverId, row.FullName, row.Phone),
            "Login successful.");
    }
}

public record DriverForgotPasswordCommand(string Phone) : IRequest<ApiResponse<object>>;

public class DriverForgotPasswordCommandValidator : AbstractValidator<DriverForgotPasswordCommand>
{
    public DriverForgotPasswordCommandValidator()
    {
        RuleFor(x => x.Phone).NotEmpty().MaximumLength(30);
    }
}

/// <summary>
/// Phone-based driver password reset. Reuses Users password-reset token columns;
/// always returns a generic success message (no account enumeration).
/// </summary>
public class DriverForgotPasswordCommandHandler(
    IDriverAppRepository driverRepository,
    IAuthRepository authRepository,
    ITenantContext tenantContext,
    IEnumerable<INotificationChannelSender> channelSenders,
    IConfiguration configuration,
    ILogger<DriverForgotPasswordCommandHandler> logger)
    : IRequestHandler<DriverForgotPasswordCommand, ApiResponse<object>>
{
    public async Task<ApiResponse<object>> Handle(DriverForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        const string okMessage = "If a driver account exists for that phone, a reset code has been sent.";

        var tenantId = tenantContext.GetRequiredTenantId();
        var row = await driverRepository.GetDriverLoginByPhoneAsync(request.Phone, tenantId, cancellationToken);
        if (row is null)
            return ApiResponse<object>.SuccessResponse(new { }, okMessage);

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(4));
        var tokenHash = ForgotPasswordCommandHandler.Sha256(token);
        var expiry = DateTime.UtcNow.AddHours(2);
        await authRepository.SetPasswordResetTokenAsync(row.UserId, tokenHash, expiry, cancellationToken);

        var sms = channelSenders.FirstOrDefault(s =>
            string.Equals(s.Channel, NotificationChannels.Sms, StringComparison.OrdinalIgnoreCase));
        if (sms is not null)
        {
            var body = $"SheikhGo Fleet reset code: {token}. Expires in 2 hours.";
            var result = await sms.SendAsync(
                new ChannelSendRequest(0, row.UserId, row.TenantId, "Password reset", body, NotificationChannels.Sms, Phone: row.Phone),
                cancellationToken);
            if (!result.Success)
                logger.LogWarning("Driver password reset SMS failed for user {UserId}: {Detail}", row.UserId, result.Response);
        }
        else
        {
            logger.LogWarning("Driver password reset requested but SMS channel is unavailable for user {UserId}", row.UserId);
        }

        var devMode = configuration.GetValue("PortalAuth:DevMode", false);
        object payload = devMode ? new { debugToken = token } : new { };
        return ApiResponse<object>.SuccessResponse(payload, okMessage);
    }
}

public record DriverResetPasswordCommand(string Token, string NewPassword) : IRequest<ApiResponse<object>>;

public class DriverResetPasswordCommandValidator : AbstractValidator<DriverResetPasswordCommand>
{
    public DriverResetPasswordCommandValidator()
    {
        RuleFor(x => x.Token).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(6);
    }
}

public class DriverResetPasswordCommandHandler(ISender mediator)
    : IRequestHandler<DriverResetPasswordCommand, ApiResponse<object>>
{
    public Task<ApiResponse<object>> Handle(DriverResetPasswordCommand request, CancellationToken cancellationToken)
        => mediator.Send(new ResetPasswordWithTokenCommand(request.Token, request.NewPassword), cancellationToken);
}

public record DriverStartTripCommand(int BookingId) : IRequest<ApiResponse<bool>>;
public record DriverCompleteTripCommand(int BookingId) : IRequest<ApiResponse<bool>>;
public record DriverRejectTripCommand(int BookingId, string Reason) : IRequest<ApiResponse<bool>>;

public class DriverStartTripCommandHandler(
    IDriverAppRepository repository,
    ICurrentUserService currentUser,
    ITenantContext tenantContext,
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
        var driverId = currentUser.DriverId;
        if (!driverId.HasValue) return false;
        return await repository.OwnsBookingAsync(
            bookingId, driverId.Value, tenantContext.GetRequiredTenantId(), cancellationToken);
    }
}

public class DriverCompleteTripCommandHandler(
    IDriverAppRepository repository,
    ICurrentUserService currentUser,
    ITenantContext tenantContext,
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
        var driverId = currentUser.DriverId;
        if (!driverId.HasValue) return false;
        return await repository.OwnsBookingAsync(
            bookingId, driverId.Value, tenantContext.GetRequiredTenantId(), cancellationToken);
    }
}

public class DriverRejectTripCommandHandler(
    IDriverAppRepository repository,
    ICurrentUserService currentUser,
    ITenantContext tenantContext,
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
        var driverId = currentUser.DriverId;
        if (!driverId.HasValue) return false;
        return await repository.OwnsBookingAsync(
            bookingId, driverId.Value, tenantContext.GetRequiredTenantId(), cancellationToken);
    }
}

public record DriverPostLocationCommand(DriverLocationDto Location) : IRequest<ApiResponse<bool>>;

public class DriverPostLocationCommandHandler(
    IDriverAppRepository repository,
    ICurrentUserService currentUser,
    ITenantContext tenantContext,
    IMediator mediator)
    : IRequestHandler<DriverPostLocationCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(DriverPostLocationCommand request, CancellationToken cancellationToken)
    {
        var driverId = currentUser.DriverId;
        if (!driverId.HasValue)
            return ApiResponse<bool>.FailResponse("Driver identity required.");

        var tenantId = tenantContext.GetRequiredTenantId();
        var active = await repository.GetActiveTripVehicleAsync(driverId.Value, tenantId, cancellationToken)
                     ?? await repository.GetActiveBookingVehicleAsync(driverId.Value, tenantId, cancellationToken);

        if (active is null)
            return ApiResponse<bool>.FailResponse("No active started trip with a vehicle.");

        var loc = request.Location;
        var dto = new IngestPositionDto(
            active.Value.VehicleId, driverId.Value, active.Value.BookingId, null,
            loc.Latitude, loc.Longitude, loc.Speed, null, null, true);

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

public record DriverCheckInCommand(double? Latitude, double? Longitude) : IRequest<ApiResponse<bool>>;
public record DriverCheckOutCommand(double? Latitude, double? Longitude) : IRequest<ApiResponse<bool>>;

public class DriverCheckInCommandHandler(
    IDriverAppRepository repository,
    ICurrentUserService currentUser,
    ITenantContext tenantContext)
    : IRequestHandler<DriverCheckInCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(DriverCheckInCommand request, CancellationToken cancellationToken)
    {
        var driverId = currentUser.DriverId;
        if (!driverId.HasValue) return ApiResponse<bool>.FailResponse("Driver identity required.");

        var now = DateTime.UtcNow;
        var tenantId = tenantContext.GetRequiredTenantId();
        var updated = await repository.UpdateCheckInAsync(
            driverId.Value, tenantId, now.Date, now, request.Latitude, request.Longitude, cancellationToken);
        if (updated == 0)
            await repository.InsertCheckInAsync(
                driverId.Value, tenantId, now.Date, now, request.Latitude, request.Longitude, cancellationToken);

        return ApiResponse<bool>.SuccessResponse(true, "Checked in successfully.");
    }
}

public class DriverCheckOutCommandHandler(
    IDriverAppRepository repository,
    ICurrentUserService currentUser,
    ITenantContext tenantContext)
    : IRequestHandler<DriverCheckOutCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(DriverCheckOutCommand request, CancellationToken cancellationToken)
    {
        var driverId = currentUser.DriverId;
        if (!driverId.HasValue) return ApiResponse<bool>.FailResponse("Driver identity required.");

        var now = DateTime.UtcNow;
        var tenantId = tenantContext.GetRequiredTenantId();
        var updated = await repository.UpdateCheckOutAsync(
            driverId.Value, tenantId, now.Date, now, request.Latitude, request.Longitude, cancellationToken);
        if (updated == 0)
            await repository.InsertCheckOutAsync(
                driverId.Value, tenantId, now.Date, now, request.Latitude, request.Longitude, cancellationToken);

        return ApiResponse<bool>.SuccessResponse(true, "Checked out successfully.");
    }
}

public record DriverPostLocationBatchCommand(List<DriverLocationDto> Positions) : IRequest<ApiResponse<bool>>;

public class DriverPostLocationBatchCommandHandler(IMediator mediator)
    : IRequestHandler<DriverPostLocationBatchCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(DriverPostLocationBatchCommand request, CancellationToken cancellationToken)
    {
        foreach (var pos in request.Positions)
            await mediator.Send(new DriverPostLocationCommand(pos), cancellationToken);
        return ApiResponse<bool>.SuccessResponse(true, $"{request.Positions.Count} positions ingested.");
    }
}

public record DriverSosCommand(double? Latitude, double? Longitude, string? Message)
    : IRequest<ApiResponse<DriverSosResultDto>>;

public class DriverSosCommandValidator : AbstractValidator<DriverSosCommand>
{
    public DriverSosCommandValidator()
    {
        RuleFor(x => x.Message).MaximumLength(500);
        RuleFor(x => x.Latitude).InclusiveBetween(-90, 90).When(x => x.Latitude.HasValue);
        RuleFor(x => x.Longitude).InclusiveBetween(-180, 180).When(x => x.Longitude.HasValue);
    }
}

public class DriverSosCommandHandler(
    IDriverAppRepository repository,
    ICurrentUserService currentUser,
    ITenantContext tenantContext,
    ILocationBroadcastService broadcaster,
    INotificationDecisionEngine decisionEngine)
    : IRequestHandler<DriverSosCommand, ApiResponse<DriverSosResultDto>>
{
    public async Task<ApiResponse<DriverSosResultDto>> Handle(DriverSosCommand request, CancellationToken cancellationToken)
    {
        var driverId = currentUser.DriverId;
        if (!driverId.HasValue)
            return ApiResponse<DriverSosResultDto>.FailResponse("Driver identity required.");

        var tenantId = tenantContext.GetRequiredTenantId();
        var driver = await repository.GetDriverNamePhoneAsync(driverId.Value, tenantId, cancellationToken);
        if (driver is null || driver.Value.FullName is null)
            return ApiResponse<DriverSosResultDto>.FailResponse("Driver not found.");

        var active = await repository.GetStartedBookingForSosAsync(driverId.Value, tenantId, cancellationToken);
        var vehicleId = active.VehicleId;
        var bookingId = active.BookingId;
        var createdAt = DateTime.UtcNow;

        var id = await repository.InsertSosAlertAsync(
            tenantId, driverId.Value, vehicleId, bookingId,
            request.Latitude, request.Longitude,
            string.IsNullOrWhiteSpace(request.Message) ? null : request.Message.Trim(),
            createdAt, cancellationToken);

        if (vehicleId.HasValue && request.Latitude.HasValue && request.Longitude.HasValue)
        {
            await broadcaster.BroadcastSosAlertAsync(
                vehicleId.Value, request.Latitude.Value, request.Longitude.Value, createdAt, cancellationToken);
        }

        var loc = request.Latitude.HasValue && request.Longitude.HasValue
            ? $" at {request.Latitude:F5},{request.Longitude:F5}" : "";
        var title = $"SOS — {driver.Value.FullName}";
        var message = $"Driver {driver.Value.FullName} ({driver.Value.Phone}) triggered SOS{loc}.";

        await decisionEngine.DispatchIfAllowedAsync(new NotificationDecisionRequest(
            "sos", title, message, NotificationType.Sos, ReferenceId: id, TenantId: tenantId,
            SuggestedPriority: 4,
            RequestedChannels: [NotificationChannels.InApp, NotificationChannels.Sms],
            Broadcast: false), cancellationToken);

        return ApiResponse<DriverSosResultDto>.SuccessResponse(new DriverSosResultDto(id, createdAt), "SOS alert sent.");
    }
}
