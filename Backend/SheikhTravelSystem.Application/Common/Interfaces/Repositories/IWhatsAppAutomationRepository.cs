using System.Data;
using SheikhTravelSystem.Application.Features.WhatsApp.Automation;
using SheikhTravelSystem.Application.Features.WhatsApp.DTOs;

namespace SheikhTravelSystem.Application.Common.Interfaces.Repositories;

public interface IWhatsAppAutomationRepository
{
    Task<IReadOnlyList<WhatsAppAutomationRuleDto>> GetRulesAsync(int tenantId, CancellationToken ct = default);

    Task<WhatsAppAutomationRuleDto?> GetRuleAsync(int tenantId, string eventType, CancellationToken ct = default);

    Task<WhatsAppAutomationRuleDto> UpsertRuleAsync(WhatsAppAutomationRuleDto rule, CancellationToken ct = default);

    Task<long> InsertEventAsync(
        AutomationEventInsert request,
        IDbTransaction? tx = null,
        CancellationToken ct = default);

    Task<WhatsAppAutomationEventRow?> GetEventAsync(long id, CancellationToken ct = default);

    Task<bool> ClaimEventAsync(long id, TimeSpan lockDuration, CancellationToken ct = default);

    Task MarkEventSentAsync(long id, int? messageId, string? recipientPhone, CancellationToken ct = default);

    Task MarkEventSkippedAsync(long id, string skipReason, CancellationToken ct = default);

    Task MarkEventFailedAsync(long id, string error, CancellationToken ct = default);

    Task RescheduleEventAsync(long id, DateTime newDueAtUtc, CancellationToken ct = default);

    Task CancelPendingForBookingAsync(
        int tenantId, int bookingId, IReadOnlyList<string>? eventTypes = null, CancellationToken ct = default);

    Task CancelPendingForTripAsync(int tenantId, int tripId, CancellationToken ct = default);

    Task<IReadOnlyList<long>> ListDueEventIdsAsync(DateTime utcNow, int take, CancellationToken ct = default);

    Task<IReadOnlyList<WhatsAppAutomationTimelineItemDto>> GetBookingTimelineAsync(
        int tenantId, int bookingId, CancellationToken ct = default);

    // Consents
    Task<WhatsAppConsentDto?> GetConsentAsync(int tenantId, string waId, CancellationToken ct = default);

    Task UpsertConsentOptInAsync(
        int tenantId, string waId, string source, string? note, int? userId, CancellationToken ct = default);

    Task OptOutAsync(int tenantId, string waId, CancellationToken ct = default);

    Task<bool> HasActiveOptInAsync(int tenantId, string waId, CancellationToken ct = default);

    // Tracking
    Task<TripTrackingLinkRow?> GetActiveTrackingLinkAsync(int tripId, CancellationToken ct = default);

    Task<TripTrackingLinkRow?> GetTrackingLinkByHashAsync(byte[] tokenHash, CancellationToken ct = default);

    Task<long> InsertTrackingLinkAsync(
        int tenantId, int tripId, int? bookingId, byte[] tokenHash, string tokenProtected,
        DateTime expiresAt, CancellationToken ct = default);

    Task RevokeTrackingLinksForTripAsync(int tripId, CancellationToken ct = default);

    Task ExpireTrackingLinksForTripAsync(int tripId, DateTime expiresAtUtc, CancellationToken ct = default);

    Task IncrementTrackingViewAsync(long linkId, CancellationToken ct = default);

    // Ratings
    Task<bool> TryInsertTripRatingAsync(
        int tenantId, int tripId, int? driverId, int score, int? messageId, CancellationToken ct = default);

    // Booking snapshot for composer
    Task<AutomationBookingSnapshot?> GetBookingSnapshotAsync(int tenantId, int bookingId, CancellationToken ct = default);

    Task<AutomationTripSnapshot?> GetTripSnapshotAsync(int tenantId, int tripId, CancellationToken ct = default);

    Task<IReadOnlyList<int>> ListActiveEnRouteTripVehicleIdsAsync(CancellationToken ct = default);

    Task LinkMessageToAutomationAsync(int messageId, long automationEventId, CancellationToken ct = default);
}

public sealed record AutomationEventInsert(
    int TenantId,
    string EventType,
    int? BookingId,
    int? TripId,
    string DedupeKey,
    DateTime DueAt,
    string? PayloadJson = null);

public sealed record WhatsAppAutomationEventRow(
    long Id,
    int TenantId,
    string EventType,
    int? BookingId,
    int? TripId,
    string DedupeKey,
    byte Status,
    string? SkipReason,
    DateTime DueAt,
    int Attempts,
    string? Error,
    int? MessageId,
    string? RecipientPhone,
    string? PayloadJson);

public sealed record TripTrackingLinkRow(
    long Id,
    int TenantId,
    int TripId,
    int? BookingId,
    byte[] TokenHash,
    string TokenProtected,
    DateTime ExpiresAt,
    DateTime? RevokedAt,
    int ViewCount);

public sealed record AutomationBookingSnapshot(
    int Id,
    string BookingNumber,
    int Status,
    DateTime PickupTime,
    string? PickupAddress,
    int CustomerId,
    string? CustomerName,
    string? CustomerPhone,
    string? PreferredLanguage,
    int? DriverId,
    string? DriverFirstName,
    int? VehicleId,
    string? VehicleDescription,
    string? PlateNumber,
    string? BookerPhone);

public sealed record AutomationTripSnapshot(
    int Id,
    string? TripNumber,
    int Status,
    int? BookingId,
    int? DriverId,
    string? DriverFirstName,
    int? VehicleId,
    string? VehicleDescription,
    string? PlateNumber,
    double? PickupLat,
    double? PickupLng,
    string? PickupAddress,
    double? DropoffLat,
    double? DropoffLng,
    string? DropoffAddress,
    DateTime? PickupAt,
    double? DistanceKm,
    decimal? TotalAmount,
    string? Currency,
    int TripType);
