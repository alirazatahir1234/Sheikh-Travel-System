using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Application.Features.Notifications.Commands;

public record MarkNotificationsReadCommand(int UserId, List<int>? NotificationIds = null)
    : IRequest<ApiResponse<bool>>;

public class MarkNotificationsReadCommandHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<MarkNotificationsReadCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(MarkNotificationsReadCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();

        if (request.NotificationIds is { Count: > 0 })
        {
            await connection.ExecuteAsync(
                "UPDATE Notifications SET IsRead = 1 WHERE UserId = @UserId AND Id IN @Ids",
                new { request.UserId, Ids = request.NotificationIds });
        }
        else
        {
            await connection.ExecuteAsync(
                "UPDATE Notifications SET IsRead = 1 WHERE UserId = @UserId AND IsRead = 0",
                new { request.UserId });
        }

        return ApiResponse<bool>.SuccessResponse(true, "Notifications marked as read.");
    }
}
