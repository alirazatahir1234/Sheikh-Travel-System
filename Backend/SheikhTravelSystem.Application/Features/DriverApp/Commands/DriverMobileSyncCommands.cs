using System.Text.Json;
using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.DriverApp.DTOs;
using SheikhTravelSystem.Application.Features.DriverApp.Services;
using SheikhTravelSystem.Application.Features.FuelLogs.DTOs;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.DriverApp.Commands;

public record DriverSyncOperationItemDto(
    string ClientOpId,
    string OpType,
    JsonElement Payload);

public record DriverSyncBatchCommand(IReadOnlyList<DriverSyncOperationItemDto> Operations)
    : IRequest<ApiResponse<DriverSyncBatchResultDto>>;

public class DriverSyncBatchCommandValidator : AbstractValidator<DriverSyncBatchCommand>
{
    public DriverSyncBatchCommandValidator()
    {
        RuleFor(x => x.Operations).NotNull();
        RuleForEach(x => x.Operations).ChildRules(op =>
        {
            op.RuleFor(o => o.ClientOpId).NotEmpty().MaximumLength(64);
            op.RuleFor(o => o.OpType).NotEmpty().MaximumLength(64);
        });
        RuleFor(x => x.Operations.Count).LessThanOrEqualTo(50);
    }
}

public class DriverSyncBatchCommandHandler(
    IDriverMobileSyncRepository syncRepository,
    ICurrentUserService currentUser,
    ITenantContext tenantContext,
    IMediator mediator)
    : IRequestHandler<DriverSyncBatchCommand, ApiResponse<DriverSyncBatchResultDto>>
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async Task<ApiResponse<DriverSyncBatchResultDto>> Handle(
        DriverSyncBatchCommand request, CancellationToken cancellationToken)
    {
        var driverId = currentUser.DriverId;
        if (!driverId.HasValue)
            return ApiResponse<DriverSyncBatchResultDto>.FailResponse("Driver identity required.");

        var tenantId = tenantContext.GetRequiredTenantId();
        var results = new List<DriverSyncOperationResultDto>();

        foreach (var op in request.Operations)
        {
            var opType = op.OpType.Trim();
            var clientOpId = op.ClientOpId.Trim();

            var (_, replay) = await DriverMobileIdempotencyHelper.TryReplayAsync<JsonElement>(
                syncRepository, tenantId, clientOpId, cancellationToken);
            if (replay != null)
            {
                results.Add(new DriverSyncOperationResultDto(
                    clientOpId, opType, true, replay.Success, replay.Message, replay.Data));
                continue;
            }

            try
            {
                var (success, message, data, httpStatus) = await DispatchOneAsync(
                    opType, op.Payload, clientOpId, cancellationToken);

                var wrapped = success
                    ? ApiResponse<JsonElement>.SuccessResponse(
                        data ?? default, message ?? "OK")
                    : ApiResponse<JsonElement>.FailResponse(message ?? "Operation failed.");

                await DriverMobileIdempotencyHelper.PersistAsync(
                    syncRepository, tenantId, driverId.Value, clientOpId,
                    opType, wrapped, httpStatus, cancellationToken);

                results.Add(new DriverSyncOperationResultDto(
                    clientOpId, opType, true, wrapped.Success, wrapped.Message, wrapped.Data));
            }
            catch (Exception ex)
            {
                results.Add(new DriverSyncOperationResultDto(
                    clientOpId, opType, false, false, ex.Message, null));
            }
        }

        return ApiResponse<DriverSyncBatchResultDto>.SuccessResponse(
            new DriverSyncBatchResultDto(results),
            $"Processed {results.Count} operation(s).");
    }

    private async Task<(bool Success, string? Message, JsonElement? Data, int HttpStatus)> DispatchOneAsync(
        string opType, JsonElement payload, string clientOpId, CancellationToken ct)
    {
        switch (opType)
        {
            case "attendanceCheckIn":
            {
                var p = payload.Deserialize<AttendancePayload>(JsonOpts)!;
                var r = await mediator.Send(new DriverCheckInCommand(p.Latitude, p.Longitude), ct);
                return (r.Success, r.Message, JsonSerializer.SerializeToElement(r.Data, JsonOpts), r.Success ? 200 : 400);
            }
            case "attendanceCheckOut":
            {
                var p = payload.Deserialize<AttendancePayload>(JsonOpts)!;
                var r = await mediator.Send(new DriverCheckOutCommand(p.Latitude, p.Longitude), ct);
                return (r.Success, r.Message, JsonSerializer.SerializeToElement(r.Data, JsonOpts), r.Success ? 200 : 400);
            }
            case "tripAdvance":
            {
                var p = payload.Deserialize<TripAdvancePayload>(JsonOpts)!;
                var action = p.Action?.Trim() switch
                {
                    "Accept" => DriverTripAction.Accept,
                    "Start" => DriverTripAction.Accept,
                    "Arrived" => DriverTripAction.Arrived,
                    "Onboard" => DriverTripAction.Onboard,
                    "Complete" => DriverTripAction.Complete,
                    "Reject" => DriverTripAction.Reject,
                    _ => throw new InvalidOperationException($"Unknown trip action {p.Action}"),
                };
                var r = await mediator.Send(new DriverAdvanceTripCommand(p.TripId, action, p.Reason), ct);
                return (r.Success, r.Message, JsonSerializer.SerializeToElement(r.Data, JsonOpts), r.Success ? 200 : 409);
            }
            case "paymentCollect":
            {
                var p = payload.Deserialize<PaymentCollectPayload>(JsonOpts)!;
                var r = await mediator.Send(new DriverCollectPaymentCommand(
                    p.TripId, p.AmountReceived, p.PaymentMethod, p.ReferenceNumber, p.Notes), ct);
                return (r.Success, r.Message, JsonSerializer.SerializeToElement(r.Data, JsonOpts), r.Success ? 200 : 400);
            }
            case "voiceCommand":
            {
                var p = payload.Deserialize<VoiceCommandPayload>(JsonOpts)!;
                var r = await mediator.Send(new SubmitDriverVoiceCommand(
                    p.Transcript, p.LanguageCode, p.Intent, clientOpId,
                    p.Latitude, p.Longitude), ct);
                return (r.Success, r.Message,
                    r.Data != null ? JsonSerializer.SerializeToElement(r.Data, JsonOpts) : null,
                    r.Success ? 200 : 400);
            }
            case "fuelSubmit":
                return (false, "fuelSubmit batch sync requires multipart — use direct POST /fuel-receipts with idempotency headers.", null, 501);
            case "inspectionSubmit":
                return (false, "inspectionSubmit batch sync requires multipart — use direct POST with idempotency headers.", null, 501);
            case "documentUpload":
                return (false, "documentUpload batch sync requires multipart — use direct POST with idempotency headers.", null, 501);
            default:
                return (false, $"Unsupported opType '{opType}'.", null, 400);
        }
    }

    private sealed record AttendancePayload(double? Latitude, double? Longitude);
    private sealed record TripAdvancePayload(int TripId, string? Action, string? Reason);
    private sealed record PaymentCollectPayload(
        int TripId,
        decimal AmountReceived,
        string PaymentMethod,
        string? ReferenceNumber,
        string? Notes);
    private sealed record VoiceCommandPayload(
        string Transcript,
        string LanguageCode,
        string? Intent,
        double? Latitude,
        double? Longitude);
}
