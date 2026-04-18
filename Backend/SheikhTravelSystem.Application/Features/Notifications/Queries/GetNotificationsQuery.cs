using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Notifications.DTOs;

namespace SheikhTravelSystem.Application.Features.Notifications.Queries;

/// <summary>
/// Retrieves paged notifications for a user, including broadcast notifications.
/// </summary>
public record GetNotificationsQuery(int UserId, int Page = 1, int PageSize = 20)
    : IRequest<ApiResponse<PagedResult<NotificationDto>>>;

/// <summary>
/// Handles notification list retrieval.
/// </summary>
public class GetNotificationsQueryHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<GetNotificationsQuery, ApiResponse<PagedResult<NotificationDto>>>
{
    /// <summary>
    /// Returns user notifications ordered by creation date descending.
    /// </summary>
    public async Task<ApiResponse<PagedResult<NotificationDto>>> Handle(GetNotificationsQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var offset = (request.Page - 1) * request.PageSize;

        var notifications = await connection.QueryAsync<NotificationDto>(
            new CommandDefinition(
                @"SELECT Id, UserId, Title, Message, Type, IsRead, ReferenceId, CreatedAt
                  FROM Notifications
                  WHERE (UserId = @UserId OR UserId IS NULL) AND IsDeleted = 0
                  ORDER BY CreatedAt DESC
                  OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY",
                new { request.UserId, Offset = offset, request.PageSize },
                cancellationToken: cancellationToken));

        var totalCount = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                "SELECT COUNT(*) FROM Notifications WHERE (UserId = @UserId OR UserId IS NULL) AND IsDeleted = 0",
                new { request.UserId },
                cancellationToken: cancellationToken));

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
