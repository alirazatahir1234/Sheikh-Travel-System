using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Bookings.DTOs;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.Bookings.Queries;

public record GetBookingsQuery(
    int Page = 1,
    int PageSize = 20,
    BookingStatus? Status = null,
    string? Search = null,
    DateTime? DateFrom = null,
    DateTime? DateTo = null,
    decimal? AmountMin = null,
    decimal? AmountMax = null
) : IRequest<ApiResponse<PagedResult<BookingDto>>>;

public class GetBookingsQueryHandler(
    IDbConnectionFactory dbFactory,
    ITenantContext tenantContext,
    ICurrentUserService currentUser,
    IDataScopeEngine dataScopeEngine)
    : IRequestHandler<GetBookingsQuery, ApiResponse<PagedResult<BookingDto>>>
{
    public async Task<ApiResponse<PagedResult<BookingDto>>> Handle(GetBookingsQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var offset = (request.Page - 1) * request.PageSize;
        var tenantId = tenantContext.GetRequiredTenantId();

        var clauses = new List<string> { "b.IsDeleted = 0", "b.TenantId = @TenantId" };
        if (request.Status.HasValue)
            clauses.Add("b.Status = @Status");
        if (!string.IsNullOrWhiteSpace(request.Search))
            clauses.Add("(b.BookingNumber LIKE @SearchPattern OR c.FullName LIKE @SearchPattern OR r.Source LIKE @SearchPattern OR r.Destination LIKE @SearchPattern)");
        if (request.DateFrom.HasValue)
            clauses.Add("b.PickupTime >= @DateFrom");
        if (request.DateTo.HasValue)
            clauses.Add("b.PickupTime < @DateTo");
        if (request.AmountMin.HasValue)
            clauses.Add("b.TotalAmount >= @AmountMin");
        if (request.AmountMax.HasValue)
            clauses.Add("b.TotalAmount <= @AmountMax");

        var parameters = new DynamicParameters(new
        {
            Offset = offset,
            request.PageSize,
            TenantId = tenantId,
            Status = (int?)request.Status,
            SearchPattern = $"%{request.Search}%",
            request.DateFrom,
            DateTo = request.DateTo.HasValue ? (DateTime?)request.DateTo.Value.Date.AddDays(1) : null,
            request.AmountMin,
            request.AmountMax
        });

        if (currentUser.UserId is int userId)
        {
            var scope = await dataScopeEngine.ResolveAsync(userId, tenantId, cancellationToken);
            DataScopeSql.ApplyLinkedFleetScope(parameters, scope, clauses, "v", "d");
        }

        var whereClause = "WHERE " + string.Join(" AND ", clauses);

        var bookings = await connection.QueryAsync<BookingDto>(
            new CommandDefinition(
                $@"SELECT b.Id, b.BookingNumber, b.CustomerId, c.FullName AS CustomerName, b.RouteId,
                  r.Source + ' -> ' + r.Destination AS RouteName,
                  b.VehicleId, v.Name AS VehicleName, b.DriverId, d.FullName AS DriverName,
                  b.PickupTime, b.DropoffTime, b.PassengerCount, b.TotalAmount, b.Status, b.Notes, b.CreatedAt
                  FROM Bookings b
                  LEFT JOIN Customers c ON b.CustomerId = c.Id
                  LEFT JOIN Routes r ON b.RouteId = r.Id
                  LEFT JOIN Vehicles v ON b.VehicleId = v.Id
                  LEFT JOIN Drivers d ON b.DriverId = d.Id
                  {whereClause}
                  ORDER BY b.CreatedAt DESC
                  OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY",
                parameters,
                cancellationToken: cancellationToken));

        var totalCount = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                $@"SELECT COUNT(*) FROM Bookings b
                   LEFT JOIN Customers c ON b.CustomerId = c.Id
                   LEFT JOIN Routes r ON b.RouteId = r.Id
                   LEFT JOIN Vehicles v ON b.VehicleId = v.Id
                   LEFT JOIN Drivers d ON b.DriverId = d.Id
                   {whereClause}",
                parameters,
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

public class GetBookingByIdQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<GetBookingByIdQuery, ApiResponse<BookingDto>>
{
    public async Task<ApiResponse<BookingDto>> Handle(GetBookingByIdQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        var booking = await connection.QuerySingleOrDefaultAsync<BookingDto>(
            new CommandDefinition(
                @"SELECT b.Id, b.BookingNumber, b.CustomerId, c.FullName AS CustomerName, b.RouteId,
                  r.Source + ' -> ' + r.Destination AS RouteName,
                  b.VehicleId, v.Name AS VehicleName, b.DriverId, d.FullName AS DriverName,
                  b.PickupTime, b.DropoffTime, b.PassengerCount, b.TotalAmount, b.Status, b.Notes, b.CreatedAt
                  FROM Bookings b
                  LEFT JOIN Customers c ON b.CustomerId = c.Id
                  LEFT JOIN Routes r ON b.RouteId = r.Id
                  LEFT JOIN Vehicles v ON b.VehicleId = v.Id
                  LEFT JOIN Drivers d ON b.DriverId = d.Id
                  WHERE b.Id = @Id AND b.IsDeleted = 0 AND b.TenantId = @TenantId",
                new { request.Id, TenantId = tenantId },
                cancellationToken: cancellationToken));

        return booking is null
            ? throw new NotFoundException("Booking", request.Id)
            : ApiResponse<BookingDto>.SuccessResponse(booking);
    }
}
