using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
namespace SheikhTravelSystem.Application.Features.FatigueDetection;

public record GetFatigueEventsQuery(string? Status = null, int Limit = 50)
    : IRequest<ApiResponse<List<FatigueEventListItemDto>>>;

public record GetFatigueEventsPlatformQuery(int? TenantId = null, string? Status = null, int Limit = 100)
    : IRequest<ApiResponse<List<FatigueEventListItemDto>>>;

public record GetFatigueSettingsQuery()
    : IRequest<ApiResponse<FatigueTenantSettingsDto>>;

public record UpsertFatigueSettingsCommand(UpsertFatigueTenantSettingsRequest Body)
    : IRequest<ApiResponse<FatigueTenantSettingsDto>>;

public record IngestFatigueSignalCommand(IngestFatigueSignalRequest Body)
    : IRequest<ApiResponse<IngestFatigueSignalResultDto>>;

public record ResolveFatigueEventCommand(int Id)
    : IRequest<ApiResponse<bool>>;

public record DriverAckFatigueCommand(int EventId)
    : IRequest<ApiResponse<DriverFatigueStatusDto>>;

public record GetDriverFatigueStatusQuery(int EventId)
    : IRequest<ApiResponse<DriverFatigueStatusDto>>;

public record GetDriverActiveFatiguePromptQuery()
    : IRequest<ApiResponse<DriverFatigueActivePromptDto?>>;

public class IngestFatigueSignalValidator : AbstractValidator<IngestFatigueSignalCommand>
{
    public IngestFatigueSignalValidator()
    {
        RuleFor(x => x.Body.SignalType).NotEmpty().MaximumLength(40);
        RuleFor(x => x.Body.Source).MaximumLength(40);
    }
}

public class UpsertFatigueSettingsValidator : AbstractValidator<UpsertFatigueSettingsCommand>
{
    public UpsertFatigueSettingsValidator()
    {
        RuleFor(x => x.Body.AckWindowSeconds).InclusiveBetween(10, 300);
        RuleFor(x => x.Body.StageIntervalSeconds).InclusiveBetween(5, 120);
    }
}

public class GetFatigueEventsQueryHandler(IFatigueDetectionRepository repository, ITenantContext tenantContext)
    : IRequestHandler<GetFatigueEventsQuery, ApiResponse<List<FatigueEventListItemDto>>>
{
    public async Task<ApiResponse<List<FatigueEventListItemDto>>> Handle(
        GetFatigueEventsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var rows = await repository.ListEventsAsync(tenantId, request.Status, request.Limit, cancellationToken);
        return ApiResponse<List<FatigueEventListItemDto>>.SuccessResponse(rows.ToList());
    }
}

public class GetFatigueEventsPlatformQueryHandler(
    IFatigueDetectionRepository repository,
    IPlatformScope platformScope)
    : IRequestHandler<GetFatigueEventsPlatformQuery, ApiResponse<List<FatigueEventListItemDto>>>
{
    public async Task<ApiResponse<List<FatigueEventListItemDto>>> Handle(
        GetFatigueEventsPlatformQuery request, CancellationToken cancellationToken)
    {
        if (!platformScope.HasPermission(PlatformPermissions.TenantsDataRead))
            return ApiResponse<List<FatigueEventListItemDto>>.FailResponse("Platform data-read access required.");

        var rows = await repository.ListEventsPlatformAsync(
            request.TenantId, request.Status, request.Limit, cancellationToken);
        return ApiResponse<List<FatigueEventListItemDto>>.SuccessResponse(rows.ToList());
    }
}

public class GetFatigueSettingsQueryHandler(IFatigueDetectionRepository repository, ITenantContext tenantContext)
    : IRequestHandler<GetFatigueSettingsQuery, ApiResponse<FatigueTenantSettingsDto>>
{
    public async Task<ApiResponse<FatigueTenantSettingsDto>> Handle(
        GetFatigueSettingsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var settings = await repository.GetOrCreateSettingsAsync(tenantId, cancellationToken);
        return ApiResponse<FatigueTenantSettingsDto>.SuccessResponse(settings);
    }
}

public class UpsertFatigueSettingsCommandHandler(IFatigueDetectionRepository repository, ITenantContext tenantContext)
    : IRequestHandler<UpsertFatigueSettingsCommand, ApiResponse<FatigueTenantSettingsDto>>
{
    public async Task<ApiResponse<FatigueTenantSettingsDto>> Handle(
        UpsertFatigueSettingsCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        await repository.UpsertSettingsAsync(tenantId, request.Body, cancellationToken);
        var settings = await repository.GetOrCreateSettingsAsync(tenantId, cancellationToken);
        return ApiResponse<FatigueTenantSettingsDto>.SuccessResponse(settings, "Fatigue settings saved.");
    }
}

public class IngestFatigueSignalCommandHandler(
    IFatigueAlertOrchestrator orchestrator,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<IngestFatigueSignalCommand, ApiResponse<IngestFatigueSignalResultDto>>
{
    public async Task<ApiResponse<IngestFatigueSignalResultDto>> Handle(
        IngestFatigueSignalCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var driverId = request.Body.DriverId ?? currentUser.DriverId;
        if (!driverId.HasValue)
            return ApiResponse<IngestFatigueSignalResultDto>.FailResponse("DriverId is required.");

        return await orchestrator.IngestAsync(tenantId, driverId.Value, request.Body, cancellationToken);
    }
}

public class ResolveFatigueEventCommandHandler(
    IFatigueDetectionRepository repository,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<ResolveFatigueEventCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(ResolveFatigueEventCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var actor = currentUser.UserId?.ToString() ?? "erp";
        await repository.ResolveEventAsync(tenantId, request.Id, actor, cancellationToken);
        return ApiResponse<bool>.SuccessResponse(true, "Fatigue event resolved.");
    }
}

public class DriverAckFatigueCommandHandler(
    IFatigueDetectionRepository repository,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<DriverAckFatigueCommand, ApiResponse<DriverFatigueStatusDto>>
{
    public async Task<ApiResponse<DriverFatigueStatusDto>> Handle(
        DriverAckFatigueCommand request, CancellationToken cancellationToken)
    {
        var driverId = currentUser.DriverId;
        if (!driverId.HasValue)
            return ApiResponse<DriverFatigueStatusDto>.FailResponse("Driver identity required.");

        var tenantId = tenantContext.GetRequiredTenantId();
        await repository.AcknowledgeEventAsync(tenantId, driverId.Value, request.EventId, cancellationToken);
        var row = await repository.GetDriverFatigueStatusAsync(
            tenantId, driverId.Value, request.EventId, cancellationToken);
        if (row is null)
            return ApiResponse<DriverFatigueStatusDto>.FailResponse("Fatigue event not found.");
        return ApiResponse<DriverFatigueStatusDto>.SuccessResponse(row, "Acknowledged — stay alert.");
    }
}

public class GetDriverFatigueStatusQueryHandler(
    IFatigueDetectionRepository repository,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<GetDriverFatigueStatusQuery, ApiResponse<DriverFatigueStatusDto>>
{
    public async Task<ApiResponse<DriverFatigueStatusDto>> Handle(
        GetDriverFatigueStatusQuery request, CancellationToken cancellationToken)
    {
        var driverId = currentUser.DriverId;
        if (!driverId.HasValue)
            return ApiResponse<DriverFatigueStatusDto>.FailResponse("Driver identity required.");

        var tenantId = tenantContext.GetRequiredTenantId();
        var row = await repository.GetDriverFatigueStatusAsync(
            tenantId, driverId.Value, request.EventId, cancellationToken);
        if (row is null)
            return ApiResponse<DriverFatigueStatusDto>.FailResponse("Fatigue event not found.");
        return ApiResponse<DriverFatigueStatusDto>.SuccessResponse(row);
    }
}

public class GetDriverActiveFatiguePromptQueryHandler(
    IFatigueDetectionRepository repository,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<GetDriverActiveFatiguePromptQuery, ApiResponse<DriverFatigueActivePromptDto?>>
{
    public async Task<ApiResponse<DriverFatigueActivePromptDto?>> Handle(
        GetDriverActiveFatiguePromptQuery request, CancellationToken cancellationToken)
    {
        var driverId = currentUser.DriverId;
        if (!driverId.HasValue)
            return ApiResponse<DriverFatigueActivePromptDto?>.FailResponse("Driver identity required.");

        var tenantId = tenantContext.GetRequiredTenantId();
        var prompt = await repository.GetActivePromptForDriverAsync(tenantId, driverId.Value, cancellationToken);
        return ApiResponse<DriverFatigueActivePromptDto?>.SuccessResponse(prompt);
    }
}
