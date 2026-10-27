using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.WhatsApp.Automation;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Commands;

public record ClientTrackingLinkDto(
    long Id,
    int TripId,
    string? TripNumber,
    int? BookingId,
    string PublicPath,
    DateTime ExpiresAt,
    DateTime? RevokedAt,
    int ViewCount,
    DateTime CreatedAt,
    bool IsActive);

public record ListClientTrackingLinksQuery(int Take = 50, int? TripId = null, bool ActiveOnly = false)
    : IRequest<ApiResponse<IReadOnlyList<ClientTrackingLinkDto>>>;

public class ListClientTrackingLinksQueryHandler(
    IWhatsAppAutomationRepository repository,
    ITenantContext tenantContext)
    : IRequestHandler<ListClientTrackingLinksQuery, ApiResponse<IReadOnlyList<ClientTrackingLinkDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<ClientTrackingLinkDto>>> Handle(
        ListClientTrackingLinksQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var rows = await repository.ListTrackingLinksAsync(
            tenantId, request.Take, request.TripId, request.ActiveOnly, cancellationToken);
        var now = DateTime.UtcNow;
        var mapped = rows.Select(r => new ClientTrackingLinkDto(
            r.Id,
            r.TripId,
            r.TripNumber,
            r.BookingId,
            $"/t/{{token}}",
            r.ExpiresAt,
            r.RevokedAt,
            r.ViewCount,
            r.CreatedAt,
            r.RevokedAt is null && r.ExpiresAt > now)).ToList();
        return ApiResponse<IReadOnlyList<ClientTrackingLinkDto>>.SuccessResponse(mapped);
    }
}

public record CreateClientTrackingLinkCommand(int TripId, int? HoursValid = null)
    : IRequest<ApiResponse<ClientTrackingLinkCreatedDto>>;

public record ClientTrackingLinkCreatedDto(long LinkId, string Token, string PublicUrl, DateTime ExpiresAt);

public class CreateClientTrackingLinkCommandHandler(
    IWhatsAppAutomationRepository repository,
    ITrackingTokenProtector tokenProtector,
    ITenantContext tenantContext)
    : IRequestHandler<CreateClientTrackingLinkCommand, ApiResponse<ClientTrackingLinkCreatedDto>>
{
    public async Task<ApiResponse<ClientTrackingLinkCreatedDto>> Handle(
        CreateClientTrackingLinkCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var trip = await repository.GetTripSnapshotAsync(tenantId, request.TripId, cancellationToken);
        if (trip is null)
            return ApiResponse<ClientTrackingLinkCreatedDto>.FailResponse("Trip not found.");

        await repository.RevokeTrackingLinksForTripAsync(request.TripId, cancellationToken);

        var token = TrackingToken.Generate();
        var hash = TrackingToken.Hash(token);
        var protectedToken = tokenProtector.Protect(token);
        var hours = Math.Clamp(request.HoursValid ?? 48, 1, 168);
        var expires = DateTime.UtcNow.AddHours(hours);
        var id = await repository.InsertTrackingLinkAsync(
            tenantId, request.TripId, trip.BookingId, hash, protectedToken, expires, cancellationToken);

        return ApiResponse<ClientTrackingLinkCreatedDto>.SuccessResponse(new ClientTrackingLinkCreatedDto(
            id, token, $"/t/{token}", expires));
    }
}

public record RevokeClientTrackingLinkCommand(long LinkId) : IRequest<ApiResponse<object>>;

public class RevokeClientTrackingLinkCommandHandler(
    IWhatsAppAutomationRepository repository,
    ITenantContext tenantContext)
    : IRequestHandler<RevokeClientTrackingLinkCommand, ApiResponse<object>>
{
    public async Task<ApiResponse<object>> Handle(
        RevokeClientTrackingLinkCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        await repository.RevokeTrackingLinkAsync(tenantId, request.LinkId, cancellationToken);
        return ApiResponse<object>.SuccessResponse(new { revoked = true });
    }
}
