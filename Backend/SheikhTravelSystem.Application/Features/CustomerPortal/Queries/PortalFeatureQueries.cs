using Dapper;
using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.CustomerPortal.Commands;
using SheikhTravelSystem.Application.Features.CustomerPortal.DTOs;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.CustomerPortal.Queries;

public record GetPortalSavedAddressesQuery(string Phone) : IRequest<ApiResponse<IReadOnlyList<PortalSavedAddressDto>>>;

public class GetPortalSavedAddressesQueryHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<GetPortalSavedAddressesQuery, ApiResponse<IReadOnlyList<PortalSavedAddressDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<PortalSavedAddressDto>>> Handle(
        GetPortalSavedAddressesQuery request,
        CancellationToken cancellationToken)
    {
        var customerId = await PortalCustomerWriter.ResolveCustomerIdByPhoneAsync(
            dbFactory, request.Phone, cancellationToken);
        if (customerId is null or <= 0)
            return ApiResponse<IReadOnlyList<PortalSavedAddressDto>>.SuccessResponse([], "No saved addresses.");

        using var connection = dbFactory.CreateConnection();
        var rows = await connection.QueryAsync<PortalSavedAddressDto>(
            new CommandDefinition(
                @"SELECT Id, Label, AddressLine, Latitude, Longitude
                  FROM CustomerSavedAddresses WHERE CustomerId = @Id AND IsDeleted = 0 ORDER BY Label",
                new { Id = customerId },
                cancellationToken: cancellationToken));

        return ApiResponse<IReadOnlyList<PortalSavedAddressDto>>.SuccessResponse(rows.ToList());
    }
}

public record GetPortalCustomerNotificationsQuery(string Phone)
    : IRequest<ApiResponse<IReadOnlyList<PortalCustomerNotificationDto>>>;

public class GetPortalCustomerNotificationsQueryHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<GetPortalCustomerNotificationsQuery, ApiResponse<IReadOnlyList<PortalCustomerNotificationDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<PortalCustomerNotificationDto>>> Handle(
        GetPortalCustomerNotificationsQuery request,
        CancellationToken cancellationToken)
    {
        var customerId = await PortalCustomerWriter.ResolveCustomerIdByPhoneAsync(
            dbFactory, request.Phone, cancellationToken);
        if (customerId is null or <= 0)
            return ApiResponse<IReadOnlyList<PortalCustomerNotificationDto>>.SuccessResponse([]);

        using var connection = dbFactory.CreateConnection();
        var rows = await connection.QueryAsync<PortalCustomerNotificationDto>(
            new CommandDefinition(
                @"SELECT Id, Title, Message, NotificationType, BookingId, IsRead, CreatedAt
                  FROM CustomerNotifications WHERE CustomerId = @Id AND IsDeleted = 0
                  ORDER BY CreatedAt DESC",
                new { Id = customerId },
                cancellationToken: cancellationToken));

        return ApiResponse<IReadOnlyList<PortalCustomerNotificationDto>>.SuccessResponse(rows.ToList());
    }
}

public record GetPortalVehicleSeatsQuery(int VehicleId, DateTime PickupTime)
    : IRequest<ApiResponse<IReadOnlyList<PortalSeatLayoutDto>>>;

public class GetPortalVehicleSeatsQueryHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<GetPortalVehicleSeatsQuery, ApiResponse<IReadOnlyList<PortalSeatLayoutDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<PortalSeatLayoutDto>>> Handle(
        GetPortalVehicleSeatsQuery request,
        CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var layouts = await connection.QueryAsync<(string SeatLabel, int RowIndex, int ColIndex)>(
            new CommandDefinition(
                @"SELECT SeatLabel, RowIndex, ColIndex FROM VehicleSeatLayouts
                  WHERE VehicleId = @VehicleId AND IsActive = 1 ORDER BY RowIndex, ColIndex",
                new { request.VehicleId },
                cancellationToken: cancellationToken));

        var layoutList = layouts.ToList();
        if (layoutList.Count == 0)
        {
            var cap = await connection.ExecuteScalarAsync<int?>(
                new CommandDefinition(
                    "SELECT SeatingCapacity FROM Vehicles WHERE Id = @Id AND IsDeleted = 0",
                    new { Id = request.VehicleId },
                    cancellationToken: cancellationToken)) ?? 6;

            layoutList = Enumerable.Range(1, Math.Min(cap, 12))
                .Select(i => ($"{i}", (i - 1) / 2, (i - 1) % 2))
                .ToList();
        }

        var booked = (await connection.QueryAsync<string>(
            new CommandDefinition(
                @"SELECT bs.SeatLabel FROM BookingSeats bs
                  INNER JOIN Bookings b ON b.Id = bs.BookingId AND b.IsDeleted = 0
                  WHERE b.VehicleId = @VehicleId AND b.Status IN (@P, @C, @S)
                    AND ABS(DATEDIFF(MINUTE, b.PickupTime, @PickupTime)) < 180",
                new
                {
                    request.VehicleId,
                    request.PickupTime,
                    P = (int)BookingStatus.Pending,
                    C = (int)BookingStatus.Confirmed,
                    S = (int)BookingStatus.Started
                },
                cancellationToken: cancellationToken))).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var dtos = layoutList.Select(l => new PortalSeatLayoutDto(l.SeatLabel, l.RowIndex, l.ColIndex, booked.Contains(l.SeatLabel))).ToList();
        return ApiResponse<IReadOnlyList<PortalSeatLayoutDto>>.SuccessResponse(dtos);
    }
}

public record GetPortalLoyaltyQuery(string Phone) : IRequest<ApiResponse<PortalLoyaltyDto>>;

public class GetPortalLoyaltyQueryHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<GetPortalLoyaltyQuery, ApiResponse<PortalLoyaltyDto>>
{
    public async Task<ApiResponse<PortalLoyaltyDto>> Handle(GetPortalLoyaltyQuery request, CancellationToken cancellationToken)
    {
        var customerId = await PortalCustomerWriter.ResolveCustomerIdByPhoneAsync(
            dbFactory, request.Phone, cancellationToken);
        if (customerId is null)
            return ApiResponse<PortalLoyaltyDto>.SuccessResponse(new PortalLoyaltyDto(0, "Bronze"));

        await PortalCustomerWriter.EnsureLoyaltyRowAsync(dbFactory, customerId.Value, cancellationToken);
        using var connection = dbFactory.CreateConnection();
        var row = await connection.QuerySingleAsync<(int Points, string Tier)>(
            new CommandDefinition(
                "SELECT Points, Tier FROM CustomerLoyalty WHERE CustomerId = @Id",
                new { Id = customerId },
                cancellationToken: cancellationToken));

        return ApiResponse<PortalLoyaltyDto>.SuccessResponse(new PortalLoyaltyDto(row.Points, row.Tier));
    }
}

public record GetPortalWalletQuery(string Phone) : IRequest<ApiResponse<PortalWalletDto>>;

public class GetPortalWalletQueryHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<GetPortalWalletQuery, ApiResponse<PortalWalletDto>>
{
    public async Task<ApiResponse<PortalWalletDto>> Handle(GetPortalWalletQuery request, CancellationToken cancellationToken)
    {
        var customerId = await PortalCustomerWriter.ResolveCustomerIdByPhoneAsync(
            dbFactory, request.Phone, cancellationToken);
        if (customerId is null)
            return ApiResponse<PortalWalletDto>.SuccessResponse(new PortalWalletDto(0));

        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(
            new CommandDefinition(
                @"IF NOT EXISTS (SELECT 1 FROM CustomerWallets WHERE CustomerId = @Id)
                  INSERT INTO CustomerWallets (CustomerId, Balance) VALUES (@Id, 0)",
                new { Id = customerId },
                cancellationToken: cancellationToken));

        var balance = await connection.ExecuteScalarAsync<decimal>(
            new CommandDefinition(
                "SELECT Balance FROM CustomerWallets WHERE CustomerId = @Id",
                new { Id = customerId },
                cancellationToken: cancellationToken));

        return ApiResponse<PortalWalletDto>.SuccessResponse(new PortalWalletDto(balance));
    }
}

public record GetPortalFavoriteRoutesQuery(string Phone) : IRequest<ApiResponse<IReadOnlyList<PortalFavoriteRouteDto>>>;

public record PortalFavoriteRouteDto(int Id, int RouteId, string RouteName, string? Label);

public class GetPortalFavoriteRoutesQueryHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<GetPortalFavoriteRoutesQuery, ApiResponse<IReadOnlyList<PortalFavoriteRouteDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<PortalFavoriteRouteDto>>> Handle(
        GetPortalFavoriteRoutesQuery request,
        CancellationToken cancellationToken)
    {
        var customerId = await PortalCustomerWriter.ResolveCustomerIdByPhoneAsync(
            dbFactory, request.Phone, cancellationToken);
        if (customerId is null or <= 0)
            return ApiResponse<IReadOnlyList<PortalFavoriteRouteDto>>.SuccessResponse([]);

        using var connection = dbFactory.CreateConnection();
        var rows = await connection.QueryAsync<PortalFavoriteRouteDto>(new CommandDefinition(
            @"SELECT f.Id, f.RouteId,
                     r.Source + ' -> ' + r.Destination AS RouteName, f.Label
              FROM CustomerFavoriteRoutes f
              INNER JOIN Routes r ON r.Id = f.RouteId
              WHERE f.CustomerId = @CustomerId AND f.IsDeleted = 0
              ORDER BY f.SortOrder, f.Id",
            new { CustomerId = customerId },
            cancellationToken: cancellationToken));

        return ApiResponse<IReadOnlyList<PortalFavoriteRouteDto>>.SuccessResponse(rows.ToList());
    }
}

public record AddPortalFavoriteRouteCommand(string Phone, int RouteId, string? Label) : IRequest<ApiResponse<int>>;

public class AddPortalFavoriteRouteCommandHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<AddPortalFavoriteRouteCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(AddPortalFavoriteRouteCommand request, CancellationToken cancellationToken)
    {
        var customerId = await PortalCustomerWriter.ResolveCustomerIdByPhoneAsync(
            dbFactory, request.Phone, cancellationToken);
        if (customerId is null or <= 0)
            return ApiResponse<int>.FailResponse("Sign in and complete a booking first.");

        using var connection = dbFactory.CreateConnection();
        var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            @"INSERT INTO CustomerFavoriteRoutes (CustomerId, RouteId, Label, SortOrder, CreatedAt, IsDeleted)
              VALUES (@CustomerId, @RouteId, @Label, 0, GETUTCDATE(), 0);
              SELECT CAST(SCOPE_IDENTITY() AS INT);",
            new { CustomerId = customerId, request.RouteId, Label = request.Label },
            cancellationToken: cancellationToken));

        return ApiResponse<int>.SuccessResponse(id, "Favorite route saved.");
    }
}
