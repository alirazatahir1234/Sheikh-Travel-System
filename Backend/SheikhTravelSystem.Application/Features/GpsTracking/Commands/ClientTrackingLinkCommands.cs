using MediatR;
using Microsoft.Extensions.Logging;
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
    /// <summary>Real `/t/{token}` when Unprotect succeeds; null when token cannot be revealed.</summary>
    string? PublicPath,
    DateTime ExpiresAt,
    DateTime? RevokedAt,
    int ViewCount,
    DateTime CreatedAt,
    bool IsActive,
    /// <summary>False when TokenProtected is missing or Data Protection keys cannot decrypt it (regenerate).</summary>
    bool CanOpen,
    string? CustomerName,
    int? VehicleId,
    string? PlateNumber,
    string? VehicleDescription);

public record ListClientTrackingLinksQuery(int Take = 50, int? TripId = null, bool ActiveOnly = false)
    : IRequest<ApiResponse<IReadOnlyList<ClientTrackingLinkDto>>>;

public class ListClientTrackingLinksQueryHandler(
    IWhatsAppAutomationRepository repository,
    ITenantContext tenantContext,
    ITrackingTokenProtector tokenProtector,
    ILogger<ListClientTrackingLinksQueryHandler> logger)
    : IRequestHandler<ListClientTrackingLinksQuery, ApiResponse<IReadOnlyList<ClientTrackingLinkDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<ClientTrackingLinkDto>>> Handle(
        ListClientTrackingLinksQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var rows = await repository.ListTrackingLinksAsync(
            tenantId, request.Take, request.TripId, request.ActiveOnly, cancellationToken);
        var now = DateTime.UtcNow;
        var mapped = rows.Select(r =>
        {
            var path = TryRevealPublicPath(r.Id, r.TokenProtected);
            return new ClientTrackingLinkDto(
                r.Id,
                r.TripId,
                r.TripNumber,
                r.BookingId,
                path,
                r.ExpiresAt,
                r.RevokedAt,
                r.ViewCount,
                r.CreatedAt,
                r.RevokedAt is null && r.ExpiresAt > now,
                path is not null,
                r.CustomerName,
                r.VehicleId,
                r.PlateNumber,
                r.VehicleDescription);
        }).ToList();
        return ApiResponse<IReadOnlyList<ClientTrackingLinkDto>>.SuccessResponse(mapped);
    }

    private string? TryRevealPublicPath(long linkId, string? tokenProtected)
    {
        if (string.IsNullOrWhiteSpace(tokenProtected))
        {
            logger.LogWarning("Tracking link {LinkId} has empty TokenProtected; cannot reveal public URL.", linkId);
            return null;
        }

        try
        {
            var token = tokenProtector.Unprotect(tokenProtected);
            if (string.IsNullOrWhiteSpace(token))
            {
                logger.LogWarning("Tracking link {LinkId} Unprotect returned empty token.", linkId);
                return null;
            }

            return $"/t/{token.Trim()}";
        }
        catch (Exception ex)
        {
            // Typical after API recycle when Data Protection keys were ephemeral.
            logger.LogWarning(
                ex,
                "Tracking link {LinkId} Unprotect failed; admin must regenerate the link to obtain a public URL.",
                linkId);
            return null;
        }
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

public record RevealClientTrackingLinkUrlQuery(long LinkId)
    : IRequest<ApiResponse<ClientTrackingLinkUrlDto>>;

public record ClientTrackingLinkUrlDto(long LinkId, string PublicUrl);

public class RevealClientTrackingLinkUrlQueryHandler(
    IWhatsAppAutomationRepository repository,
    ITenantContext tenantContext,
    ITrackingTokenProtector tokenProtector,
    ILogger<RevealClientTrackingLinkUrlQueryHandler> logger)
    : IRequestHandler<RevealClientTrackingLinkUrlQuery, ApiResponse<ClientTrackingLinkUrlDto>>
{
    public async Task<ApiResponse<ClientTrackingLinkUrlDto>> Handle(
        RevealClientTrackingLinkUrlQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var link = await repository.GetTrackingLinkByIdAsync(tenantId, request.LinkId, cancellationToken);
        if (link is null)
            return ApiResponse<ClientTrackingLinkUrlDto>.FailResponse("Link not found.");

        if (string.IsNullOrWhiteSpace(link.TokenProtected))
            return ApiResponse<ClientTrackingLinkUrlDto>.FailResponse(
                "This link has no stored token. Generate a new tracking link for the trip.");

        try
        {
            var token = tokenProtector.Unprotect(link.TokenProtected);
            if (string.IsNullOrWhiteSpace(token))
                return ApiResponse<ClientTrackingLinkUrlDto>.FailResponse(
                    "Token reveal failed. Generate a new tracking link for the trip.");

            return ApiResponse<ClientTrackingLinkUrlDto>.SuccessResponse(
                new ClientTrackingLinkUrlDto(link.Id, $"/t/{token.Trim()}"));
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Reveal failed for tracking link {LinkId}; Data Protection keys likely rotated — regenerate the link.",
                request.LinkId);
            return ApiResponse<ClientTrackingLinkUrlDto>.FailResponse(
                "Cannot decrypt this link’s token (server key rotation). Generate a new tracking link for the trip, then Open.");
        }
    }
}
