using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Notifications;
using SheikhTravelSystem.Application.Features.Trips.DTOs;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.Trips.Commands;

public record CloneTripCommand(int Id) : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => "Create";
    public string AuditEntityName => "Trip";
    public int? AuditEntityId => null;
}

public class CloneTripCommandHandler(ITripEnhancementsRepository repo)
    : IRequestHandler<CloneTripCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(CloneTripCommand request, CancellationToken cancellationToken)
    {
        var id = await repo.CloneTripAsync(request.Id, cancellationToken);
        return ApiResponse<int>.SuccessResponse(id, "Trip cloned.");
    }
}

public record BulkImportTripsCommand(string CsvContent) : IRequest<ApiResponse<BulkTripImportResultDto>>, IAuditableCommand
{
    public string AuditAction => "Create";
    public string AuditEntityName => "Trip";
    public int? AuditEntityId => null;
}

public class BulkImportTripsCommandHandler(ITripEnhancementsRepository repo)
    : IRequestHandler<BulkImportTripsCommand, ApiResponse<BulkTripImportResultDto>>
{
    public async Task<ApiResponse<BulkTripImportResultDto>> Handle(
        BulkImportTripsCommand request, CancellationToken cancellationToken)
    {
        var rows = ParseBulkImportCsv(request.CsvContent);
        if (rows.Count == 0)
            return ApiResponse<BulkTripImportResultDto>.FailResponse("No data rows found in CSV.");

        var result = await repo.ImportFromRowsAsync(rows, cancellationToken);
        var dto = new BulkTripImportResultDto(result.Created, result.Failed, result.Errors);
        return ApiResponse<BulkTripImportResultDto>.SuccessResponse(
            dto,
            $"Imported {result.Created} trip(s), {result.Failed} failed.");
    }

    public static List<BulkTripImportRowDto> ParseBulkImportCsv(string content)
    {
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length < 2)
            return [];

        var headers = SplitCsvLine(lines[0]).Select(h => h.Trim()).ToArray();
        int Idx(string name) => Array.FindIndex(headers, h => h.Equals(name, StringComparison.OrdinalIgnoreCase));

        var iName = Idx("tripName"); if (iName < 0) iName = Idx("name");
        var iCustomer = Idx("customerId");
        var iTripDate = Idx("tripDate");
        var iStart = Idx("plannedStart");
        if (iName < 0 || iCustomer < 0 || iTripDate < 0 || iStart < 0)
            return [];

        var rows = new List<BulkTripImportRowDto>();
        for (var li = 1; li < lines.Length; li++)
        {
            var cols = SplitCsvLine(lines[li]);
            if (cols.Count <= Math.Max(iStart, iCustomer))
                continue;
            if (!int.TryParse(cols[iCustomer], out var customerId))
                continue;
            if (!DateTime.TryParse(cols[iTripDate], out var tripDate))
                continue;
            if (!DateTime.TryParse(cols[iStart], out var plannedStart))
                continue;

            rows.Add(new BulkTripImportRowDto(
                cols[iName],
                customerId,
                Col(cols, Idx("tripType")) ?? "Transfer",
                Col(cols, Idx("priority")) ?? "Normal",
                Col(cols, Idx("pickupAddress")),
                Col(cols, Idx("destinationAddress")),
                tripDate,
                plannedStart,
                ParseInt(Col(cols, Idx("driverId"))),
                ParseInt(Col(cols, Idx("vehicleId"))),
                Col(cols, Idx("specialCargoFlags"))));
        }

        return rows;

        static string? Col(List<string> c, int i) => i >= 0 && i < c.Count ? c[i].Trim() : null;
        static int? ParseInt(string? s) => int.TryParse(s, out var v) ? v : null;
    }

    public static List<string> SplitCsvLine(string line)
    {
        var result = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }
            if (ch == ',' && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
                continue;
            }
            current.Append(ch);
        }
        result.Add(current.ToString());
        return result;
    }
}

public record SaveTripTemplateCommand(UpsertTripTemplateDto Template) : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => "Update";
    public string AuditEntityName => "TripTemplate";
    public int? AuditEntityId => Template.Id;
}

public class SaveTripTemplateCommandHandler(ITripEnhancementsRepository repo)
    : IRequestHandler<SaveTripTemplateCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(SaveTripTemplateCommand request, CancellationToken cancellationToken)
    {
        var id = await repo.SaveTemplateAsync(request.Template, cancellationToken);
        return ApiResponse<int>.SuccessResponse(id, "Template saved.");
    }
}

public record DeleteTripTemplateCommand(int Id) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Delete";
    public string AuditEntityName => "TripTemplate";
    public int? AuditEntityId => Id;
}

public class DeleteTripTemplateCommandHandler(ITripEnhancementsRepository repo)
    : IRequestHandler<DeleteTripTemplateCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(DeleteTripTemplateCommand request, CancellationToken cancellationToken)
    {
        await repo.SoftDeleteTemplateAsync(request.Id, cancellationToken);
        return ApiResponse<bool>.SuccessResponse(true, "Template deleted.");
    }
}

public record SpawnTripFromTemplateCommand(int TemplateId, DateTime? OccurrenceDate) : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => "Create";
    public string AuditEntityName => "Trip";
    public int? AuditEntityId => null;
}

public class SpawnTripFromTemplateCommandHandler(ITripEnhancementsRepository repo)
    : IRequestHandler<SpawnTripFromTemplateCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(SpawnTripFromTemplateCommand request, CancellationToken cancellationToken)
    {
        var id = await repo.SpawnFromTemplateAsync(request.TemplateId, request.OccurrenceDate, cancellationToken);
        return ApiResponse<int>.SuccessResponse(id, "Trip created from template.");
    }
}

public record SpawnDueRecurringTripsCommand : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => "Create";
    public string AuditEntityName => "Trip";
    public int? AuditEntityId => null;
}

public class SpawnDueRecurringTripsCommandHandler(ITripEnhancementsRepository repo)
    : IRequestHandler<SpawnDueRecurringTripsCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(SpawnDueRecurringTripsCommand request, CancellationToken cancellationToken)
    {
        var count = await repo.SpawnDueRecurringTemplatesAsync(cancellationToken);
        return ApiResponse<int>.SuccessResponse(count, count > 0 ? $"Spawned {count} recurring trip(s)." : "No templates due.");
    }
}

public record SendTripDelayedEtaCommand(int TripId, SendDelayedEtaRequestDto Body) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Update";
    public string AuditEntityName => "Trip";
    public int? AuditEntityId => TripId;
}

public class SendTripDelayedEtaCommandHandler(
    ITripRepository tripRepository,
    ITripEnhancementsRepository enhancements,
    INotificationDecisionEngine notifications,
    ITenantContext tenantContext)
    : IRequestHandler<SendTripDelayedEtaCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(SendTripDelayedEtaCommand request, CancellationToken cancellationToken)
    {
        var load = await tripRepository.GetDetailAsync(request.TripId, cancellationToken);
        var trip = load.Detail;
        if (trip.Status != TripStatus.Delayed && trip.Status is not (TripStatus.Started or TripStatus.Enroute or TripStatus.AtPickup))
            return ApiResponse<bool>.FailResponse("Trip is not in a delay-eligible state.");

        var summary = await tripRepository.GetRouteSummaryAsync(request.TripId, cancellationToken);
        var etaMin = summary.EtaMinutes;
        var msg = request.Body.CustomMessage?.Trim()
                  ?? $"Your trip {trip.TripNumber} is delayed. Updated ETA: {(etaMin.HasValue ? $"{etaMin} minutes" : "we will update you shortly")}.";

        var channels = new List<string> { NotificationChannels.InApp, NotificationChannels.Email };
        if (string.Equals(request.Body.Channel, "whatsapp", StringComparison.OrdinalIgnoreCase))
            channels.Add(NotificationChannels.WhatsApp);

        var tenantId = tenantContext.GetRequiredTenantId();
        await notifications.DispatchIfAllowedAsync(new NotificationDecisionRequest(
            "trip_delayed_eta_client",
            $"Updated ETA — {trip.TripNumber}",
            msg,
            NotificationType.TripDelayed,
            ReferenceId: trip.Id,
            TenantId: tenantId,
            SuggestedPriority: 2,
            RequestedChannels: channels), cancellationToken);

        await enhancements.LogDelayedEtaSentAsync(request.TripId, request.Body.Channel ?? "email", msg, cancellationToken);
        return ApiResponse<bool>.SuccessResponse(true, "Delayed ETA notification queued.");
    }
}

public record MoveDispatchBoardTripCommand(DispatchBoardMoveDto Body) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "UpdateStatus";
    public string AuditEntityName => "Trip";
    public int? AuditEntityId => Body.TripId;
}

public class MoveDispatchBoardTripCommandHandler(ITripRepository tripRepository)
    : IRequestHandler<MoveDispatchBoardTripCommand, ApiResponse<bool>>
{
    private static readonly Dictionary<string, TripStatus> ColumnStatus = new(StringComparer.OrdinalIgnoreCase)
    {
        ["queued"] = TripStatus.Scheduled,
        ["started"] = TripStatus.Started,
        ["enroute"] = TripStatus.Enroute,
        ["delayed"] = TripStatus.Delayed
    };

    public async Task<ApiResponse<bool>> Handle(MoveDispatchBoardTripCommand request, CancellationToken cancellationToken)
    {
        if (!ColumnStatus.TryGetValue(request.Body.TargetColumnKey, out var status))
            return ApiResponse<bool>.FailResponse("Unknown board column.");

        var result = await tripRepository.UpdateStatusAsync(
            request.Body.TripId, status, "Moved on dispatch board", null, cancellationToken);
        return result.Success
            ? ApiResponse<bool>.SuccessResponse(true, "Trip moved.")
            : ApiResponse<bool>.FailResponse(result.ErrorMessage ?? "Move failed.");
    }
}
