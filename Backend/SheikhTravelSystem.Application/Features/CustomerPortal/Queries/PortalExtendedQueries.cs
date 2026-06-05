using System.Text;
using Dapper;
using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.CustomerPortal.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Queries;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.CustomerPortal.Queries;

public record GetPortalBookingTrackingQuery(int BookingId, string Phone, int? CustomerId = null)
    : IRequest<ApiResponse<PortalBookingTrackingDto>>;

public class GetPortalBookingTrackingQueryValidator : AbstractValidator<GetPortalBookingTrackingQuery>
{
    public GetPortalBookingTrackingQueryValidator()
    {
        RuleFor(x => x.BookingId).GreaterThan(0);
        RuleFor(x => x.Phone).NotEmpty().MaximumLength(20);
    }
}

public class GetPortalBookingTrackingQueryHandler(IDbConnectionFactory dbFactory, ISender mediator)
    : IRequestHandler<GetPortalBookingTrackingQuery, ApiResponse<PortalBookingTrackingDto>>
{
    public async Task<ApiResponse<PortalBookingTrackingDto>> Handle(
        GetPortalBookingTrackingQuery request,
        CancellationToken cancellationToken)
    {
        if (!await PortalBookingAccess.CustomerOwnsBookingAsync(
                dbFactory, request.BookingId, request.Phone, request.CustomerId, cancellationToken))
        {
            return ApiResponse<PortalBookingTrackingDto>.FailResponse("Booking not found for this phone number.");
        }

        using var connection = dbFactory.CreateConnection();
        var head = await connection.QuerySingleOrDefaultAsync<(int Status, int? VehicleId)>(
            new CommandDefinition(
                "SELECT Status, VehicleId FROM Bookings WHERE Id = @Id AND IsDeleted = 0",
                new { Id = request.BookingId },
                cancellationToken: cancellationToken));

        if (head.Status == 0)
        {
            return ApiResponse<PortalBookingTrackingDto>.FailResponse("Booking not found.");
        }

        var status = (BookingStatus)head.Status;
        var trackable = status is BookingStatus.Confirmed or BookingStatus.Started && head.VehicleId.HasValue;

        if (!trackable)
        {
            return ApiResponse<PortalBookingTrackingDto>.SuccessResponse(
                new PortalBookingTrackingDto(
                    request.BookingId,
                    head.VehicleId,
                    null,
                    status,
                    false,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null),
                "Tracking not available for this booking.");
        }

        var eta = await mediator.Send(new GetGpsEtaQuery(request.BookingId), cancellationToken);
        if (!eta.Success || eta.Data is null)
        {
            return ApiResponse<PortalBookingTrackingDto>.SuccessResponse(
                new PortalBookingTrackingDto(
                    request.BookingId,
                    head.VehicleId,
                    null,
                    status,
                    false,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null),
                eta.Message ?? "No live GPS yet.");
        }

        var d = eta.Data;
        var live = await connection.QueryFirstOrDefaultAsync<(decimal? Speed, DateTime? UpdatedAt)>(
            new CommandDefinition(
                @"SELECT Speed, Timestamp FROM VehicleCurrentLocation WHERE VehicleId = @VehicleId",
                new { d.VehicleId },
                cancellationToken: cancellationToken));

        var driverPhone = await connection.ExecuteScalarAsync<string?>(
            new CommandDefinition(
                @"SELECT d.Phone FROM Bookings b
                  INNER JOIN Drivers d ON d.Id = b.DriverId
                  WHERE b.Id = @Id AND b.Status = @Started",
                new { Id = request.BookingId, Started = (int)BookingStatus.Started },
                cancellationToken: cancellationToken));

        string? masked = null;
        if (!string.IsNullOrWhiteSpace(driverPhone) && driverPhone.Length >= 4)
            masked = new string('*', driverPhone.Length - 4) + driverPhone[^4..];

        return ApiResponse<PortalBookingTrackingDto>.SuccessResponse(
            new PortalBookingTrackingDto(
                request.BookingId,
                d.VehicleId,
                d.VehicleName,
                status,
                true,
                d.DriverLatitude,
                d.DriverLongitude,
                d.PickupLatitude,
                d.PickupLongitude,
                d.DistanceKm,
                d.EtaMinutes,
                live.Speed,
                live.UpdatedAt,
                masked),
            "Tracking loaded.");
    }
}

public record GetPortalBookingInvoiceQuery(int BookingId, string Phone, int? CustomerId = null)
    : IRequest<ApiResponse<byte[]>>;

public class GetPortalBookingInvoiceQueryValidator : AbstractValidator<GetPortalBookingInvoiceQuery>
{
    public GetPortalBookingInvoiceQueryValidator()
    {
        RuleFor(x => x.BookingId).GreaterThan(0);
        RuleFor(x => x.Phone).NotEmpty().MaximumLength(20);
    }
}

public class GetPortalBookingInvoiceQueryHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<GetPortalBookingInvoiceQuery, ApiResponse<byte[]>>
{
    public async Task<ApiResponse<byte[]>> Handle(GetPortalBookingInvoiceQuery request, CancellationToken cancellationToken)
    {
        if (!await PortalBookingAccess.CustomerOwnsBookingAsync(
                dbFactory, request.BookingId, request.Phone, request.CustomerId, cancellationToken))
        {
            return ApiResponse<byte[]>.FailResponse("Booking not found for this phone number.");
        }

        using var connection = dbFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<(
            string BookingNumber,
            string RouteLabel,
            DateTime PickupTime,
            decimal TotalAmount,
            decimal PaidAmount,
            string CustomerName,
            string CustomerPhone)>(
            new CommandDefinition(
                @"SELECT b.BookingNumber,
                         ISNULL(r.Source + N' → ' + r.Destination, N'') AS RouteLabel,
                         b.PickupTime,
                         b.TotalAmount,
                         ISNULL((
                           SELECT SUM(p.Amount)
                           FROM Payments p
                           WHERE p.BookingId = b.Id
                             AND p.Status IN (@Paid, @Partial)
                             AND p.IsDeleted = 0
                         ), 0) AS PaidAmount,
                         c.FullName AS CustomerName,
                         c.Phone AS CustomerPhone
                  FROM Bookings b
                  INNER JOIN Customers c ON c.Id = b.CustomerId
                  LEFT JOIN Routes r ON r.Id = b.RouteId
                  WHERE b.Id = @Id AND b.IsDeleted = 0",
                new
                {
                    Id = request.BookingId,
                    Paid = (int)PaymentStatus.Paid,
                    Partial = (int)PaymentStatus.PartiallyPaid
                },
                cancellationToken: cancellationToken));

        if (string.IsNullOrEmpty(row.BookingNumber))
        {
            return ApiResponse<byte[]>.FailResponse("Booking not found.");
        }

        var remaining = Math.Max(0, row.TotalAmount - row.PaidAmount);
        var html = new StringBuilder();
        html.Append("<!DOCTYPE html><html><head><meta charset=\"utf-8\"><title>Invoice ")
            .Append(row.BookingNumber)
            .Append("</title><style>body{font-family:system-ui,sans-serif;padding:24px;color:#0f172a}table{width:100%;border-collapse:collapse}td,th{padding:8px;border-bottom:1px solid #e2e8f0;text-align:left}h1{color:#1B7F75}</style></head><body>");
        html.Append("<h1>Sheikh Travel</h1><p>Invoice for booking <strong>").Append(row.BookingNumber).Append("</strong></p>");
        html.Append("<table><tr><th>Customer</th><td>").Append(row.CustomerName).Append(" (").Append(row.CustomerPhone).Append(")</td></tr>");
        html.Append("<tr><th>Route</th><td>").Append(row.RouteLabel).Append("</td></tr>");
        html.Append("<tr><th>Pickup</th><td>").Append(row.PickupTime.ToString("f")).Append("</td></tr>");
        html.Append("<tr><th>Total</th><td>PKR ").Append(row.TotalAmount.ToString("N2")).Append("</td></tr>");
        html.Append("<tr><th>Paid</th><td>PKR ").Append(row.PaidAmount.ToString("N2")).Append("</td></tr>");
        html.Append("<tr><th>Remaining</th><td>PKR ").Append(remaining.ToString("N2")).Append("</td></tr></table>");
        html.Append("<p style=\"margin-top:24px;font-size:12px;color:#64748b\">Generated ").Append(DateTime.UtcNow.ToString("u")).Append(" UTC</p></body></html>");

        return ApiResponse<byte[]>.SuccessResponse(Encoding.UTF8.GetBytes(html.ToString()), "Invoice ready.");
    }
}

public record CancelPortalBookingCommand(int BookingId, string Phone, int? CustomerId = null)
    : IRequest<ApiResponse<bool>>;

public class CancelPortalBookingCommandValidator : AbstractValidator<CancelPortalBookingCommand>
{
    public CancelPortalBookingCommandValidator()
    {
        RuleFor(x => x.BookingId).GreaterThan(0);
        RuleFor(x => x.Phone).NotEmpty().MaximumLength(20);
    }
}

public class CancelPortalBookingCommandHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<CancelPortalBookingCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(CancelPortalBookingCommand request, CancellationToken cancellationToken)
    {
        if (!await PortalBookingAccess.CustomerOwnsBookingAsync(
                dbFactory, request.BookingId, request.Phone, request.CustomerId, cancellationToken))
        {
            return ApiResponse<bool>.FailResponse("Booking not found for this phone number.");
        }

        using var connection = dbFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<(int Status, DateTime PickupTime)>(
            new CommandDefinition(
                "SELECT Status, PickupTime FROM Bookings WHERE Id = @Id AND IsDeleted = 0",
                new { Id = request.BookingId },
                cancellationToken: cancellationToken));

        if (row.Status == 0)
        {
            return ApiResponse<bool>.FailResponse("Booking not found.");
        }

        var status = (BookingStatus)row.Status;
        if (status is BookingStatus.Started or BookingStatus.Completed or BookingStatus.Cancelled)
        {
            return ApiResponse<bool>.FailResponse("This booking can no longer be cancelled online.");
        }

        if (row.PickupTime <= DateTime.UtcNow.AddHours(-1))
        {
            return ApiResponse<bool>.FailResponse("Pickup time has passed; contact support to cancel.");
        }

        await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE Bookings SET Status = @Status, UpdatedAt = SYSUTCDATETIME() WHERE Id = @Id",
                new { Id = request.BookingId, Status = (int)BookingStatus.Cancelled },
                cancellationToken: cancellationToken));

        return ApiResponse<bool>.SuccessResponse(true, "Booking cancelled.");
    }
}

public record GetPortalNotificationPreferencesQuery(string Phone) : IRequest<ApiResponse<PortalNotificationPreferencesDto>>;

public class GetPortalNotificationPreferencesQueryHandler
    : IRequestHandler<GetPortalNotificationPreferencesQuery, ApiResponse<PortalNotificationPreferencesDto>>
{
    public Task<ApiResponse<PortalNotificationPreferencesDto>> Handle(
        GetPortalNotificationPreferencesQuery request,
        CancellationToken cancellationToken)
    {
        _ = request;
        var dto = new PortalNotificationPreferencesDto(true, false, null);
        return Task.FromResult(ApiResponse<PortalNotificationPreferencesDto>.SuccessResponse(dto));
    }
}

public record UpdatePortalNotificationPreferencesCommand(string Phone, UpdatePortalNotificationPreferencesRequest Body)
    : IRequest<ApiResponse<PortalNotificationPreferencesDto>>;

public class UpdatePortalNotificationPreferencesCommandHandler
    : IRequestHandler<UpdatePortalNotificationPreferencesCommand, ApiResponse<PortalNotificationPreferencesDto>>
{
    public Task<ApiResponse<PortalNotificationPreferencesDto>> Handle(
        UpdatePortalNotificationPreferencesCommand request,
        CancellationToken cancellationToken)
    {
        var dto = new PortalNotificationPreferencesDto(
            request.Body.SmsEnabled,
            request.Body.EmailEnabled,
            request.Body.Email);
        return Task.FromResult(ApiResponse<PortalNotificationPreferencesDto>.SuccessResponse(dto, "Preferences saved."));
    }
}

public record GetPortalPaymentGatewayInfoQuery : IRequest<ApiResponse<PortalPaymentGatewayInfoDto>>;

public class GetPortalPaymentGatewayInfoQueryHandler(Microsoft.Extensions.Configuration.IConfiguration configuration)
    : IRequestHandler<GetPortalPaymentGatewayInfoQuery, ApiResponse<PortalPaymentGatewayInfoDto>>
{
    public Task<ApiResponse<PortalPaymentGatewayInfoDto>> Handle(
        GetPortalPaymentGatewayInfoQuery request,
        CancellationToken cancellationToken)
    {
        _ = request;
        var enabled = bool.TryParse(configuration["PortalPaymentGateway:Enabled"], out var gwOn) && gwOn;
        var provider = configuration["PortalPaymentGateway:Provider"] ?? "Stripe";
        var dto = new PortalPaymentGatewayInfoDto(
            enabled,
            provider,
            enabled
                ? $"Online payments will open in {provider}."
                : "Online payments are not enabled yet. Use cash or bank transfer and record payment here.");
        return Task.FromResult(ApiResponse<PortalPaymentGatewayInfoDto>.SuccessResponse(dto));
    }
}
