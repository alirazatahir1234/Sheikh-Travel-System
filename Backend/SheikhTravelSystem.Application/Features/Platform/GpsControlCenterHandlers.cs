using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.GpsTracking.Commands;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;

namespace SheikhTravelSystem.Application.Features.Platform;

public record GetGpsControlDashboardQuery : IRequest<ApiResponse<GpsControlDashboardDto>>;

public record GetGpsManufacturersQuery : IRequest<ApiResponse<IReadOnlyList<GpsManufacturerDto>>>;

public record UpsertGpsManufacturerCommand(GpsManufacturerDto Manufacturer)
    : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => Manufacturer.Id > 0 ? "Update" : "Create";
    public string AuditEntityName => "TrackerBrand";
    public int? AuditEntityId => Manufacturer.Id > 0 ? Manufacturer.Id : null;
}

public record GetGpsTrackerModelsQuery(int? BrandId = null)
    : IRequest<ApiResponse<IReadOnlyList<GpsTrackerModelDto>>>;

public record UpsertGpsTrackerModelCommand(GpsTrackerModelDto Model)
    : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => Model.Id > 0 ? "Update" : "Create";
    public string AuditEntityName => "TrackerModel";
    public int? AuditEntityId => Model.Id > 0 ? Model.Id : null;
}

public record GetGpsCapabilitiesQuery : IRequest<ApiResponse<IReadOnlyList<GpsCapabilityDto>>>;

public record GetGpsModelCapabilitiesQuery(int ModelId)
    : IRequest<ApiResponse<IReadOnlyList<string>>>;

public record SetGpsModelCapabilityCommand(int ModelId, string CapabilityKey, bool Enabled)
    : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Update";
    public string AuditEntityName => "TrackerModelCapability";
    public int? AuditEntityId => ModelId;
}

public record GetGpsCommandDefinitionsQuery
    : IRequest<ApiResponse<IReadOnlyList<GpsCommandDefinitionDto>>>;

public record GetGpsCommandParametersQuery(string? CommandKey = null)
    : IRequest<ApiResponse<IReadOnlyList<GpsCommandParameterDto>>>;

public record GetGpsCommandTemplatesQuery(int? ModelId = null)
    : IRequest<ApiResponse<IReadOnlyList<GpsCommandTemplateDto>>>;

public record UpsertGpsCommandTemplateCommand(GpsCommandTemplateDto Template)
    : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => Template.Id > 0 ? "Update" : "Create";
    public string AuditEntityName => "GpsCommandTemplate";
    public int? AuditEntityId => Template.Id > 0 ? Template.Id : null;
}

public record TranslateGpsCommandQuery(GpsTranslateRequest Request)
    : IRequest<ApiResponse<GpsTranslateResult>>;

public record SimulateGpsCommandCommand(GpsTranslateRequest Request)
    : IRequest<ApiResponse<GpsSimulateResult>>;

public record GpsSimulateResult(
    GpsTranslateResult Translation,
    GpsTransportSendResult Transport,
    IReadOnlyDictionary<string, string>? Parsed);

public record ApproveGpsDeviceCommandCommand(int CommandId, bool Approve, string? Note = null)
    : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => Approve ? "Approve" : "Reject";
    public string AuditEntityName => "GpsDeviceCommand";
    public int? AuditEntityId => CommandId;
}

public record BulkExecuteGpsCommandCommand(
    IReadOnlyList<int> DeviceIds,
    string CommandKey,
    IReadOnlyDictionary<string, string>? Parameters,
    string? Reason)
    : IRequest<ApiResponse<BulkExecuteGpsResult>>, IAuditableCommand
{
    public string AuditAction => "BulkExecute";
    public string AuditEntityName => "GpsDeviceCommand";
    public int? AuditEntityId => null;
}

public record BulkExecuteGpsResult(int Enqueued, int Failed, IReadOnlyList<string> Errors);

public class GetGpsControlDashboardQueryHandler(IGpsControlCenterService service)
    : IRequestHandler<GetGpsControlDashboardQuery, ApiResponse<GpsControlDashboardDto>>
{
    public async Task<ApiResponse<GpsControlDashboardDto>> Handle(
        GetGpsControlDashboardQuery request, CancellationToken cancellationToken)
        => ApiResponse<GpsControlDashboardDto>.SuccessResponse(
            await service.GetDashboardAsync(cancellationToken));
}

public class GetGpsManufacturersQueryHandler(IGpsControlCenterService service)
    : IRequestHandler<GetGpsManufacturersQuery, ApiResponse<IReadOnlyList<GpsManufacturerDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<GpsManufacturerDto>>> Handle(
        GetGpsManufacturersQuery request, CancellationToken cancellationToken)
        => ApiResponse<IReadOnlyList<GpsManufacturerDto>>.SuccessResponse(
            await service.GetManufacturersAsync(cancellationToken));
}

public class UpsertGpsManufacturerCommandHandler(IGpsControlCenterService service)
    : IRequestHandler<UpsertGpsManufacturerCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(
        UpsertGpsManufacturerCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Manufacturer.Name))
            return ApiResponse<int>.FailResponse("Manufacturer name is required.");
        var id = await service.UpsertManufacturerAsync(request.Manufacturer, cancellationToken);
        return ApiResponse<int>.SuccessResponse(id, "Manufacturer saved.");
    }
}

public class GetGpsTrackerModelsQueryHandler(IGpsControlCenterService service)
    : IRequestHandler<GetGpsTrackerModelsQuery, ApiResponse<IReadOnlyList<GpsTrackerModelDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<GpsTrackerModelDto>>> Handle(
        GetGpsTrackerModelsQuery request, CancellationToken cancellationToken)
        => ApiResponse<IReadOnlyList<GpsTrackerModelDto>>.SuccessResponse(
            await service.GetModelsAsync(request.BrandId, cancellationToken));
}

public class UpsertGpsTrackerModelCommandHandler(IGpsControlCenterService service)
    : IRequestHandler<UpsertGpsTrackerModelCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(
        UpsertGpsTrackerModelCommand request, CancellationToken cancellationToken)
    {
        if (request.Model.TrackerBrandId <= 0 || string.IsNullOrWhiteSpace(request.Model.Name))
            return ApiResponse<int>.FailResponse("Brand and model name are required.");
        if (string.IsNullOrWhiteSpace(request.Model.Protocol))
            return ApiResponse<int>.FailResponse("Protocol is required.");
        var id = await service.UpsertModelAsync(request.Model, cancellationToken);
        return ApiResponse<int>.SuccessResponse(id, "Model saved.");
    }
}

public class GetGpsCapabilitiesQueryHandler(IGpsControlCenterService service)
    : IRequestHandler<GetGpsCapabilitiesQuery, ApiResponse<IReadOnlyList<GpsCapabilityDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<GpsCapabilityDto>>> Handle(
        GetGpsCapabilitiesQuery request, CancellationToken cancellationToken)
        => ApiResponse<IReadOnlyList<GpsCapabilityDto>>.SuccessResponse(
            await service.GetCapabilitiesAsync(cancellationToken));
}

public class GetGpsModelCapabilitiesQueryHandler(IGpsControlCenterService service)
    : IRequestHandler<GetGpsModelCapabilitiesQuery, ApiResponse<IReadOnlyList<string>>>
{
    public async Task<ApiResponse<IReadOnlyList<string>>> Handle(
        GetGpsModelCapabilitiesQuery request, CancellationToken cancellationToken)
        => ApiResponse<IReadOnlyList<string>>.SuccessResponse(
            await service.GetModelCapabilityKeysAsync(request.ModelId, cancellationToken));
}

public class SetGpsModelCapabilityCommandHandler(IGpsControlCenterService service)
    : IRequestHandler<SetGpsModelCapabilityCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(
        SetGpsModelCapabilityCommand request, CancellationToken cancellationToken)
    {
        await service.SetModelCapabilityAsync(
            request.ModelId, request.CapabilityKey, request.Enabled, cancellationToken);
        return ApiResponse<bool>.SuccessResponse(true, "Capability updated.");
    }
}

public class GetGpsCommandDefinitionsQueryHandler(IGpsControlCenterService service)
    : IRequestHandler<GetGpsCommandDefinitionsQuery, ApiResponse<IReadOnlyList<GpsCommandDefinitionDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<GpsCommandDefinitionDto>>> Handle(
        GetGpsCommandDefinitionsQuery request, CancellationToken cancellationToken)
        => ApiResponse<IReadOnlyList<GpsCommandDefinitionDto>>.SuccessResponse(
            await service.GetCommandDefinitionsAsync(cancellationToken));
}

public class GetGpsCommandParametersQueryHandler(IGpsControlCenterService service)
    : IRequestHandler<GetGpsCommandParametersQuery, ApiResponse<IReadOnlyList<GpsCommandParameterDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<GpsCommandParameterDto>>> Handle(
        GetGpsCommandParametersQuery request, CancellationToken cancellationToken)
        => ApiResponse<IReadOnlyList<GpsCommandParameterDto>>.SuccessResponse(
            await service.GetCommandParametersAsync(request.CommandKey, cancellationToken));
}

public class GetGpsCommandTemplatesQueryHandler(IGpsControlCenterService service)
    : IRequestHandler<GetGpsCommandTemplatesQuery, ApiResponse<IReadOnlyList<GpsCommandTemplateDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<GpsCommandTemplateDto>>> Handle(
        GetGpsCommandTemplatesQuery request, CancellationToken cancellationToken)
        => ApiResponse<IReadOnlyList<GpsCommandTemplateDto>>.SuccessResponse(
            await service.GetTemplatesAsync(request.ModelId, cancellationToken));
}

public class UpsertGpsCommandTemplateCommandHandler(IGpsControlCenterService service)
    : IRequestHandler<UpsertGpsCommandTemplateCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(
        UpsertGpsCommandTemplateCommand request, CancellationToken cancellationToken)
    {
        if (request.Template.TrackerModelId <= 0 || string.IsNullOrWhiteSpace(request.Template.CommandKey))
            return ApiResponse<int>.FailResponse("Model and command key are required.");
        if (string.IsNullOrWhiteSpace(request.Template.PayloadTemplate))
            return ApiResponse<int>.FailResponse("Payload template is required.");
        var id = await service.UpsertTemplateAsync(request.Template, cancellationToken);
        return ApiResponse<int>.SuccessResponse(id, "Template saved.");
    }
}

public class TranslateGpsCommandQueryHandler(IGpsCommandTranslator translator)
    : IRequestHandler<TranslateGpsCommandQuery, ApiResponse<GpsTranslateResult>>
{
    public async Task<ApiResponse<GpsTranslateResult>> Handle(
        TranslateGpsCommandQuery request, CancellationToken cancellationToken)
    {
        var result = await translator.TranslateAsync(request.Request, cancellationToken);
        return result.Success
            ? ApiResponse<GpsTranslateResult>.SuccessResponse(result)
            : ApiResponse<GpsTranslateResult>.FailResponse(result.Error ?? "Translation failed.");
    }
}

public class SimulateGpsCommandCommandHandler(
    IGpsCommandTranslator translator,
    IGpsTransportRouter transportRouter,
    IGpsCommandResultParserRegistry parserRegistry)
    : IRequestHandler<SimulateGpsCommandCommand, ApiResponse<GpsSimulateResult>>
{
    public async Task<ApiResponse<GpsSimulateResult>> Handle(
        SimulateGpsCommandCommand request, CancellationToken cancellationToken)
    {
        var translateReq = request.Request with { UseSimulator = true };
        var translation = await translator.TranslateAsync(translateReq, cancellationToken);
        if (!translation.Success)
            return ApiResponse<GpsSimulateResult>.FailResponse(translation.Error ?? "Translation failed.");

        var send = await transportRouter.SendAsync(new GpsTransportSendRequest(
            "Simulator",
            null,
            translation.TraccarType,
            translation.RenderedPayload,
            null), cancellationToken);

        var parsed = send.ResponseText is null
            ? null
            : parserRegistry.Parse(translation.ParserKey, send.ResponseText);

        return ApiResponse<GpsSimulateResult>.SuccessResponse(
            new GpsSimulateResult(translation, send, parsed));
    }
}

public class ApproveGpsDeviceCommandCommandHandler(
    IDbConnectionFactory dbFactory,
    ICurrentUserService currentUser)
    : IRequestHandler<ApproveGpsDeviceCommandCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(
        ApproveGpsDeviceCommandCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var status = request.Approve ? "pending" : "cancelled";
        var approval = request.Approve ? "Approved" : "Rejected";
        var rows = await connection.ExecuteAsync(new Dapper.CommandDefinition(
            """
            UPDATE GpsDeviceCommands
            SET Status = @Status,
                ApprovalStatus = @Approval,
                ApprovedBy = @By,
                ApprovedAt = SYSUTCDATETIME(),
                UpdatedAt = SYSUTCDATETIME(),
                ErrorMessage = CASE WHEN @Approve = 0 THEN COALESCE(@Note, N'Rejected') ELSE ErrorMessage END
            WHERE Id = @Id AND IsDeleted = 0 AND Status = N'PendingApproval'
            """,
            new
            {
                Id = request.CommandId,
                Status = status,
                Approval = approval,
                By = currentUser.UserId?.ToString(),
                Approve = request.Approve,
                request.Note
            },
            cancellationToken: cancellationToken));

        return rows > 0
            ? ApiResponse<bool>.SuccessResponse(true, approval)
            : ApiResponse<bool>.FailResponse("Command not found or not awaiting approval.");
    }
}

public class BulkExecuteGpsCommandCommandHandler(IMediator mediator)
    : IRequestHandler<BulkExecuteGpsCommandCommand, ApiResponse<BulkExecuteGpsResult>>
{
    public async Task<ApiResponse<BulkExecuteGpsResult>> Handle(
        BulkExecuteGpsCommandCommand request, CancellationToken cancellationToken)
    {
        if (request.DeviceIds.Count == 0)
            return ApiResponse<BulkExecuteGpsResult>.FailResponse("No devices selected.");

        var errors = new List<string>();
        var enqueued = 0;
        foreach (var deviceId in request.DeviceIds.Distinct())
        {
            var attrs = request.Parameters is null
                ? null
                : request.Parameters.ToDictionary(kv => kv.Key, kv => (object)kv.Value);

            var result = await mediator.Send(new SendDeviceCommandCommand(
                new SendDeviceCommandDto(
                    deviceId,
                    request.CommandKey,
                    request.Reason,
                    attrs)), cancellationToken);

            if (result.Success) enqueued++;
            else errors.Add($"Device {deviceId}: {result.Message}");
        }

        return ApiResponse<BulkExecuteGpsResult>.SuccessResponse(
            new BulkExecuteGpsResult(enqueued, errors.Count, errors));
    }
}
