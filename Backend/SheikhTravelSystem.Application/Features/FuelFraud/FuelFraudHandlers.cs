using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;

namespace SheikhTravelSystem.Application.Features.FuelFraud;

public record GetFuelFraudEventsQuery(string? Status = null, int Limit = 50)
    : IRequest<ApiResponse<List<FuelFraudEventListItemDto>>>;

public record GetFuelFraudEventsPlatformQuery(int? TenantId = null, string? Status = null, int Limit = 100)
    : IRequest<ApiResponse<List<FuelFraudEventListItemDto>>>;

public record ResolveFuelFraudEventCommand(int Id)
    : IRequest<ApiResponse<bool>>;

public record DriverSubmitFuelPumpOrderCommand(DriverSubmitFuelPumpOrderRequest Body)
    : IRequest<ApiResponse<DriverSubmitFuelPumpOrderResultDto>>;

public class DriverSubmitFuelPumpOrderValidator : AbstractValidator<DriverSubmitFuelPumpOrderCommand>
{
    public DriverSubmitFuelPumpOrderValidator()
    {
        RuleFor(x => x.Body.VehicleId).GreaterThan(0);
        RuleFor(x => x.Body.OrderedLiters).InclusiveBetween(0.5m, 500m);
        RuleFor(x => x.Body.StationName).MaximumLength(120);
    }
}

public class GetFuelFraudEventsQueryHandler(
    IFuelFraudRepository repository,
    ITenantContext tenantContext)
    : IRequestHandler<GetFuelFraudEventsQuery, ApiResponse<List<FuelFraudEventListItemDto>>>
{
    public async Task<ApiResponse<List<FuelFraudEventListItemDto>>> Handle(
        GetFuelFraudEventsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var rows = await repository.ListEventsAsync(tenantId, request.Status, request.Limit, cancellationToken);
        return ApiResponse<List<FuelFraudEventListItemDto>>.SuccessResponse(rows.ToList());
    }
}

public class GetFuelFraudEventsPlatformQueryHandler(
    IFuelFraudRepository repository,
    IPlatformScope platformScope)
    : IRequestHandler<GetFuelFraudEventsPlatformQuery, ApiResponse<List<FuelFraudEventListItemDto>>>
{
    public async Task<ApiResponse<List<FuelFraudEventListItemDto>>> Handle(
        GetFuelFraudEventsPlatformQuery request, CancellationToken cancellationToken)
    {
        if (!platformScope.IsSuperAdmin)
            return ApiResponse<List<FuelFraudEventListItemDto>>.FailResponse("Super admin access required.");

        var rows = await repository.ListEventsPlatformAsync(
            request.TenantId, request.Status, request.Limit, cancellationToken);
        return ApiResponse<List<FuelFraudEventListItemDto>>.SuccessResponse(rows.ToList());
    }
}

public class ResolveFuelFraudEventCommandHandler(
    IFuelFraudRepository repository,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<ResolveFuelFraudEventCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(
        ResolveFuelFraudEventCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var ok = await repository.ResolveEventAsync(
            tenantId, request.Id, currentUser.UserId?.ToString() ?? "user", cancellationToken);
        return ok
            ? ApiResponse<bool>.SuccessResponse(true, "Event resolved.")
            : ApiResponse<bool>.FailResponse("Event not found.");
    }
}

public class DriverSubmitFuelPumpOrderCommandHandler(
    IFuelFraudRepository repository,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<DriverSubmitFuelPumpOrderCommand, ApiResponse<DriverSubmitFuelPumpOrderResultDto>>
{
    public async Task<ApiResponse<DriverSubmitFuelPumpOrderResultDto>> Handle(
        DriverSubmitFuelPumpOrderCommand request, CancellationToken cancellationToken)
    {
        var driverId = currentUser.DriverId;
        if (!driverId.HasValue)
            return ApiResponse<DriverSubmitFuelPumpOrderResultDto>.FailResponse("Driver identity required.");

        var tenantId = tenantContext.GetRequiredTenantId();
        var ctx = await repository.GetVehicleFuelContextAsync(tenantId, request.Body.VehicleId, cancellationToken);
        if (ctx is null)
            return ApiResponse<DriverSubmitFuelPumpOrderResultDto>.FailResponse("Vehicle not found.");

        var id = await repository.InsertPumpOrderAsync(new FuelPumpOrderInsertRow(
            tenantId,
            request.Body.VehicleId,
            driverId.Value,
            request.Body.OrderedLiters,
            ctx.Value.FuelLevel,
            ctx.Value.TankCapacity,
            request.Body.StationName,
            request.Body.Latitude,
            request.Body.Longitude), cancellationToken);

        var msg = ctx.Value.FuelLevel.HasValue
            ? "Pump order logged. OBD fuel level will be checked after you leave the station."
            : "Pump order logged. Waiting for OBD fuel level from your tracker — verification may be delayed.";

        return ApiResponse<DriverSubmitFuelPumpOrderResultDto>.SuccessResponse(
            new DriverSubmitFuelPumpOrderResultDto(
                id,
                FuelFraudVerification.StatusPending,
                ctx.Value.FuelLevel,
                msg));
    }
}
