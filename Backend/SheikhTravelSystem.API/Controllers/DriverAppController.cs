using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.DriverApp.Commands;
using SheikhTravelSystem.Application.Features.DriverApp.DTOs;
using SheikhTravelSystem.Application.Features.DriverApp.Queries;
using SheikhTravelSystem.Application.Features.FuelLogs.DTOs;

namespace SheikhTravelSystem.API.Controllers;

[ApiController]
[Route("api/driver-app")]
public class DriverAppController : BaseApiController
{
    // ── Auth ──────────────────────────────────────────────────────────────────

    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("auth/login")]
    public async Task<IActionResult> Login([FromBody] DriverLoginRequest request)
        => Ok(await Mediator.Send(new DriverLoginCommand(request.Phone, request.Password)));

    // ── Profile & Dashboard ──────────────────────────────────────────────────

    [Authorize(Roles = "Driver")]
    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile()
        => Ok(await Mediator.Send(new GetDriverProfileQuery()));

    [Authorize(Roles = "Driver")]
    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard()
        => Ok(await Mediator.Send(new GetDriverDashboardQuery()));

    [Authorize(Roles = "Driver")]
    [HttpGet("status")]
    public async Task<IActionResult> GetDriverStatus()
        => Ok(await Mediator.Send(new GetDriverStatusQuery()));

    [Authorize(Roles = "Driver")]
    [HttpPost("status")]
    public async Task<IActionResult> SetDriverStatus([FromBody] SetDriverStatusRequest request)
        => Ok(await Mediator.Send(new SetDriverStatusCommand(request.Status)));

    // ── Trips ────────────────────────────────────────────────────────────────

    [Authorize(Roles = "Driver")]
    [HttpGet("trips")]
    public async Task<IActionResult> GetTrips()
        => Ok(await Mediator.Send(new GetDriverTripsQuery()));

    /// <summary>Accept trip / start driving to pickup (TripStatus.Started).</summary>
    [Authorize(Roles = "Driver")]
    [HttpPost("trips/{id:int}/accept")]
    public async Task<IActionResult> AcceptTrip(int id)
        => Ok(await Mediator.Send(new DriverAdvanceTripCommand(id, DriverTripAction.Accept)));

    /// <summary>Arrived at pickup (TripStatus.AtPickup).</summary>
    [Authorize(Roles = "Driver")]
    [HttpPost("trips/{id:int}/arrived")]
    public async Task<IActionResult> ArrivedTrip(int id)
        => Ok(await Mediator.Send(new DriverAdvanceTripCommand(id, DriverTripAction.Arrived)));

    /// <summary>Passenger onboard / enroute (TripStatus.Enroute).</summary>
    [Authorize(Roles = "Driver")]
    [HttpPost("trips/{id:int}/onboard")]
    public async Task<IActionResult> OnboardTrip(int id)
        => Ok(await Mediator.Send(new DriverAdvanceTripCommand(id, DriverTripAction.Onboard)));

    /// <summary>Legacy alias for Accept (driving to pickup).</summary>
    [Authorize(Roles = "Driver")]
    [HttpPost("trips/{id:int}/start")]
    public async Task<IActionResult> StartTrip(int id)
        => Ok(await Mediator.Send(new DriverAdvanceTripCommand(id, DriverTripAction.Accept)));

    [Authorize(Roles = "Driver")]
    [HttpPost("trips/{id:int}/complete")]
    public async Task<IActionResult> CompleteTrip(int id)
        => Ok(await Mediator.Send(new DriverAdvanceTripCommand(id, DriverTripAction.Complete)));

    [Authorize(Roles = "Driver")]
    [HttpPost("trips/{id:int}/reject")]
    public async Task<IActionResult> RejectTrip(int id, [FromBody] string reason)
        => Ok(await Mediator.Send(new DriverAdvanceTripCommand(id, DriverTripAction.Reject, reason)));

    [Authorize(Roles = "Driver")]
    [HttpGet("trips/{id:int}/payment-summary")]
    public async Task<IActionResult> GetTripPaymentSummary(int id)
        => Ok(await Mediator.Send(new GetDriverTripPaymentSummaryQuery(id)));

    [Authorize(Roles = "Driver")]
    [HttpPost("trips/{id:int}/collect-payment")]
    public async Task<IActionResult> CollectTripPayment(int id, [FromBody] DriverCollectPaymentRequest request)
        => Ok(await Mediator.Send(new DriverCollectPaymentCommand(
            id, request.AmountReceived, request.PaymentMethod, request.ReferenceNumber, request.Notes)));

    [Authorize(Roles = "Driver")]
    [HttpPost("trips/location")]
    public async Task<IActionResult> PostLocation([FromBody] DriverLocationDto location)
        => Ok(await Mediator.Send(new DriverPostLocationCommand(location)));

    [Authorize(Roles = "Driver")]
    [HttpPost("location/batch")]
    public async Task<IActionResult> PostLocationBatch([FromBody] DriverLocationBatchDto batch)
        => Ok(await Mediator.Send(new DriverPostLocationBatchCommand(batch.Positions)));

    // ── Attendance ───────────────────────────────────────────────────────────

    [Authorize(Roles = "Driver")]
    [HttpPost("attendance/check-in")]
    public async Task<IActionResult> CheckIn([FromBody] DriverCheckInRequest request)
        => Ok(await Mediator.Send(new DriverCheckInCommand(request.Latitude, request.Longitude)));

    [Authorize(Roles = "Driver")]
    [HttpPost("attendance/check-out")]
    public async Task<IActionResult> CheckOut([FromBody] DriverCheckOutRequest request)
        => Ok(await Mediator.Send(new DriverCheckOutCommand(request.Latitude, request.Longitude)));

    [Authorize(Roles = "Driver")]
    [HttpGet("attendance/history")]
    public async Task<IActionResult> GetAttendanceHistory(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 30)
        => Ok(await Mediator.Send(new GetDriverAttendanceHistoryQuery(from, to, page, pageSize)));

    // ── Fuel ─────────────────────────────────────────────────────────────────

    [Authorize(Roles = "Driver")]
    [HttpGet("fuel-receipts")]
    public async Task<IActionResult> GetFuelReceipts([FromQuery] int page = 1, [FromQuery] int pageSize = 30)
        => Ok(await Mediator.Send(new GetDriverFuelReceiptsQuery(page, pageSize)));

    [Authorize(Roles = "Driver")]
    [HttpPost("fuel-receipts")]
    [RequestSizeLimit(12_000_000)]
    public async Task<IActionResult> SubmitFuelReceipt(
        [FromForm] int vehicleId,
        [FromForm] decimal liters,
        [FromForm] decimal pricePerLiter,
        [FromForm] decimal odometerReading,
        [FromForm] string fuelType,
        [FromForm] DateTime? fuelDate,
        [FromForm] string? station,
        IFormFile? receipt)
    {
        if (!TryParseFuelType(fuelType, out var parsedType))
            return BadRequest(ApiResponse<object>.FailResponse("Invalid fuelType. Use Petrol, Diesel, or CNG."));

        DriverFuelReceiptUploadFile? file = null;
        if (receipt is { Length: > 0 })
        {
            file = new DriverFuelReceiptUploadFile(
                receipt.OpenReadStream(), receipt.FileName, receipt.ContentType, receipt.Length);
        }

        return Ok(await Mediator.Send(new DriverSubmitFuelReceiptFormCommand(
            vehicleId, liters, pricePerLiter, odometerReading, parsedType, fuelDate, station, file)));
    }

    /// <summary>Legacy JSON submit without receipt image.</summary>
    [Authorize(Roles = "Driver")]
    [HttpPost("fuel-receipts/json")]
    public async Task<IActionResult> SubmitFuelReceiptJson([FromBody] CreateFuelLogDto fuelLog)
        => Ok(await Mediator.Send(new DriverSubmitFuelReceiptCommand(fuelLog)));

    [Authorize(Roles = "Driver")]
    [HttpPost("fuel-receipts/scan")]
    [RequestSizeLimit(12_000_000)]
    public async Task<IActionResult> ScanFuelReceipt(IFormFile receipt)
    {
        if (receipt is null || receipt.Length <= 0)
            return BadRequest(ApiResponse<object>.FailResponse("Receipt image is required."));

        return Ok(await Mediator.Send(new ScanFuelReceiptOcrCommand(
            new DriverFuelReceiptUploadFile(
                receipt.OpenReadStream(), receipt.FileName, receipt.ContentType, receipt.Length))));
    }

    private static bool TryParseFuelType(string? value, out Domain.Enums.FuelType fuelType)
    {
        fuelType = Domain.Enums.FuelType.Petrol;
        if (string.IsNullOrWhiteSpace(value)) return false;
        if (Enum.TryParse<Domain.Enums.FuelType>(value.Trim(), ignoreCase: true, out fuelType) &&
            Enum.IsDefined(fuelType))
            return true;
        return int.TryParse(value, out var n) && Enum.IsDefined(typeof(Domain.Enums.FuelType), n)
            && (fuelType = (Domain.Enums.FuelType)n) is not 0;
    }

    // ── Earnings ──────────────────────────────────────────────────────────────

    [Authorize(Roles = "Driver")]
    [HttpGet("earnings")]
    public async Task<IActionResult> GetEarnings([FromQuery] DateTime? from, [FromQuery] DateTime? to)
        => Ok(await Mediator.Send(new GetDriverEarningsQuery(from, to)));

    // ── Notifications (proxy to Notification Center) ─────────────────────────

    [Authorize(Roles = "Driver")]
    [HttpGet("notifications")]
    public async Task<IActionResult> GetNotifications(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 30,
        [FromQuery] bool? unreadOnly = null,
        [FromQuery] string? module = null,
        [FromQuery] bool archived = false)
    {
        if (!TryGetCurrentUserId(out var userId) || !TryGetCurrentTenantId(out var tenantId))
            return Unauthorized();

        return Ok(await Mediator.Send(new SheikhTravelSystem.Application.Features.Notifications.Queries.GetNotificationsQuery(
            tenantId, userId, page, pageSize, unreadOnly, Module: module, Archived: archived)));
    }

    [Authorize(Roles = "Driver")]
    [HttpGet("notifications/unread-count")]
    public async Task<IActionResult> GetUnreadNotificationCount()
    {
        if (!TryGetCurrentUserId(out var userId) || !TryGetCurrentTenantId(out var tenantId))
            return Unauthorized();

        return Ok(await Mediator.Send(
            new SheikhTravelSystem.Application.Features.Notifications.Queries.GetUnreadNotificationCountQuery(tenantId, userId)));
    }

    [Authorize(Roles = "Driver")]
    [HttpPut("notifications/read")]
    public async Task<IActionResult> MarkNotificationsRead([FromBody] List<int>? notificationIds)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        return Ok(await Mediator.Send(
            new SheikhTravelSystem.Application.Features.Notifications.Commands.MarkNotificationsReadCommand(
                userId, notificationIds)));
    }

    [Authorize(Roles = "Driver")]
    [HttpPost("notifications/archive")]
    public async Task<IActionResult> ArchiveNotifications(
        [FromBody] SheikhTravelSystem.Application.Features.Notifications.DTOs.NotificationLifecycleIdsRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        return Ok(await Mediator.Send(
            new SheikhTravelSystem.Application.Features.Notifications.Commands.ArchiveNotificationsCommand(
                userId, request.Ids ?? [])));
    }

    [Authorize(Roles = "Driver")]
    [HttpPost("notifications/restore")]
    public async Task<IActionResult> RestoreNotifications(
        [FromBody] SheikhTravelSystem.Application.Features.Notifications.DTOs.NotificationLifecycleIdsRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        return Ok(await Mediator.Send(
            new SheikhTravelSystem.Application.Features.Notifications.Commands.RestoreNotificationsCommand(
                userId, request.Ids ?? [])));
    }

    [Authorize(Roles = "Driver")]
    [HttpDelete("notifications/{id:int}")]
    public async Task<IActionResult> DeleteNotification(int id)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        return Ok(await Mediator.Send(
            new SheikhTravelSystem.Application.Features.Notifications.Commands.DeleteNotificationCommand(userId, id)));
    }

    private bool TryGetCurrentUserId(out int userId)
    {
        var claim = User.FindFirst("userId")?.Value
                    ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(claim, out userId);
    }

    private bool TryGetCurrentTenantId(out int tenantId)
    {
        var claim = User.FindFirst("tenantId")?.Value ?? User.FindFirst("tenant_id")?.Value;
        return int.TryParse(claim, out tenantId);
    }

    // ── SOS ───────────────────────────────────────────────────────────────────

    [Authorize(Roles = "Driver")]
    [HttpPost("sos")]
    public async Task<IActionResult> SendSos([FromBody] DriverSosRequest? request)
        => Ok(await Mediator.Send(new DriverSosCommand(
            request?.Latitude,
            request?.Longitude,
            request?.Message)));

    // ── Timeline ──────────────────────────────────────────────────────────────

    [Authorize(Roles = "Driver")]
    [HttpGet("timeline")]
    public async Task<IActionResult> GetTimeline([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        => Ok(await Mediator.Send(new GetDriverTimelineQuery(page, pageSize)));

    // ── Inspections ───────────────────────────────────────────────────────────

    [Authorize(Roles = "Driver")]
    [HttpGet("inspection/template")]
    public async Task<IActionResult> GetInspectionTemplate()
        => Ok(await Mediator.Send(new GetDriverInspectionTemplateQuery()));

    [Authorize(Roles = "Driver")]
    [HttpGet("inspection/vehicles")]
    public async Task<IActionResult> GetInspectionVehicles()
        => Ok(await Mediator.Send(new GetDriverVehiclesForInspectionQuery()));

    [Authorize(Roles = "Driver")]
    [HttpGet("inspection/history")]
    public async Task<IActionResult> GetInspectionHistory([FromQuery] int page = 1, [FromQuery] int pageSize = 30)
        => Ok(await Mediator.Send(new GetDriverInspectionHistoryQuery(page, pageSize)));

    [Authorize(Roles = "Driver")]
    [HttpPost("inspection")]
    [RequestSizeLimit(40_000_000)]
    public async Task<IActionResult> SubmitInspection(
        [FromForm] int vehicleId,
        [FromForm] int? templateId,
        [FromForm] decimal? odometerReading,
        [FromForm] string? comments,
        [FromForm] string resultsJson,
        [FromForm] string? overallResult,
        List<IFormFile>? photos,
        IFormFile? signature)
    {
        List<InspectionResultItemDto> results;
        try
        {
            results = System.Text.Json.JsonSerializer.Deserialize<List<InspectionResultItemDto>>(
                resultsJson,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? [];
        }
        catch
        {
            return BadRequest(ApiResponse<object>.FailResponse("Invalid resultsJson."));
        }

        var photoFiles = new List<DriverInspectionUploadFile>();
        if (photos is not null)
        {
            foreach (var p in photos)
            {
                if (p.Length <= 0) continue;
                photoFiles.Add(new DriverInspectionUploadFile(
                    p.OpenReadStream(), p.FileName, p.ContentType, p.Length));
            }
        }

        DriverInspectionUploadFile? sig = null;
        if (signature is { Length: > 0 })
        {
            sig = new DriverInspectionUploadFile(
                signature.OpenReadStream(), signature.FileName, signature.ContentType, signature.Length);
        }

        return Ok(await Mediator.Send(new SubmitDriverInspectionCommand(
            vehicleId, templateId, odometerReading, comments, results, overallResult, photoFiles, sig)));
    }

    // ── Documents ─────────────────────────────────────────────────────────────

    [Authorize(Roles = "Driver")]
    [HttpGet("documents")]
    public async Task<IActionResult> GetDocuments()
        => Ok(await Mediator.Send(new GetDriverAppDocumentsQuery()));

    [Authorize(Roles = "Driver")]
    [HttpPost("documents/upload")]
    [RequestSizeLimit(20_000_000)]
    public async Task<IActionResult> UploadDocument(
        [FromForm] string documentType,
        [FromForm] DateTime? expiryDate,
        [FromForm] int? vehicleId,
        IFormFile file)
    {
        if (file is null || file.Length <= 0)
            return BadRequest(ApiResponse<object>.FailResponse("File is required."));

        return Ok(await Mediator.Send(new UploadDriverAppDocumentCommand(
            documentType,
            expiryDate,
            file.OpenReadStream(),
            file.FileName,
            file.ContentType,
            file.Length,
            vehicleId)));
    }

    // ── Device registration / security ───────────────────────────────────────

    [Authorize(Roles = "Driver")]
    [HttpPost("devices/register")]
    public async Task<IActionResult> RegisterDevice([FromBody] RegisterDriverDeviceRequest request)
        => Ok(await Mediator.Send(new RegisterDriverDeviceCommand(request)));

    // ── App Version ───────────────────────────────────────────────────────────

    [AllowAnonymous]
    [HttpGet("app-version")]
    public IActionResult GetAppVersion()
        => Ok(new { MinVersion = "1.0.0", LatestVersion = "1.0.0", ForceUpdate = false });
}

public record DriverCollectPaymentRequest(
    decimal AmountReceived,
    string PaymentMethod,
    string? ReferenceNumber,
    string? Notes);

public record SetDriverStatusRequest(string Status);
