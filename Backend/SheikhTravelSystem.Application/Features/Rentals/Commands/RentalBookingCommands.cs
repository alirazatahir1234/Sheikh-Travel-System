using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Rentals.DTOs;

namespace SheikhTravelSystem.Application.Features.Rentals.Commands;

public record CreateRentalBookingCommand(CreateRentalBookingDto Booking)
    : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => "Create";
    public string AuditEntityName => "RentalBooking";
    public int? AuditEntityId => null;
}

public class CreateRentalBookingCommandValidator : AbstractValidator<CreateRentalBookingCommand>
{
    public CreateRentalBookingCommandValidator()
    {
        RuleFor(x => x.Booking.CustomerId).GreaterThan(0);
        RuleFor(x => x.Booking.VehicleId).GreaterThan(0);
        RuleFor(x => x.Booking.RateType).Must(RentalRateTypes.IsValid);
        RuleFor(x => x.Booking.EndAt).GreaterThan(x => x.Booking.StartAt)
            .WithMessage("End must be after start.");
        RuleFor(x => x.Booking.DiscountAmount).GreaterThanOrEqualTo(0).When(x => x.Booking.DiscountAmount.HasValue);
    }
}

public class CreateRentalBookingCommandHandler(
    IRentalRepository rentalRepository,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateRentalBookingCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(CreateRentalBookingCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var dto = request.Booking;

        if (!await rentalRepository.CustomerExistsAsync(dto.CustomerId, tenantId, cancellationToken))
            throw new NotFoundException("Customer", dto.CustomerId);
        if (!await rentalRepository.VehicleExistsAsync(dto.VehicleId, tenantId, cancellationToken))
            throw new NotFoundException("Vehicle", dto.VehicleId);

        if (await rentalRepository.HasOverlappingBookingAsync(
                tenantId, dto.VehicleId, dto.StartAt, dto.EndAt, null, cancellationToken))
            return ApiResponse<int>.FailResponse("Vehicle is not available for the selected period.");

        decimal rateAmount;
        decimal depositAmount;
        int? rateId = dto.RateId;

        if (rateId is int rid)
        {
            var rate = await rentalRepository.GetRateByIdAsync(rid, tenantId, cancellationToken)
                ?? throw new NotFoundException("RentalRate", rid);
            rateAmount = rate.Amount;
            depositAmount = dto.DepositAmount ?? rate.DepositAmount;
        }
        else
        {
            var rate = await rentalRepository.FindBestRateAsync(tenantId, dto.VehicleId, dto.RateType, cancellationToken);
            if (rate is null && dto.TotalAmount is null)
                return ApiResponse<int>.FailResponse("No active rental rate found for this vehicle and rate type.");
            rateAmount = rate?.Amount ?? 0;
            depositAmount = dto.DepositAmount ?? rate?.DepositAmount ?? 0;
            rateId = rate?.Id;
        }

        var baseTotal = dto.TotalAmount
            ?? RentalPricing.CalculateTotal(dto.RateType, rateAmount, dto.StartAt, dto.EndAt);
        var discount = Math.Max(0, dto.DiscountAmount ?? 0);
        var totalAmount = Math.Max(0, baseTotal - discount);

        var vehicle = await rentalRepository.GetVehicleSnapshotAsync(dto.VehicleId, tenantId, cancellationToken);
        var customerName = await rentalRepository.GetCustomerNameAsync(dto.CustomerId, tenantId, cancellationToken) ?? "Customer";
        var vehicleLabel = $"{vehicle?.Name ?? "Vehicle"} ({vehicle?.RegistrationNumber ?? "-"})";
        var rentalNumber = await rentalRepository.NextRentalNumberAsync(tenantId, cancellationToken);
        var agreement = string.IsNullOrWhiteSpace(dto.AgreementText)
            ? RentalPricing.DefaultAgreementText(rentalNumber, vehicleLabel, customerName)
            : dto.AgreementText;

        var createDto = dto with { RateId = rateId };
        var id = await rentalRepository.CreateBookingAsync(
            tenantId, createDto, rentalNumber, totalAmount, depositAmount, rateAmount,
            vehicle?.Model, agreement, currentUser.UserId, cancellationToken);

        return ApiResponse<int>.SuccessResponse(id, "Rental booking created.");
    }
}

public record UpdateRentalBookingCommand(int Id, UpdateRentalBookingDto Booking)
    : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Update";
    public string AuditEntityName => "RentalBooking";
    public int? AuditEntityId => Id;
}

public class UpdateRentalBookingCommandValidator : AbstractValidator<UpdateRentalBookingCommand>
{
    public UpdateRentalBookingCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Booking.EndAt).GreaterThan(x => x.Booking.StartAt);
    }
}

public class UpdateRentalBookingCommandHandler(IRentalRepository rentalRepository, ITenantContext tenantContext)
    : IRequestHandler<UpdateRentalBookingCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdateRentalBookingCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var existing = await rentalRepository.GetBookingByIdAsync(request.Id, tenantId, cancellationToken)
            ?? throw new NotFoundException("RentalBooking", request.Id);

        if (existing.Status is not (RentalBookingStatuses.Draft or RentalBookingStatuses.Confirmed))
            return ApiResponse<bool>.FailResponse("Only draft or confirmed rentals can be rescheduled.");

        if (await rentalRepository.HasOverlappingBookingAsync(
                tenantId, existing.VehicleId, request.Booking.StartAt, request.Booking.EndAt, request.Id, cancellationToken))
            return ApiResponse<bool>.FailResponse("Vehicle is not available for the selected period.");

        var ok = await rentalRepository.UpdateBookingScheduleAsync(
            request.Id, tenantId, request.Booking.StartAt, request.Booking.EndAt,
            request.Booking.PickupLocation, request.Booking.ReturnLocation, request.Booking.Notes,
            cancellationToken);
        return ApiResponse<bool>.SuccessResponse(ok);
    }
}

public record ConfirmRentalBookingCommand(int Id)
    : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Confirm";
    public string AuditEntityName => "RentalBooking";
    public int? AuditEntityId => Id;
}

public class ConfirmRentalBookingCommandHandler(IRentalRepository rentalRepository, ITenantContext tenantContext)
    : IRequestHandler<ConfirmRentalBookingCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(ConfirmRentalBookingCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var booking = await rentalRepository.GetBookingByIdAsync(request.Id, tenantId, cancellationToken)
            ?? throw new NotFoundException("RentalBooking", request.Id);

        if (booking.Status != RentalBookingStatuses.Draft)
            return ApiResponse<bool>.FailResponse("Only draft rentals can be confirmed.");

        if (await rentalRepository.HasOverlappingBookingAsync(
                tenantId, booking.VehicleId, booking.StartAt, booking.EndAt, booking.Id, cancellationToken))
            return ApiResponse<bool>.FailResponse("Vehicle is no longer available for this period.");

        var ok = await rentalRepository.UpdateBookingStatusAsync(
            request.Id, tenantId, RentalBookingStatuses.Confirmed, cancellationToken);
        return ApiResponse<bool>.SuccessResponse(ok, "Rental confirmed.");
    }
}

public record CancelRentalBookingCommand(int Id)
    : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Cancel";
    public string AuditEntityName => "RentalBooking";
    public int? AuditEntityId => Id;
}

public class CancelRentalBookingCommandHandler(IRentalRepository rentalRepository, ITenantContext tenantContext)
    : IRequestHandler<CancelRentalBookingCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(CancelRentalBookingCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var booking = await rentalRepository.GetBookingByIdAsync(request.Id, tenantId, cancellationToken)
            ?? throw new NotFoundException("RentalBooking", request.Id);

        if (booking.Status is RentalBookingStatuses.Returned or RentalBookingStatuses.Closed or RentalBookingStatuses.Cancelled)
            return ApiResponse<bool>.FailResponse("This rental cannot be cancelled.");

        if (booking.Status == RentalBookingStatuses.PickedUp)
            return ApiResponse<bool>.FailResponse("Complete return before cancelling, or keep the rental active.");

        var ok = await rentalRepository.UpdateBookingStatusAsync(
            request.Id, tenantId, RentalBookingStatuses.Cancelled, cancellationToken);
        return ApiResponse<bool>.SuccessResponse(ok, "Rental cancelled.");
    }
}

public record PickupRentalCommand(int Id, PickupRentalDto Payload)
    : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Pickup";
    public string AuditEntityName => "RentalBooking";
    public int? AuditEntityId => Id;
}

public class PickupRentalCommandHandler(IRentalRepository rentalRepository, ITenantContext tenantContext)
    : IRequestHandler<PickupRentalCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(PickupRentalCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var booking = await rentalRepository.GetBookingByIdAsync(request.Id, tenantId, cancellationToken)
            ?? throw new NotFoundException("RentalBooking", request.Id);

        if (booking.Status != RentalBookingStatuses.Confirmed)
            return ApiResponse<bool>.FailResponse("Only confirmed rentals can be picked up.");

        var hold = request.Payload.HoldDeposit;
        var depositStatus = hold && booking.DepositAmount > 0
            ? RentalDepositStatuses.Held
            : booking.DepositStatus;
        var depositHeld = hold ? booking.DepositAmount : booking.DepositHeld;

        var ok = await rentalRepository.MarkPickedUpAsync(
            request.Id, tenantId, DateTime.UtcNow, request.Payload.Odometer, request.Payload.FuelLevel,
            request.Payload.Notes, depositStatus, depositHeld, cancellationToken);
        return ok
            ? ApiResponse<bool>.SuccessResponse(true, "Vehicle picked up.")
            : ApiResponse<bool>.FailResponse("Pickup failed.");
    }
}

public record ReturnRentalCommand(int Id, ReturnRentalDto Payload)
    : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Return";
    public string AuditEntityName => "RentalBooking";
    public int? AuditEntityId => Id;
}

public class ReturnRentalCommandValidator : AbstractValidator<ReturnRentalCommand>
{
    public ReturnRentalCommandValidator()
    {
        RuleFor(x => x.Payload.DamageCharges).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Payload.DepositRefund).GreaterThanOrEqualTo(0).When(x => x.Payload.DepositRefund.HasValue);
    }
}

public class ReturnRentalCommandHandler(IRentalRepository rentalRepository, ITenantContext tenantContext)
    : IRequestHandler<ReturnRentalCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(ReturnRentalCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var booking = await rentalRepository.GetBookingByIdAsync(request.Id, tenantId, cancellationToken)
            ?? throw new NotFoundException("RentalBooking", request.Id);

        if (booking.Status != RentalBookingStatuses.PickedUp)
            return ApiResponse<bool>.FailResponse("Only picked-up rentals can be returned.");

        var damage = Math.Max(0, request.Payload.DamageCharges);
        var refundable = Math.Max(0, booking.DepositHeld - damage);
        var refund = request.Payload.DepositRefund.HasValue
            ? Math.Min(refundable, Math.Max(0, request.Payload.DepositRefund.Value))
            : refundable;

        string depositStatus;
        if (booking.DepositHeld <= 0)
            depositStatus = RentalDepositStatuses.Pending;
        else if (damage >= booking.DepositHeld && refund <= 0)
            depositStatus = RentalDepositStatuses.Forfeited;
        else if (refund >= booking.DepositHeld)
            depositStatus = RentalDepositStatuses.Refunded;
        else if (refund > 0)
            depositStatus = RentalDepositStatuses.PartiallyRefunded;
        else
            depositStatus = RentalDepositStatuses.Held;

        var ok = await rentalRepository.MarkReturnedAsync(
            request.Id, tenantId, DateTime.UtcNow, request.Payload.Odometer, request.Payload.FuelLevel,
            request.Payload.Notes, damage, request.Payload.DamageNotes, refund, depositStatus,
            cancellationToken);

        if (ok)
            await rentalRepository.UpdateBookingPaymentTotalsAsync(request.Id, tenantId, cancellationToken);

        return ok
            ? ApiResponse<bool>.SuccessResponse(true, "Vehicle returned.")
            : ApiResponse<bool>.FailResponse("Return failed.");
    }
}

public record CloseRentalBookingCommand(int Id)
    : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Close";
    public string AuditEntityName => "RentalBooking";
    public int? AuditEntityId => Id;
}

public class CloseRentalBookingCommandHandler(IRentalRepository rentalRepository, ITenantContext tenantContext)
    : IRequestHandler<CloseRentalBookingCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(CloseRentalBookingCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var booking = await rentalRepository.GetBookingByIdAsync(request.Id, tenantId, cancellationToken)
            ?? throw new NotFoundException("RentalBooking", request.Id);

        if (booking.Status != RentalBookingStatuses.Returned)
            return ApiResponse<bool>.FailResponse("Only returned rentals can be closed.");

        var ok = await rentalRepository.CloseBookingAsync(request.Id, tenantId, cancellationToken);
        return ApiResponse<bool>.SuccessResponse(ok, "Rental closed.");
    }
}

public record SignRentalAgreementCommand(int Id, SignRentalAgreementDto Payload)
    : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "SignAgreement";
    public string AuditEntityName => "RentalBooking";
    public int? AuditEntityId => Id;
}

public class SignRentalAgreementCommandValidator : AbstractValidator<SignRentalAgreementCommand>
{
    public SignRentalAgreementCommandValidator()
    {
        RuleFor(x => x.Payload.SignedBy).NotEmpty().MaximumLength(200);
    }
}

public class SignRentalAgreementCommandHandler(IRentalRepository rentalRepository, ITenantContext tenantContext)
    : IRequestHandler<SignRentalAgreementCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(SignRentalAgreementCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var booking = await rentalRepository.GetBookingByIdAsync(request.Id, tenantId, cancellationToken)
            ?? throw new NotFoundException("RentalBooking", request.Id);

        var text = string.IsNullOrWhiteSpace(request.Payload.AgreementText)
            ? booking.AgreementText ?? RentalPricing.DefaultAgreementText(
                booking.RentalNumber,
                $"{booking.VehicleName} ({booking.RegistrationNumber})",
                booking.CustomerName ?? "Customer")
            : request.Payload.AgreementText;

        var ok = await rentalRepository.SignAgreementAsync(
            request.Id, tenantId, request.Payload.SignedBy.Trim(), text, DateTime.UtcNow, cancellationToken);
        return ok
            ? ApiResponse<bool>.SuccessResponse(true, "Agreement signed.")
            : ApiResponse<bool>.FailResponse("Could not sign agreement.");
    }
}

public record RecordRentalPaymentCommand(int Id, RecordRentalPaymentDto Payload)
    : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => "RecordPayment";
    public string AuditEntityName => "RentalPayment";
    public int? AuditEntityId => Id;
}

public class RecordRentalPaymentCommandValidator : AbstractValidator<RecordRentalPaymentCommand>
{
    public RecordRentalPaymentCommandValidator()
    {
        RuleFor(x => x.Payload.Amount).GreaterThan(0);
        RuleFor(x => x.Payload.PaymentMethod).NotEmpty().MaximumLength(50);
    }
}

public class RecordRentalPaymentCommandHandler(
    IRentalRepository rentalRepository,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<RecordRentalPaymentCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(RecordRentalPaymentCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var booking = await rentalRepository.GetBookingByIdAsync(request.Id, tenantId, cancellationToken)
            ?? throw new NotFoundException("RentalBooking", request.Id);

        if (booking.Status == RentalBookingStatuses.Cancelled)
            return ApiResponse<int>.FailResponse("Cannot record payment on a cancelled rental.");

        var paymentId = await rentalRepository.RecordPaymentAsync(
            tenantId, request.Id, request.Payload, currentUser.UserId, cancellationToken);
        await rentalRepository.UpdateBookingPaymentTotalsAsync(request.Id, tenantId, cancellationToken);
        return ApiResponse<int>.SuccessResponse(paymentId, "Payment recorded.");
    }
}
