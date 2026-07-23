using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Payments.DTOs;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.Payments.Queries;

public record GetPaymentsQuery(
    int Page = 1,
    int PageSize = 20,
    int? BookingId = null,
    PaymentStatus? Status = null,
    DateTime? DateFrom = null,
    DateTime? DateTo = null
) : IRequest<ApiResponse<PagedResult<PaymentDto>>>;

public class GetPaymentsQueryHandler(
    IDbConnectionFactory dbFactory,
    ITenantContext tenantContext,
    ICurrentUserService currentUser,
    IDataScopeEngine dataScopeEngine)
    : IRequestHandler<GetPaymentsQuery, ApiResponse<PagedResult<PaymentDto>>>
{
    public async Task<ApiResponse<PagedResult<PaymentDto>>> Handle(GetPaymentsQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var offset = (request.Page - 1) * request.PageSize;
        var tenantId = tenantContext.GetRequiredTenantId();

        var clauses = new List<string> { "p.IsDeleted = 0", "p.TenantId = @TenantId" };
        if (request.BookingId.HasValue) clauses.Add("p.BookingId = @BookingId");
        if (request.Status.HasValue) clauses.Add("p.Status = @Status");
        if (request.DateFrom.HasValue) clauses.Add("p.PaymentDate >= @DateFrom");
        if (request.DateTo.HasValue) clauses.Add("p.PaymentDate < @DateTo");

        var parameters = new DynamicParameters(new
        {
            request.BookingId,
            Status = request.Status.HasValue ? (int?)request.Status.Value : null,
            request.DateFrom,
            DateTo = request.DateTo.HasValue ? (DateTime?)request.DateTo.Value.Date.AddDays(1) : null,
            Offset = offset,
            request.PageSize,
            TenantId = tenantId
        });

        if (currentUser.UserId is int userId)
        {
            var scope = await dataScopeEngine.ResolveAsync(userId, tenantId, cancellationToken);
            DataScopeSql.ApplyLinkedFleetScope(parameters, scope, clauses, "v", "d");
        }

        var where = "WHERE " + string.Join(" AND ", clauses);

        var sql = $@"SELECT p.Id, p.BookingId, p.Amount, p.PaymentMethod, p.Status, p.PaymentDate,
                     p.TransactionReference, p.Notes, p.CreatedAt
                     FROM Payments p
                     LEFT JOIN Bookings b ON b.Id = p.BookingId
                     LEFT JOIN Vehicles v ON v.Id = b.VehicleId
                     LEFT JOIN Drivers d ON d.Id = b.DriverId
                     {where}
                     ORDER BY p.CreatedAt DESC
                     OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

        var countSql = $@"SELECT COUNT(*) FROM Payments p
                     LEFT JOIN Bookings b ON b.Id = p.BookingId
                     LEFT JOIN Vehicles v ON v.Id = b.VehicleId
                     LEFT JOIN Drivers d ON d.Id = b.DriverId
                     {where}";

        var payments = await connection.QueryAsync<PaymentDto>(
            new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        var totalCount = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(countSql, parameters, cancellationToken: cancellationToken));

        return ApiResponse<PagedResult<PaymentDto>>.SuccessResponse(new PagedResult<PaymentDto>
        {
            Items = payments.ToList(),
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        });
    }
}
