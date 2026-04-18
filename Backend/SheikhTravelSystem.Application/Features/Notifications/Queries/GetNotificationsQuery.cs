using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Notifications.DTOs;

namespace SheikhTravelSystem.Application.Features.Notifications.Queries;

public record GetNotificationsQuery(int UserId, int Page = 1, int PageSize = 20)
    : IRequest<ApiResponse<PagedResult<NotificationDto>>>;

public class GetNotificationsQueryHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<GetNotificationsQuery, ApiResponse<PagedResult<NotificationDto>>>
{
    public async Task<ApiResponse<PagedResult<NotificationDto>>> Handle(GetNotificationsQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var offset = (request.Page - 1) * request.PageSize;

        var notifications = await connection.QueryAsync<NotificationDto>(
            @"SELECT Id, UserId, Title, Message, Type, IsRead, ReferenceId, CreatedAt
              FROM Notifications
              WHERE (UserId = @UserId OR UserId IS NULL) AND IsDeleted = 0
              ORDER BY CreatedAt DESC
              OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY",
            new { request.UserId, Offset = offset, request.PageSize });

        var totalCount = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM Notifications WHERE (UserId = @UserId OR UserId IS NULL) AND IsDeleted = 0",
            new { request.UserId });

        var result = new PagedResult<NotificationDto>
        {
            Items = notifications.ToList(),
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };

        return ApiResponse<PagedResult<NotificationDto>>.SuccessResponse(result);
    }
}
