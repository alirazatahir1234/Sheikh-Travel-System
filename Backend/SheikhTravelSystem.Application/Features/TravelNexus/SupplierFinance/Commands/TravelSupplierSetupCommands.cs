using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.SupplierFinance.DTOs;

namespace SheikhTravelSystem.Application.Features.TravelNexus.SupplierFinance.Commands;

// ── Exchange rates ──────────────────────────────────────────────────────

public record GetTravelExchangeRatesQuery : IRequest<ApiResponse<IReadOnlyList<TravelExchangeRateDto>>>;

public class GetTravelExchangeRatesQueryHandler(ITravelSupplierSetupRepository repository, ITenantContext tenantContext)
    : IRequestHandler<GetTravelExchangeRatesQuery, ApiResponse<IReadOnlyList<TravelExchangeRateDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<TravelExchangeRateDto>>> Handle(GetTravelExchangeRatesQuery request, CancellationToken cancellationToken)
        => ApiResponse<IReadOnlyList<TravelExchangeRateDto>>.SuccessResponse(await repository.GetExchangeRatesAsync(tenantContext.GetRequiredTenantId(), cancellationToken));
}

public record SaveTravelExchangeRateCommand(int? Id, SaveTravelExchangeRateDto Rate) : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => Id.HasValue ? "UpdateExchangeRate" : "AddExchangeRate";
    public string AuditEntityName => "TravelExchangeRate";
    public int? AuditEntityId => Id;
}

public class SaveTravelExchangeRateCommandValidator : AbstractValidator<SaveTravelExchangeRateCommand>
{
    public SaveTravelExchangeRateCommandValidator()
    {
        RuleFor(x => x.Rate.CurrencyCode).Must(c => TravelValues.IsCurrencyCode(c?.ToUpperInvariant())).WithMessage("Currency must be a 3-letter ISO code.");
        RuleFor(x => x.Rate.RateToReporting).GreaterThan(0).LessThan(10_000_000);
    }
}

public class SaveTravelExchangeRateCommandHandler(ITravelSupplierSetupRepository repository, ITravelConfigRepository config, ITenantContext tenantContext,
    ICurrentUserService currentUser) : IRequestHandler<SaveTravelExchangeRateCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(SaveTravelExchangeRateCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var dto = request.Rate with { CurrencyCode = request.Rate.CurrencyCode.ToUpperInvariant(), EffectiveFrom = request.Rate.EffectiveFrom.Date };
        var settings = await config.GetSettingsAsync(tenantId, cancellationToken);
        var reporting = settings.ReportingCurrencyCode ?? settings.DefaultCurrencyCode;
        if (reporting is null) return ApiResponse<int>.FailResponse("Set the reporting currency in settings first.");
        if (dto.CurrencyCode == reporting) return ApiResponse<int>.FailResponse($"{reporting} is the reporting currency — it always converts at 1.");
        // Repository: id on success, 0 when the rate to update is not this tenant's, -1 when the currency already has a rate on that date.
        var id = await repository.SaveExchangeRateAsync(tenantId, request.Id, dto, currentUser.UserId, cancellationToken);
        if (id == 0) throw new NotFoundException("TravelExchangeRate", request.Id ?? 0);
        if (id < 0) return ApiResponse<int>.FailResponse($"A {dto.CurrencyCode} rate already starts on {dto.EffectiveFrom:yyyy-MM-dd}.");
        return ApiResponse<int>.SuccessResponse(id, "Exchange rate saved.");
    }
}

public record DeleteTravelExchangeRateCommand(int Id) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "DeleteExchangeRate";
    public string AuditEntityName => "TravelExchangeRate";
    public int? AuditEntityId => Id;
}

public class DeleteTravelExchangeRateCommandHandler(ITravelSupplierSetupRepository repository, ITenantContext tenantContext)
    : IRequestHandler<DeleteTravelExchangeRateCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(DeleteTravelExchangeRateCommand request, CancellationToken cancellationToken)
    {
        if (!await repository.DeleteExchangeRateAsync(tenantContext.GetRequiredTenantId(), request.Id, cancellationToken))
            throw new NotFoundException("TravelExchangeRate", request.Id);
        return ApiResponse<bool>.SuccessResponse(true, "Exchange rate deleted.");
    }
}

// ── Rate cards ──────────────────────────────────────────────────────────

internal static class SupplierCheck
{
    public static async Task EnsureAsync(ITravelSupplierRepository suppliers, int tenantId, int supplierId, CancellationToken ct)
        => _ = await suppliers.GetByIdAsync(supplierId, tenantId, ct) ?? throw new NotFoundException("TravelSupplier", supplierId);
}

public record GetTravelSupplierRatesQuery(int SupplierId) : IRequest<ApiResponse<IReadOnlyList<TravelSupplierRateDto>>>;

public class GetTravelSupplierRatesQueryHandler(ITravelSupplierSetupRepository repository, ITravelSupplierRepository suppliers, ITenantContext tenantContext)
    : IRequestHandler<GetTravelSupplierRatesQuery, ApiResponse<IReadOnlyList<TravelSupplierRateDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<TravelSupplierRateDto>>> Handle(GetTravelSupplierRatesQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        await SupplierCheck.EnsureAsync(suppliers, tenantId, request.SupplierId, cancellationToken);
        return ApiResponse<IReadOnlyList<TravelSupplierRateDto>>.SuccessResponse(await repository.GetRatesAsync(tenantId, request.SupplierId, cancellationToken));
    }
}

public record SaveTravelSupplierRateCommand(int SupplierId, int? Id, SaveTravelSupplierRateDto Rate) : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => Id.HasValue ? "UpdateSupplierRate" : "AddSupplierRate";
    public string AuditEntityName => "TravelSupplier";
    public int? AuditEntityId => SupplierId;
}

public class SaveTravelSupplierRateCommandValidator : AbstractValidator<SaveTravelSupplierRateCommand>
{
    public SaveTravelSupplierRateCommandValidator()
    {
        RuleFor(x => x.Rate.RateType).Must(t => TravelValues.IsOneOf(t, TravelValues.SupplierRateTypes)).WithMessage("Unknown rate type.");
        RuleFor(x => x.Rate.HotelId).NotNull().When(x => x.Rate.RateType == "HotelRoomNight").WithMessage("A room-night rate needs a hotel.");
        RuleFor(x => x.Rate.RoomType).Must(r => TravelValues.IsOneOf(r, TravelValues.RoomTypes)).When(x => x.Rate.RateType == "HotelRoomNight")
            .WithMessage("A room-night rate needs a room type.");
        RuleFor(x => x.Rate.ValidTo).GreaterThanOrEqualTo(x => x.Rate.ValidFrom).WithMessage("Valid to must be on or after valid from.");
        RuleFor(x => x.Rate.Amount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Rate.CurrencyCode).Must(c => TravelValues.IsCurrencyCode(c?.ToUpperInvariant())).WithMessage("Currency must be a 3-letter ISO code.");
        RuleFor(x => x.Rate.Description).MaximumLength(200);
    }
}

public class SaveTravelSupplierRateCommandHandler(ITravelSupplierSetupRepository repository, ITravelSupplierRepository suppliers, ITravelBookingRepository bookings,
    ITenantContext tenantContext, ICurrentUserService currentUser) : IRequestHandler<SaveTravelSupplierRateCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(SaveTravelSupplierRateCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        await SupplierCheck.EnsureAsync(suppliers, tenantId, request.SupplierId, cancellationToken);
        var dto = request.Rate with
        {
            CurrencyCode = request.Rate.CurrencyCode.ToUpperInvariant(), ValidFrom = request.Rate.ValidFrom.Date, ValidTo = request.Rate.ValidTo.Date,
            HotelId = request.Rate.RateType == "HotelRoomNight" ? request.Rate.HotelId : null,
            RoomType = request.Rate.RateType == "HotelRoomNight" ? request.Rate.RoomType : null,
            Description = string.IsNullOrWhiteSpace(request.Rate.Description) ? null : request.Rate.Description.Trim()
        };
        if (dto.HotelId is int hotelId && !await bookings.HotelExistsAsync(tenantId, hotelId, cancellationToken)) return ApiResponse<int>.FailResponse("Hotel not found.");
        var id = await repository.SaveRateAsync(tenantId, request.SupplierId, request.Id, dto, currentUser.UserId, cancellationToken);
        if (id == 0) throw new NotFoundException("TravelSupplierRate", request.Id ?? 0);
        return ApiResponse<int>.SuccessResponse(id, "Rate saved.");
    }
}

public record DeleteTravelSupplierRateCommand(int SupplierId, int Id) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "DeleteSupplierRate";
    public string AuditEntityName => "TravelSupplier";
    public int? AuditEntityId => SupplierId;
}

public class DeleteTravelSupplierRateCommandHandler(ITravelSupplierSetupRepository repository, ITenantContext tenantContext)
    : IRequestHandler<DeleteTravelSupplierRateCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(DeleteTravelSupplierRateCommand request, CancellationToken cancellationToken)
    {
        if (!await repository.DeleteRateAsync(tenantContext.GetRequiredTenantId(), request.SupplierId, request.Id, cancellationToken))
            throw new NotFoundException("TravelSupplierRate", request.Id);
        return ApiResponse<bool>.SuccessResponse(true, "Rate deleted.");
    }
}

/// <summary>Hotel cost quote from the supplier's rate cards (nights without a rate are reported).</summary>
public record QuoteTravelSupplierRateQuery(TravelRateQuoteRequestDto Request) : IRequest<ApiResponse<TravelRateQuoteDto>>;

public class QuoteTravelSupplierRateQueryValidator : AbstractValidator<QuoteTravelSupplierRateQuery>
{
    public QuoteTravelSupplierRateQueryValidator()
    {
        RuleFor(x => x.Request.CheckOut).GreaterThan(x => x.Request.CheckIn).WithMessage("Check-out must be after check-in.");
        RuleFor(x => x.Request).Must(r => (r.CheckOut.Date - r.CheckIn.Date).TotalDays <= 90).WithMessage("At most 90 nights.");
        RuleFor(x => x.Request.Rooms).NotEmpty();
    }
}

public class QuoteTravelSupplierRateQueryHandler(ITravelSupplierSetupRepository repository, ITravelSupplierRepository suppliers, ITenantContext tenantContext)
    : IRequestHandler<QuoteTravelSupplierRateQuery, ApiResponse<TravelRateQuoteDto>>
{
    public async Task<ApiResponse<TravelRateQuoteDto>> Handle(QuoteTravelSupplierRateQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var r = request.Request;
        await SupplierCheck.EnsureAsync(suppliers, tenantId, r.SupplierId, cancellationToken);
        var rates = await repository.GetRatesAsync(tenantId, r.SupplierId, cancellationToken);
        return ApiResponse<TravelRateQuoteDto>.SuccessResponse(TravelRateQuote.Quote(rates, r.HotelId, r.CheckIn, r.CheckOut, r.Rooms));
    }
}

// ── Supplier commission rules ───────────────────────────────────────────

public record GetTravelSupplierCommissionRulesQuery(int SupplierId) : IRequest<ApiResponse<IReadOnlyList<TravelSupplierCommissionRuleDto>>>;

public class GetTravelSupplierCommissionRulesQueryHandler(ITravelSupplierSetupRepository repository, ITravelSupplierRepository suppliers, ITenantContext tenantContext)
    : IRequestHandler<GetTravelSupplierCommissionRulesQuery, ApiResponse<IReadOnlyList<TravelSupplierCommissionRuleDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<TravelSupplierCommissionRuleDto>>> Handle(GetTravelSupplierCommissionRulesQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        await SupplierCheck.EnsureAsync(suppliers, tenantId, request.SupplierId, cancellationToken);
        return ApiResponse<IReadOnlyList<TravelSupplierCommissionRuleDto>>.SuccessResponse(
            await repository.GetCommissionRulesAsync(tenantId, request.SupplierId, cancellationToken));
    }
}

public record SaveTravelSupplierCommissionRuleCommand(int SupplierId, int? Id, SaveTravelSupplierCommissionRuleDto Rule) : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => Id.HasValue ? "UpdateSupplierCommissionRule" : "AddSupplierCommissionRule";
    public string AuditEntityName => "TravelSupplier";
    public int? AuditEntityId => SupplierId;
}

public class SaveTravelSupplierCommissionRuleCommandValidator : AbstractValidator<SaveTravelSupplierCommissionRuleCommand>
{
    public SaveTravelSupplierCommissionRuleCommandValidator()
    {
        RuleFor(x => x.Rule.ServiceType).Must(t => TravelValues.IsOneOf(t, TravelValues.SupplierCommissionServiceTypes)).WithMessage("Unknown service type.");
        RuleFor(x => x.Rule.Basis).Must(b => TravelValues.IsOneOf(b, TravelValues.SupplierCommissionBases)).WithMessage("Unknown basis.");
        RuleFor(x => x.Rule.Rate).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Rule.Rate).LessThanOrEqualTo(100).When(x => x.Rule.Basis == "PercentOfCost").WithMessage("A percentage is at most 100.");
        RuleFor(x => x.Rule.CurrencyCode).Must(c => TravelValues.IsCurrencyCode(c?.ToUpperInvariant())).When(x => x.Rule.Basis == "FixedPerUnit")
            .WithMessage("A fixed commission needs its currency.");
        RuleFor(x => x.Rule.ValidTo).GreaterThanOrEqualTo(x => x.Rule.ValidFrom).When(x => x.Rule.ValidTo.HasValue);
    }
}

public class SaveTravelSupplierCommissionRuleCommandHandler(ITravelSupplierSetupRepository repository, ITravelSupplierRepository suppliers,
    ITenantContext tenantContext, ICurrentUserService currentUser) : IRequestHandler<SaveTravelSupplierCommissionRuleCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(SaveTravelSupplierCommissionRuleCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        await SupplierCheck.EnsureAsync(suppliers, tenantId, request.SupplierId, cancellationToken);
        var dto = request.Rule with
        {
            CurrencyCode = request.Rule.Basis == "FixedPerUnit" ? request.Rule.CurrencyCode!.ToUpperInvariant() : null,
            ValidFrom = request.Rule.ValidFrom.Date, ValidTo = request.Rule.ValidTo?.Date
        };
        var id = await repository.SaveCommissionRuleAsync(tenantId, request.SupplierId, request.Id, dto, currentUser.UserId, cancellationToken);
        if (id == 0) throw new NotFoundException("TravelSupplierCommissionRule", request.Id ?? 0);
        return ApiResponse<int>.SuccessResponse(id, "Commission rule saved.");
    }
}

public record DeleteTravelSupplierCommissionRuleCommand(int SupplierId, int Id) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "DeleteSupplierCommissionRule";
    public string AuditEntityName => "TravelSupplier";
    public int? AuditEntityId => SupplierId;
}

public class DeleteTravelSupplierCommissionRuleCommandHandler(ITravelSupplierSetupRepository repository, ITenantContext tenantContext)
    : IRequestHandler<DeleteTravelSupplierCommissionRuleCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(DeleteTravelSupplierCommissionRuleCommand request, CancellationToken cancellationToken)
    {
        if (!await repository.DeleteCommissionRuleAsync(tenantContext.GetRequiredTenantId(), request.SupplierId, request.Id, cancellationToken))
            throw new NotFoundException("TravelSupplierCommissionRule", request.Id);
        return ApiResponse<bool>.SuccessResponse(true, "Commission rule deleted.");
    }
}
