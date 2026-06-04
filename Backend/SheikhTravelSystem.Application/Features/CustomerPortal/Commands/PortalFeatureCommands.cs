using Dapper;
using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.CustomerPortal.DTOs;

namespace SheikhTravelSystem.Application.Features.CustomerPortal.Commands;

public record PortalPointToPointQuoteCommand(PortalPointToPointQuoteRequest Request)
    : IRequest<ApiResponse<PortalQuoteResultDto>>;

public class PortalPointToPointQuoteCommandValidator : AbstractValidator<PortalPointToPointQuoteCommand>
{
    public PortalPointToPointQuoteCommandValidator()
    {
        RuleFor(x => x.Request.VehicleId).GreaterThan(0);
    }
}

public class PortalPointToPointQuoteCommandHandler(IDbConnectionFactory dbFactory, ISender mediator)
    : IRequestHandler<PortalPointToPointQuoteCommand, ApiResponse<PortalQuoteResultDto>>
{
    public async Task<ApiResponse<PortalQuoteResultDto>> Handle(
        PortalPointToPointQuoteCommand request,
        CancellationToken cancellationToken)
    {
        var r = request.Request;
        var quote = await PortalDynamicPricingHelper.CalculatePointToPointQuoteAsync(
            mediator,
            dbFactory,
            r.VehicleId,
            r.PickupLat,
            r.PickupLng,
            r.DropLat,
            r.DropLng,
            r.IsRoundTrip,
            r.RouteId,
            cancellationToken);

        if (!quote.Success || quote.Data is null)
            return ApiResponse<PortalQuoteResultDto>.FailResponse(quote.Message ?? "Could not calculate fare.");

        var distanceKm = PortalDynamicPricingHelper.HaversineDistanceKm(
            r.PickupLat, r.PickupLng, r.DropLat, r.DropLng);
        var duration = PortalDynamicPricingHelper.EstimateDurationMinutes(distanceKm);

        string? routeLabel = null;
        if (r.RouteId is > 0)
        {
            using var connection = dbFactory.CreateConnection();
            routeLabel = await connection.ExecuteScalarAsync<string?>(
                new CommandDefinition(
                    "SELECT Source + N' → ' + Destination FROM Routes WHERE Id = @Id",
                    new { Id = r.RouteId },
                    cancellationToken: cancellationToken));
        }

        return ApiResponse<PortalQuoteResultDto>.SuccessResponse(
            new PortalQuoteResultDto(quote.Data, distanceKm, duration, routeLabel),
            "Quote ready.");
    }
}

public record ValidatePortalPromoCommand(string Phone, PortalValidatePromoRequest Request)
    : IRequest<ApiResponse<PortalPromoResultDto>>;

public class ValidatePortalPromoCommandHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<ValidatePortalPromoCommand, ApiResponse<PortalPromoResultDto>>
{
    public async Task<ApiResponse<PortalPromoResultDto>> Handle(
        ValidatePortalPromoCommand request,
        CancellationToken cancellationToken)
    {
        var code = request.Request.Code.Trim().ToUpperInvariant();
        using var connection = dbFactory.CreateConnection();
        var promo = await connection.QuerySingleOrDefaultAsync<(int Id, decimal? Pct, decimal? Fixed)>(
            new CommandDefinition(
                @"SELECT Id, DiscountPercent, DiscountFixed FROM PromoCodes
                  WHERE Code = @Code AND IsActive = 1 AND IsDeleted = 0
                    AND (ValidFrom IS NULL OR ValidFrom <= SYSUTCDATETIME())
                    AND (ValidTo IS NULL OR ValidTo >= SYSUTCDATETIME())",
                new { Code = code },
                cancellationToken: cancellationToken));

        if (promo.Id == 0)
            return ApiResponse<PortalPromoResultDto>.SuccessResponse(
                new PortalPromoResultDto(false, code, 0, "Invalid or expired promo code."));

        var discount = promo.Pct is > 0
            ? Math.Round(request.Request.QuoteTotal * promo.Pct.Value / 100m, 2)
            : promo.Fixed ?? 0;

        if (discount <= 0)
            return ApiResponse<PortalPromoResultDto>.SuccessResponse(
                new PortalPromoResultDto(false, code, 0, "Promo code has no discount value."));

        if (discount > request.Request.QuoteTotal)
            discount = request.Request.QuoteTotal;

        return ApiResponse<PortalPromoResultDto>.SuccessResponse(
            new PortalPromoResultDto(true, code, discount, $"Promo applied: PKR {discount:N2} off."),
            "Promo valid.");
    }
}

public static class PortalCustomerWriter
{
    public static async Task<int?> ResolveCustomerIdByPhoneAsync(
        IDbConnectionFactory dbFactory,
        string phone,
        CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int?>(
            new CommandDefinition(
                "SELECT TOP 1 Id FROM Customers WHERE Phone = @Phone AND IsDeleted = 0",
                new { Phone = phone.Trim() },
                cancellationToken: cancellationToken));
    }

    public static async Task WriteCustomerNotificationAsync(
        IDbConnectionFactory dbFactory,
        int customerId,
        string title,
        string message,
        string type,
        int? bookingId,
        CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(
            new CommandDefinition(
                @"INSERT INTO CustomerNotifications (CustomerId, Title, Message, NotificationType, BookingId, IsRead, CreatedAt, IsDeleted)
                  VALUES (@CustomerId, @Title, @Message, @Type, @BookingId, 0, SYSUTCDATETIME(), 0)",
                new { CustomerId = customerId, Title = title, Message = message, Type = type, BookingId = bookingId },
                cancellationToken: cancellationToken));
    }

    public static async Task EnsureLoyaltyRowAsync(IDbConnectionFactory dbFactory, int customerId, CancellationToken ct)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(
            new CommandDefinition(
                @"IF NOT EXISTS (SELECT 1 FROM CustomerLoyalty WHERE CustomerId = @Id)
                  INSERT INTO CustomerLoyalty (CustomerId, Points, Tier) VALUES (@Id, 0, 'Bronze')",
                new { Id = customerId },
                cancellationToken: ct));
    }

    public static async Task AddLoyaltyPointsAsync(IDbConnectionFactory dbFactory, int customerId, int points, CancellationToken ct)
    {
        await EnsureLoyaltyRowAsync(dbFactory, customerId, ct);
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE CustomerLoyalty SET Points = Points + @Pts, UpdatedAt = SYSUTCDATETIME() WHERE CustomerId = @Id",
                new { Pts = points, Id = customerId },
                cancellationToken: ct));
    }
}
