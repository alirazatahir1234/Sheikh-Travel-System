using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.WhatsApp.Automation;
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
    IBookingRepository bookingRepository,
    ITenantContext tenantContext,
    IWhatsAppAutomationHooks automationHooks,
    ILogger<UpdateBookingStatusCommandHandler> logger)
    : IRequestHandler<UpdateBookingStatusCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdateBookingStatusCommand request, CancellationToken cancellationToken)
    {
        var result = await bookingRepository.UpdateStatusAsync(
            request.Id,
            request.Status,
            request.CancellationReason,
            cancellationToken);

        if (!result.Success)
            return ApiResponse<bool>.FailResponse(result.ErrorMessage!);

        logger.LogInformation(
            "Booking {BookingId} status transitioned from {From} to {To}",
            request.Id, result.PreviousStatus, request.Status);

        try
        {
            var tenantId = tenantContext.GetRequiredTenantId();
            await automationHooks.OnBookingStatusChangedAsync(
                tenantId, request.Id, request.Status, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "WhatsApp automation hook failed for booking {BookingId}", request.Id);
        }

        return ApiResponse<bool>.SuccessResponse(true, result.SuccessMessage!);
    }
}
