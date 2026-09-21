using MediatR;
using Microsoft.Extensions.Configuration;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.CustomerPortal.DTOs;

namespace SheikhTravelSystem.Application.Features.CustomerPortal.Commands;

public record CreatePortalPaymentCheckoutCommand(int BookingId, decimal Amount, string Phone, int? CustomerId = null)
    : IRequest<ApiResponse<PortalPaymentCheckoutDto>>;

public record PortalPaymentCheckoutDto(string CheckoutUrl, string SessionId, string Provider);

public class CreatePortalPaymentCheckoutCommandHandler(
    IPaymentGatewayService gateway,
    IConfiguration configuration,
    ICustomerPortalRepository portalRepository)
    : IRequestHandler<CreatePortalPaymentCheckoutCommand, ApiResponse<PortalPaymentCheckoutDto>>
{
    public async Task<ApiResponse<PortalPaymentCheckoutDto>> Handle(
        CreatePortalPaymentCheckoutCommand request,
        CancellationToken cancellationToken)
    {
        var enabled = bool.TryParse(configuration["PortalPaymentGateway:Enabled"], out var on) && on;
        if (!enabled)
            return ApiResponse<PortalPaymentCheckoutDto>.FailResponse("Online payments are not enabled.");

        if (string.IsNullOrWhiteSpace(request.Phone))
            return ApiResponse<PortalPaymentCheckoutDto>.FailResponse("Portal customer token is missing a phone number.");

        if (!await portalRepository.CustomerOwnsBookingAsync(
                request.BookingId, request.Phone, request.CustomerId, cancellationToken))
            return ApiResponse<PortalPaymentCheckoutDto>.FailResponse("Booking not found for this phone number.");

        var booking = await portalRepository.GetCheckoutBookingAsync(request.BookingId, cancellationToken);

        if (booking is null)
            return ApiResponse<PortalPaymentCheckoutDto>.FailResponse("Booking not found.");

        var remaining = booking.TotalAmount - booking.PaidAmount;
        if (request.Amount <= 0 || request.Amount > remaining)
            return ApiResponse<PortalPaymentCheckoutDto>.FailResponse("Invalid checkout amount.");

        var result = await gateway.CreateCheckoutSessionAsync(
            new PaymentCheckoutRequest(
                request.BookingId,
                request.Amount,
                "PKR",
                booking.Email ?? "",
                "/payments?success=1",
                "/payments?cancel=1"),
            cancellationToken);

        if (!result.Success || string.IsNullOrEmpty(result.CheckoutUrl))
            return ApiResponse<PortalPaymentCheckoutDto>.FailResponse(result.Error ?? "Checkout failed.");

        return ApiResponse<PortalPaymentCheckoutDto>.SuccessResponse(
            new PortalPaymentCheckoutDto(result.CheckoutUrl, result.SessionId!, gateway.ProviderName));
    }
}
