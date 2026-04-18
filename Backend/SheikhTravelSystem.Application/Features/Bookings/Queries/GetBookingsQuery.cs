using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Bookings.DTOs;

namespace SheikhTravelSystem.Application.Features.Bookings.Queries;

public record GetBookingsQuery(int Page = 1, int PageSize = 20) : IRequest<ApiResponse<PagedResult<BookingDto>>>;

public class GetBookingsQueryHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<GetBookingsQuery, ApiResponse<PagedResult<BookingDto>>>
{
    public async Task<ApiResponse<PagedResult<BookingDto>>> Handle(GetBookingsQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var offset = (request.Page - 1) * request.PageSize;

        var bookings = await connection.QueryAsync<BookingDto>(
            new CommandDefinition(
                @"SELECT b.Id, b.CustomerId, c.FullName AS CustomerName, b.RouteId,
                  r.Source + ' -> ' + r.Destination AS RouteDescription,
                  b.VehicleId, v.Name AS VehicleName, b.DriverId, d.FullName AS DriverName,
                  b.PickupTime, b.DropoffTime, b.PassengerCount, b.TotalAmount, b.Status, b.Notes, b.CreatedAt
                  FROM Bookings b
                  LEFT JOIN Customers c ON b.CustomerId = c.Id
                  LEFT JOIN Routes r ON b.RouteId = r.Id
                  LEFT JOIN Vehicles v ON b.VehicleId = v.Id
                  LEFT JOIN Drivers d ON b.DriverId = d.Id
                  WHERE b.IsDeleted = 0
                  ORDER BY b.CreatedAt DESC
                  OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY",
                new { Offset = offset, request.PageSize },
                cancellationToken: cancellationToken));

        var totalCount = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                "SELECT COUNT(*) FROM Bookings WHERE IsDeleted = 0",
                cancellationToken: cancellationToken));

        var result = new PagedResult<BookingDto>
        {
            Items = bookings.ToList(),
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };

        return ApiResponse<PagedResult<BookingDto>>.SuccessResponse(result);
    }
}

public record GetBookingByIdQuery(int Id) : IRequest<ApiResponse<BookingDto>>;

public class GetBookingByIdQueryHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<GetBookingByIdQuery, ApiResponse<BookingDto>>
{
    public async Task<ApiResponse<BookingDto>> Handle(GetBookingByIdQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();

        var booking = await connection.QuerySingleOrDefaultAsync<BookingDto>(
            new CommandDefinition(
                @"SELECT b.Id, b.CustomerId, c.FullName AS CustomerName, b.RouteId,
                  r.Source + ' -> ' + r.Destination AS RouteDescription,
                  b.VehicleId, v.Name AS VehicleName, b.DriverId, d.FullName AS DriverName,
                  b.PickupTime, b.DropoffTime, b.PassengerCount, b.TotalAmount, b.Status, b.Notes, b.CreatedAt
                  FROM Bookings b
                  LEFT JOIN Customers c ON b.CustomerId = c.Id
                  LEFT JOIN Routes r ON b.RouteId = r.Id
                  LEFT JOIN Vehicles v ON b.VehicleId = v.Id
                  LEFT JOIN Drivers d ON b.DriverId = d.Id
                  WHERE b.Id = @Id AND b.IsDeleted = 0",
                new { request.Id },
                cancellationToken: cancellationToken));

        if (booking is null)
            throw new NotFoundException("Booking", request.Id);

        return ApiResponse<BookingDto>.SuccessResponse(booking);
    }
}
