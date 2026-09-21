using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Notifications.DTOs;

namespace SheikhTravelSystem.Application.Features.Notifications.Queries;

public record GetNotificationRecipientsQuery(int TenantId, string? Search = null)
    : IRequest<ApiResponse<List<NotificationRecipientDto>>>;

public class GetNotificationRecipientsQueryHandler(INotificationRepository notificationRepository)
    : IRequestHandler<GetNotificationRecipientsQuery, ApiResponse<List<NotificationRecipientDto>>>
{
    public async Task<ApiResponse<List<NotificationRecipientDto>>> Handle(
        GetNotificationRecipientsQuery request, CancellationToken cancellationToken)
    {
        var rows = await notificationRepository.SearchRecipientsAsync(
            request.TenantId, request.Search, cancellationToken);
        return ApiResponse<List<NotificationRecipientDto>>.SuccessResponse(rows);
    }
}
