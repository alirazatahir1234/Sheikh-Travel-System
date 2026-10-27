using FluentValidation;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Notifications;
using SheikhTravelSystem.Application.Features.Website.Queries;
using SheikhTravelSystem.Application.Features.WhatsApp.Automation;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.TripFeedback;

public record GetTripFeedbackFormQuery(string Token) : IRequest<ApiResponse<TripFeedbackFormDto>>;

public class GetTripFeedbackFormQueryHandler(
    ITripFeedbackRepository feedbackRepository,
    ITrackingTokenProtector tokenProtector)
    : IRequestHandler<GetTripFeedbackFormQuery, ApiResponse<TripFeedbackFormDto>>
{
    public async Task<ApiResponse<TripFeedbackFormDto>> Handle(
        GetTripFeedbackFormQuery request, CancellationToken cancellationToken)
    {
        var resolved = await TripFeedbackLinkResolver.ResolveAsync(request.Token, feedbackRepository, tokenProtector, cancellationToken);
        if (resolved.Error is not null)
            return ApiResponse<TripFeedbackFormDto>.FailResponse(resolved.Error);

        var ctx = await feedbackRepository.GetFeedbackFormByTripAsync(resolved.Link!.TenantId, resolved.Link.TripId, cancellationToken);
        if (ctx is null)
            return ApiResponse<TripFeedbackFormDto>.FailResponse("Trip not found.");

        return ApiResponse<TripFeedbackFormDto>.SuccessResponse(new TripFeedbackFormDto(
            ctx.TripNumber,
            ctx.DriverFirstName,
            ctx.AlreadySubmitted,
            resolved.Link.ExpiresAt));
    }
}

public record SubmitTripFeedbackCommand(string Token, SubmitTripFeedbackRequest Body)
    : IRequest<ApiResponse<object>>;

public class SubmitTripFeedbackCommandValidator : AbstractValidator<SubmitTripFeedbackCommand>
{
    public SubmitTripFeedbackCommandValidator()
    {
        RuleFor(x => x.Token).NotEmpty();
        RuleFor(x => x.Body.Politeness).InclusiveBetween(1, 5);
        RuleFor(x => x.Body.OnTimeDelivery).InclusiveBetween(1, 5);
        RuleFor(x => x.Body.VehicleCondition).InclusiveBetween(1, 5);
        RuleFor(x => x.Body.Comment).MaximumLength(500);
    }
}

public class SubmitTripFeedbackCommandHandler(
    ITripFeedbackRepository feedbackRepository,
    ITrackingTokenProtector tokenProtector)
    : IRequestHandler<SubmitTripFeedbackCommand, ApiResponse<object>>
{
    public async Task<ApiResponse<object>> Handle(
        SubmitTripFeedbackCommand request, CancellationToken cancellationToken)
    {
        var resolved = await TripFeedbackLinkResolver.ResolveAsync(request.Token, feedbackRepository, tokenProtector, cancellationToken);
        if (resolved.Error is not null)
            return ApiResponse<object>.FailResponse(resolved.Error);

        var link = resolved.Link!;
        if (link.UsedAt is not null || link.ExpiresAt < DateTime.UtcNow)
            return ApiResponse<object>.FailResponse("This feedback link has expired.");

        var ctx = await feedbackRepository.GetFeedbackFormByTripAsync(link.TenantId, link.TripId, cancellationToken);
        if (ctx is null)
            return ApiResponse<object>.FailResponse("Trip not found.");
        if (ctx.AlreadySubmitted)
            return ApiResponse<object>.FailResponse("Feedback was already submitted for this trip.");

        var payload = new SubmitTripFeedbackPayload(
            request.Body.Politeness,
            request.Body.OnTimeDelivery,
            request.Body.VehicleCondition,
            request.Body.Comment);

        var inserted = await feedbackRepository.TryInsertDetailedRatingAsync(
            link.TenantId, link.TripId, ctx.DriverId, payload, "WebForm", cancellationToken);
        if (!inserted)
            return ApiResponse<object>.FailResponse("Feedback was already submitted for this trip.");

        await feedbackRepository.MarkFeedbackLinkUsedAsync(link.Id, cancellationToken);
        if (ctx.DriverId is > 0)
            await feedbackRepository.RefreshDriverAggregateRatingAsync(link.TenantId, ctx.DriverId.Value, cancellationToken);

        return ApiResponse<object>.SuccessResponse(new { submitted = true }, "Thank you for your feedback.");
    }
}

public record ListTripClientRatingsQuery(int Page = 1, int PageSize = 25, int? DriverId = null, bool IncludeHidden = false)
    : IRequest<ApiResponse<PagedResult<TripClientRatingDto>>>;

public class ListTripClientRatingsQueryHandler(
    ITripFeedbackRepository feedbackRepository,
    ITenantContext tenantContext)
    : IRequestHandler<ListTripClientRatingsQuery, ApiResponse<PagedResult<TripClientRatingDto>>>
{
    public async Task<ApiResponse<PagedResult<TripClientRatingDto>>> Handle(
        ListTripClientRatingsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var (items, total) = await feedbackRepository.ListRatingsAsync(
            tenantId, request.Page, request.PageSize, request.DriverId, request.IncludeHidden, cancellationToken);

        var mapped = items.Select(r => new TripClientRatingDto(
            r.Id, r.TripId, r.TripNumber, r.DriverId, r.DriverName,
            r.Score, r.Politeness, r.OnTimeDelivery, r.VehicleCondition,
            r.Comment, r.Source, r.IsHidden, r.CreatedAt)).ToList();

        return ApiResponse<PagedResult<TripClientRatingDto>>.SuccessResponse(new PagedResult<TripClientRatingDto>
        {
            Items = mapped,
            TotalCount = total,
            Page = request.Page,
            PageSize = request.PageSize
        });
    }
}

public record GetDriverClientRatingSummaryQuery(int DriverId)
    : IRequest<ApiResponse<DriverClientRatingSummaryDto>>;

public class GetDriverClientRatingSummaryQueryHandler(
    ITripFeedbackRepository feedbackRepository,
    ITenantContext tenantContext)
    : IRequestHandler<GetDriverClientRatingSummaryQuery, ApiResponse<DriverClientRatingSummaryDto>>
{
    public async Task<ApiResponse<DriverClientRatingSummaryDto>> Handle(
        GetDriverClientRatingSummaryQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var summary = await feedbackRepository.GetDriverSummaryAsync(tenantId, request.DriverId, cancellationToken);
        if (summary is null || summary.ReviewCount == 0)
            return ApiResponse<DriverClientRatingSummaryDto>.SuccessResponse(
                new DriverClientRatingSummaryDto(null, 0, null, null, null));

        return ApiResponse<DriverClientRatingSummaryDto>.SuccessResponse(new DriverClientRatingSummaryDto(
            summary.AverageRating, summary.ReviewCount, summary.AvgPoliteness, summary.AvgOnTime, summary.AvgVehicle));
    }
}

public record SetTripRatingVisibilityCommand(int RatingId, bool Hidden)
    : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => Hidden ? "HideRating" : "ShowRating";
    public string AuditEntityName => "TripRating";
    public int? AuditEntityId => RatingId;
}

public class SetTripRatingVisibilityCommandHandler(
    ITripFeedbackRepository feedbackRepository,
    ITenantContext tenantContext)
    : IRequestHandler<SetTripRatingVisibilityCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(
        SetTripRatingVisibilityCommand request, CancellationToken cancellationToken)
    {
        await feedbackRepository.SetRatingHiddenAsync(
            tenantContext.GetRequiredTenantId(), request.RatingId, request.Hidden, cancellationToken);
        return ApiResponse<bool>.SuccessResponse(true, request.Hidden ? "Rating hidden from public aggregate." : "Rating visible again.");
    }
}

public record SetDriverPublicRatingOptOutCommand(int DriverId, bool OptOut)
    : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => OptOut ? "OptOutPublicRating" : "OptInPublicRating";
    public string AuditEntityName => "Driver";
    public int? AuditEntityId => DriverId;
}

public class SetDriverPublicRatingOptOutCommandHandler(
    ITripFeedbackRepository feedbackRepository,
    ITenantContext tenantContext)
    : IRequestHandler<SetDriverPublicRatingOptOutCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(
        SetDriverPublicRatingOptOutCommand request, CancellationToken cancellationToken)
    {
        await feedbackRepository.SetDriverPublicOptOutAsync(
            tenantContext.GetRequiredTenantId(), request.DriverId, request.OptOut, cancellationToken);
        return ApiResponse<bool>.SuccessResponse(true, request.OptOut
            ? "Driver hidden from public rating leaderboard."
            : "Driver eligible for public rating display.");
    }
}

public record GetPublicDriverRatingsQuery(int Take = 12)
    : IRequest<ApiResponse<IReadOnlyList<PublicDriverRatingDto>>>;

public class GetPublicDriverRatingsQueryHandler(
    ITripFeedbackRepository feedbackRepository,
    ITenantContext tenantContext)
    : IRequestHandler<GetPublicDriverRatingsQuery, ApiResponse<IReadOnlyList<PublicDriverRatingDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<PublicDriverRatingDto>>> Handle(
        GetPublicDriverRatingsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = WebsiteTenant.Resolve(tenantContext);
        var rows = await feedbackRepository.ListPublicDriverRatingsAsync(tenantId, request.Take, cancellationToken);
        var mapped = rows.Select(r => new PublicDriverRatingDto(
            r.DisplayName, r.AverageRating, r.ReviewCount,
            r.AvgPoliteness, r.AvgOnTime, r.AvgVehicle)).ToList();
        return ApiResponse<IReadOnlyList<PublicDriverRatingDto>>.SuccessResponse(mapped);
    }
}

public interface ITripFeedbackAutomation
{
    Task TrySendFeedbackInviteOnTripCompletedAsync(int tenantId, int tripId, CancellationToken cancellationToken = default);
}

public sealed class TripFeedbackAutomation(
    ITripFeedbackRepository feedbackRepository,
    ICustomerInvoiceRepository invoiceRepository,
    ITrackingTokenProtector tokenProtector,
    INotificationService notifications,
    IConfiguration configuration,
    ILogger<TripFeedbackAutomation> logger) : ITripFeedbackAutomation
{
    public async Task TrySendFeedbackInviteOnTripCompletedAsync(
        int tenantId, int tripId, CancellationToken cancellationToken = default)
    {
        if (await feedbackRepository.HasRatingForTripAsync(tenantId, tripId, cancellationToken))
            return;

        var trip = await invoiceRepository.GetTripSnapshotAsync(tripId, tenantId, cancellationToken);
        if (trip is null || string.IsNullOrWhiteSpace(trip.CustomerEmail))
        {
            logger.LogDebug("Trip {TripId} feedback invite skipped — no customer email.", tripId);
            return;
        }

        var token = TrackingToken.Generate();
        var hash = TrackingToken.Hash(token);
        var protectedToken = tokenProtector.Protect(token);
        var expires = DateTime.UtcNow.AddDays(14);
        await feedbackRepository.InsertFeedbackLinkAsync(tenantId, tripId, hash, protectedToken, expires, cancellationToken);

        var portal = configuration["Notifications:Email:PortalUrl"]?.TrimEnd('/')
                     ?? configuration["PortalPaymentGateway:ReturnBaseUrl"]?.TrimEnd('/')
                     ?? "https://app.sheikhgo.com";
        var link = $"{portal}/rate/{token}";
        var tripRef = trip.TripNumber ?? tripId.ToString();
        var body = $"""
            Dear {trip.CustomerName ?? "Customer"},

            Your trip {tripRef} is complete. Please take a moment to rate your driver (politeness, on-time delivery, and vehicle condition):

            {link}

            This link expires in 14 days. Thank you for choosing SheikhGo.
            """;

        try
        {
            await notifications.CreateAndDispatchAsync(new NotificationCreateOptions(
                UserId: null,
                TenantId: tenantId,
                Title: $"Rate your trip {tripRef}",
                Message: body,
                Type: NotificationType.TripCompleted,
                ReferenceId: tripId,
                Priority: 2,
                Channel: NotificationChannels.Email,
                Email: trip.CustomerEmail.Trim(),
                Module: "Operations",
                TemplateKey: "trip_feedback_request",
                SendNow: true,
                Variables: new Dictionary<string, string>
                {
                    ["TripNumber"] = tripRef,
                    ["FeedbackUrl"] = link,
                    ["CustomerName"] = trip.CustomerName ?? "",
                    ["Message"] = body
                },
                Channels: [NotificationChannels.Email]), cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Trip {TripId} feedback email failed.", tripId);
        }
    }
}

internal sealed record ResolvedFeedbackLink(TripFeedbackInviteRow? Link, string? Error);

internal static class TripFeedbackLinkResolver
{
    internal static async Task<ResolvedFeedbackLink> ResolveAsync(
        string token,
        ITripFeedbackRepository feedbackRepository,
        ITrackingTokenProtector tokenProtector,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length < 16)
            return new ResolvedFeedbackLink(null, "Not found.");

        var hash = TrackingToken.Hash(token.Trim());
        var link = await feedbackRepository.GetFeedbackLinkByHashAsync(hash, cancellationToken);
        if (link is null)
            return new ResolvedFeedbackLink(null, "Not found.");

        try
        {
            var plain = tokenProtector.Unprotect(link.TokenProtected);
            if (!string.Equals(plain, token.Trim(), StringComparison.Ordinal))
                return new ResolvedFeedbackLink(null, "Not found.");
        }
        catch
        {
            return new ResolvedFeedbackLink(null, "Not found.");
        }

        return new ResolvedFeedbackLink(link, null);
    }
}
