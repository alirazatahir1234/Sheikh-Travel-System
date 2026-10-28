using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Rentals.DTOs;

namespace SheikhTravelSystem.Application.Features.Rentals.Queries;

public record GetRentalRatesQuery(
    int Page = 1,
    int PageSize = 20,
    int? VehicleId = null,
    string? RateType = null,
    bool? IsActive = null) : IRequest<ApiResponse<PagedResult<RentalRateDto>>>;

public class GetRentalRatesQueryHandler(IRentalRepository rentalRepository, ITenantContext tenantContext)
    : IRequestHandler<GetRentalRatesQuery, ApiResponse<PagedResult<RentalRateDto>>>
{
    public async Task<ApiResponse<PagedResult<RentalRateDto>>> Handle(
        GetRentalRatesQuery request, CancellationToken cancellationToken)
    {
        var result = await rentalRepository.GetRatesPagedAsync(
            tenantContext.GetRequiredTenantId(),
            request.Page, request.PageSize, request.VehicleId, request.RateType, request.IsActive,
            cancellationToken);
        return ApiResponse<PagedResult<RentalRateDto>>.SuccessResponse(result);
    }
}

public record GetRentalBookingsQuery(
    int Page = 1,
    int PageSize = 20,
    string? Status = null,
    int? VehicleId = null,
    int? CustomerId = null) : IRequest<ApiResponse<PagedResult<RentalBookingListItemDto>>>;

public class GetRentalBookingsQueryHandler(IRentalRepository rentalRepository, ITenantContext tenantContext)
    : IRequestHandler<GetRentalBookingsQuery, ApiResponse<PagedResult<RentalBookingListItemDto>>>
{
    public async Task<ApiResponse<PagedResult<RentalBookingListItemDto>>> Handle(
        GetRentalBookingsQuery request, CancellationToken cancellationToken)
    {
        var result = await rentalRepository.GetBookingsPagedAsync(
            tenantContext.GetRequiredTenantId(),
            request.Page, request.PageSize, request.Status, request.VehicleId, request.CustomerId,
            cancellationToken);
        return ApiResponse<PagedResult<RentalBookingListItemDto>>.SuccessResponse(result);
    }
}

public record GetRentalBookingByIdQuery(int Id) : IRequest<ApiResponse<RentalBookingDetailDto>>;

public class GetRentalBookingByIdQueryHandler(IRentalRepository rentalRepository, ITenantContext tenantContext)
    : IRequestHandler<GetRentalBookingByIdQuery, ApiResponse<RentalBookingDetailDto>>
{
    public async Task<ApiResponse<RentalBookingDetailDto>> Handle(
        GetRentalBookingByIdQuery request, CancellationToken cancellationToken)
    {
        var booking = await rentalRepository.GetBookingByIdAsync(
            request.Id, tenantContext.GetRequiredTenantId(), cancellationToken)
            ?? throw new NotFoundException("RentalBooking", request.Id);
        return ApiResponse<RentalBookingDetailDto>.SuccessResponse(booking);
    }
}

public record GetRentalPaymentsQuery(int RentalBookingId) : IRequest<ApiResponse<IReadOnlyList<RentalPaymentDto>>>;

public class GetRentalPaymentsQueryHandler(IRentalRepository rentalRepository, ITenantContext tenantContext)
    : IRequestHandler<GetRentalPaymentsQuery, ApiResponse<IReadOnlyList<RentalPaymentDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<RentalPaymentDto>>> Handle(
        GetRentalPaymentsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        _ = await rentalRepository.GetBookingByIdAsync(request.RentalBookingId, tenantId, cancellationToken)
            ?? throw new NotFoundException("RentalBooking", request.RentalBookingId);
        var payments = await rentalRepository.GetPaymentsAsync(request.RentalBookingId, tenantId, cancellationToken);
        return ApiResponse<IReadOnlyList<RentalPaymentDto>>.SuccessResponse(payments);
    }
}

public record CheckRentalAvailabilityQuery(
    int VehicleId,
    DateTime From,
    DateTime To) : IRequest<ApiResponse<RentalAvailabilityDto>>;

public class CheckRentalAvailabilityQueryValidator : AbstractValidator<CheckRentalAvailabilityQuery>
{
    public CheckRentalAvailabilityQueryValidator()
    {
        RuleFor(x => x.VehicleId).GreaterThan(0);
        RuleFor(x => x.To).GreaterThan(x => x.From);
    }
}

public class CheckRentalAvailabilityQueryHandler(IRentalRepository rentalRepository, ITenantContext tenantContext)
    : IRequestHandler<CheckRentalAvailabilityQuery, ApiResponse<RentalAvailabilityDto>>
{
    public async Task<ApiResponse<RentalAvailabilityDto>> Handle(
        CheckRentalAvailabilityQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        if (!await rentalRepository.VehicleExistsAsync(request.VehicleId, tenantId, cancellationToken))
            throw new NotFoundException("Vehicle", request.VehicleId);

        var result = await rentalRepository.CheckAvailabilityAsync(
            tenantId, request.VehicleId, request.From, request.To, cancellationToken);
        return ApiResponse<RentalAvailabilityDto>.SuccessResponse(result);
    }
}

public record QuoteRentalQuery(
    int VehicleId,
    string RateType,
    DateTime StartAt,
    DateTime EndAt,
    int? RateId = null,
    decimal? DiscountAmount = null) : IRequest<ApiResponse<RentalQuotationDto>>;

public class QuoteRentalQueryValidator : AbstractValidator<QuoteRentalQuery>
{
    public QuoteRentalQueryValidator()
    {
        RuleFor(x => x.VehicleId).GreaterThan(0);
        RuleFor(x => x.RateType).Must(RentalRateTypes.IsValid);
        RuleFor(x => x.EndAt).GreaterThan(x => x.StartAt);
        RuleFor(x => x.DiscountAmount).GreaterThanOrEqualTo(0).When(x => x.DiscountAmount.HasValue);
    }
}

public class QuoteRentalQueryHandler(IRentalRepository rentalRepository, ITenantContext tenantContext)
    : IRequestHandler<QuoteRentalQuery, ApiResponse<RentalQuotationDto>>
{
    public async Task<ApiResponse<RentalQuotationDto>> Handle(
        QuoteRentalQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        if (!await rentalRepository.VehicleExistsAsync(request.VehicleId, tenantId, cancellationToken))
            throw new NotFoundException("Vehicle", request.VehicleId);

        RentalRateDto? rate = null;
        if (request.RateId is int rateId)
            rate = await rentalRepository.GetRateByIdAsync(rateId, tenantId, cancellationToken)
                ?? throw new NotFoundException("RentalRate", rateId);
        else
            rate = await rentalRepository.FindBestRateAsync(
                tenantId, request.VehicleId, request.RateType, cancellationToken);

        if (rate is null)
            return ApiResponse<RentalQuotationDto>.FailResponse("No active rate found for quotation.");

        var units = RentalPricing.BillableUnits(request.RateType, request.StartAt, request.EndAt);
        var baseCharge = RentalPricing.CalculateTotal(request.RateType, rate.Amount, request.StartAt, request.EndAt);
        var discount = Math.Max(0, request.DiscountAmount ?? 0);
        var estimated = Math.Max(0, baseCharge - discount);

        return ApiResponse<RentalQuotationDto>.SuccessResponse(new RentalQuotationDto(
            request.VehicleId,
            request.RateType,
            rate.Amount,
            units,
            baseCharge,
            discount,
            rate.DepositAmount,
            estimated,
            rate.Currency));
    }
}
