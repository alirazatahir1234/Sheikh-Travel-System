using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Payments.DTOs;

namespace SheikhTravelSystem.Application.Features.Payments.Queries;

public record GetPaymentsQuery(int Page = 1, int PageSize = 20) : IRequest<ApiResponse<PagedResult<PaymentDto>>>;

public class GetPaymentsQueryHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<GetPaymentsQuery, ApiResponse<PagedResult<PaymentDto>>>
{
    public async Task<ApiResponse<PagedResult<PaymentDto>>> Handle(GetPaymentsQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var offset = (request.Page - 1) * request.PageSize;

        var payments = await connection.QueryAsync<PaymentDto>(
            new CommandDefinition(
                @"SELECT Id, BookingId, Amount, PaymentMethod, Status, PaymentDate,
                  TransactionReference, Notes, CreatedAt
                  FROM Payments WHERE IsDeleted = 0
                  ORDER BY CreatedAt DESC
                  OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY",
                new { Offset = offset, request.PageSize },
                cancellationToken: cancellationToken));

        var totalCount = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                "SELECT COUNT(*) FROM Payments WHERE IsDeleted = 0",
                cancellationToken: cancellationToken));

        var result = new PagedResult<PaymentDto>
        {
            Items = payments.ToList(),
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };

        return ApiResponse<PagedResult<PaymentDto>>.SuccessResponse(result);
    }
}
