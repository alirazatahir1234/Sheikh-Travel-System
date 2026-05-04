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

public class GetPaymentsQueryHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<GetPaymentsQuery, ApiResponse<PagedResult<PaymentDto>>>
{
    public async Task<ApiResponse<PagedResult<PaymentDto>>> Handle(GetPaymentsQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var offset = (request.Page - 1) * request.PageSize;

        var where = "WHERE p.IsDeleted = 0";
        if (request.BookingId.HasValue)      where += " AND p.BookingId = @BookingId";
        if (request.Status.HasValue)         where += " AND p.Status = @Status";
        if (request.DateFrom.HasValue)       where += " AND p.PaymentDate >= @DateFrom";
        if (request.DateTo.HasValue)         where += " AND p.PaymentDate < @DateTo";

        var sql = $@"SELECT p.Id, p.BookingId, p.Amount, p.PaymentMethod, p.Status, p.PaymentDate,
                     p.TransactionReference, p.Notes, p.CreatedAt
                     FROM Payments p {where}
                     ORDER BY p.CreatedAt DESC
                     OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

        var countSql = $"SELECT COUNT(*) FROM Payments p {where}";

        var parameters = new
        {
            request.BookingId,
            Status   = request.Status.HasValue   ? (int?)request.Status.Value : null,
            request.DateFrom,
            DateTo   = request.DateTo.HasValue   ? (DateTime?)request.DateTo.Value.Date.AddDays(1) : null,
            Offset   = offset,
            request.PageSize
        };

        var payments = await connection.QueryAsync<PaymentDto>(
            new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        var totalCount = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(countSql, parameters, cancellationToken: cancellationToken));

        return ApiResponse<PagedResult<PaymentDto>>.SuccessResponse(new PagedResult<PaymentDto>
        {
            Items      = payments.ToList(),
            TotalCount = totalCount,
            Page       = request.Page,
            PageSize   = request.PageSize
        });
    }
}
