using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.CustomerPortal.Commands;
using SheikhTravelSystem.Application.Features.CustomerPortal.DTOs;

namespace SheikhTravelSystem.Application.Features.CustomerPortal.Queries;

public record GetPortalSavedAddressesQuery(string Phone) : IRequest<ApiResponse<IReadOnlyList<PortalSavedAddressDto>>>;

public class GetPortalSavedAddressesQueryHandler(ICustomerPortalRepository portalRepository, ITenantContext tenantContext)
    : IRequestHandler<GetPortalSavedAddressesQuery, ApiResponse<IReadOnlyList<PortalSavedAddressDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<PortalSavedAddressDto>>> Handle(
        GetPortalSavedAddressesQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var customerId = await portalRepository.ResolveCustomerIdByPhoneAsync(tenantId, request.Phone, cancellationToken);
        if (customerId is null or <= 0)
            return ApiResponse<IReadOnlyList<PortalSavedAddressDto>>.SuccessResponse([], "No saved addresses.");

        var rows = await portalRepository.GetSavedAddressesAsync(tenantId, customerId.Value, cancellationToken);
        return ApiResponse<IReadOnlyList<PortalSavedAddressDto>>.SuccessResponse(rows.ToList());
    }
}

public record GetPortalCustomerNotificationsQuery(string Phone)
    : IRequest<ApiResponse<IReadOnlyList<PortalCustomerNotificationDto>>>;

public class GetPortalCustomerNotificationsQueryHandler(ICustomerPortalRepository portalRepository, ITenantContext tenantContext)
    : IRequestHandler<GetPortalCustomerNotificationsQuery, ApiResponse<IReadOnlyList<PortalCustomerNotificationDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<PortalCustomerNotificationDto>>> Handle(
        GetPortalCustomerNotificationsQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var customerId = await portalRepository.ResolveCustomerIdByPhoneAsync(tenantId, request.Phone, cancellationToken);
        if (customerId is null or <= 0)
            return ApiResponse<IReadOnlyList<PortalCustomerNotificationDto>>.SuccessResponse([]);

        var rows = await portalRepository.GetCustomerNotificationsAsync(tenantId, customerId.Value, cancellationToken);
        return ApiResponse<IReadOnlyList<PortalCustomerNotificationDto>>.SuccessResponse(rows.ToList());
    }
}

public record GetPortalVehicleSeatsQuery(int VehicleId, DateTime PickupTime)
    : IRequest<ApiResponse<IReadOnlyList<PortalSeatLayoutDto>>>;

public class GetPortalVehicleSeatsQueryHandler(ICustomerPortalRepository portalRepository, ITenantContext tenantContext)
    : IRequestHandler<GetPortalVehicleSeatsQuery, ApiResponse<IReadOnlyList<PortalSeatLayoutDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<PortalSeatLayoutDto>>> Handle(
        GetPortalVehicleSeatsQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var layoutList = (await portalRepository.GetVehicleSeatLayoutsAsync(tenantId, request.VehicleId, cancellationToken)).ToList();
        if (layoutList.Count == 0)
        {
            var cap = await portalRepository.GetVehicleSeatingCapacityAsync(tenantId, request.VehicleId, cancellationToken) ?? 6;
            layoutList = Enumerable.Range(1, Math.Min(cap, 12))
                .Select(i => ($"{i}", (i - 1) / 2, (i - 1) % 2))
                .ToList();
        }

        var booked = (await portalRepository.GetBookedSeatsNearPickupAsync(
            tenantId, request.VehicleId, request.PickupTime, cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var dtos = layoutList.Select(l => new PortalSeatLayoutDto(l.SeatLabel, l.RowIndex, l.ColIndex, booked.Contains(l.SeatLabel))).ToList();
        return ApiResponse<IReadOnlyList<PortalSeatLayoutDto>>.SuccessResponse(dtos);
    }
}

public record GetPortalLoyaltyQuery(string Phone) : IRequest<ApiResponse<PortalLoyaltyDto>>;

public class GetPortalLoyaltyQueryHandler(ICustomerPortalRepository portalRepository, ITenantContext tenantContext)
    : IRequestHandler<GetPortalLoyaltyQuery, ApiResponse<PortalLoyaltyDto>>
{
    public async Task<ApiResponse<PortalLoyaltyDto>> Handle(GetPortalLoyaltyQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var customerId = await portalRepository.ResolveCustomerIdByPhoneAsync(tenantId, request.Phone, cancellationToken);
        if (customerId is null)
            return ApiResponse<PortalLoyaltyDto>.SuccessResponse(new PortalLoyaltyDto(0, "Bronze"));

        await portalRepository.EnsureLoyaltyRowAsync(customerId.Value, cancellationToken);
        var row = await portalRepository.GetLoyaltyAsync(tenantId, customerId.Value, cancellationToken);
        return ApiResponse<PortalLoyaltyDto>.SuccessResponse(new PortalLoyaltyDto(row.Points, row.Tier));
    }
}

public record GetPortalWalletQuery(string Phone) : IRequest<ApiResponse<PortalWalletDto>>;

public class GetPortalWalletQueryHandler(ICustomerPortalRepository portalRepository, ITenantContext tenantContext)
    : IRequestHandler<GetPortalWalletQuery, ApiResponse<PortalWalletDto>>
{
    public async Task<ApiResponse<PortalWalletDto>> Handle(GetPortalWalletQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var customerId = await portalRepository.ResolveCustomerIdByPhoneAsync(tenantId, request.Phone, cancellationToken);
        if (customerId is null)
            return ApiResponse<PortalWalletDto>.SuccessResponse(new PortalWalletDto(0));

        await portalRepository.EnsureWalletRowAsync(customerId.Value, cancellationToken);
        var balance = await portalRepository.GetWalletBalanceAsync(tenantId, customerId.Value, cancellationToken);
        return ApiResponse<PortalWalletDto>.SuccessResponse(new PortalWalletDto(balance));
    }
}

public record GetPortalFavoriteRoutesQuery(string Phone) : IRequest<ApiResponse<IReadOnlyList<PortalFavoriteRouteDto>>>;

public record PortalFavoriteRouteDto(int Id, int RouteId, string RouteName, string? Label);

public class GetPortalFavoriteRoutesQueryHandler(ICustomerPortalRepository portalRepository, ITenantContext tenantContext)
    : IRequestHandler<GetPortalFavoriteRoutesQuery, ApiResponse<IReadOnlyList<PortalFavoriteRouteDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<PortalFavoriteRouteDto>>> Handle(
        GetPortalFavoriteRoutesQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var customerId = await portalRepository.ResolveCustomerIdByPhoneAsync(tenantId, request.Phone, cancellationToken);
        if (customerId is null or <= 0)
            return ApiResponse<IReadOnlyList<PortalFavoriteRouteDto>>.SuccessResponse([]);

        var rows = await portalRepository.GetFavoriteRoutesAsync(tenantId, customerId.Value, cancellationToken);
        return ApiResponse<IReadOnlyList<PortalFavoriteRouteDto>>.SuccessResponse(rows.ToList());
    }
}

public record AddPortalFavoriteRouteCommand(string Phone, int RouteId, string? Label) : IRequest<ApiResponse<int>>;

public class AddPortalFavoriteRouteCommandHandler(ICustomerPortalRepository portalRepository, ITenantContext tenantContext)
    : IRequestHandler<AddPortalFavoriteRouteCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(AddPortalFavoriteRouteCommand request, CancellationToken cancellationToken)
    {
        var customerId = await portalRepository.ResolveCustomerIdByPhoneAsync(tenantContext.GetRequiredTenantId(), request.Phone, cancellationToken);
        if (customerId is null or <= 0)
            return ApiResponse<int>.FailResponse("Sign in and complete a booking first.");

        var id = await portalRepository.AddFavoriteRouteAsync(
            customerId.Value, request.RouteId, request.Label, cancellationToken);
        return ApiResponse<int>.SuccessResponse(id, "Favorite route saved.");
    }
}
