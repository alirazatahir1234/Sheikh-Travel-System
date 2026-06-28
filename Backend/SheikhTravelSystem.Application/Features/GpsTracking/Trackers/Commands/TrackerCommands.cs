using FluentValidation;
using MediatR;
using Dapper;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.GpsTracking.Traccar;
using SheikhTravelSystem.Application.Features.GpsTracking.Trackers;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Trackers.Commands;

public record RegisterTrackerCommand(RegisterTrackerDto Tracker) : IRequest<ApiResponse<TrackerRegisteredDto>>;

public class RegisterTrackerCommandValidator : AbstractValidator<RegisterTrackerCommand>
{
    public RegisterTrackerCommandValidator()
    {
        RuleFor(x => x.Tracker.UniqueId)
            .NotEmpty()
            .Matches(@"^\d{15}$")
            .WithMessage("IMEI must be exactly 15 digits.");

        RuleFor(x => x.Tracker.Name)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.Tracker.Category)
            .NotEmpty()
            .Must(c => TrackerCatalog.ValidCategories.Contains(c))
            .WithMessage("Invalid category.");

        RuleFor(x => x.Tracker)
            .Must(t => t.TrackerModelId > 0 || !string.IsNullOrWhiteSpace(t.TrackerModelKey))
            .WithMessage("Tracker model is required.");

        RuleFor(x => x.Tracker.WarrantyStart)
            .Must(d => !d.HasValue || d.Value >= DateOnly.FromDateTime(DateTime.UtcNow.Date))
            .WithMessage("Warranty start cannot be in the past.");

        RuleFor(x => x.Tracker.PurchaseDate)
            .Must(d => !d.HasValue || d.Value >= DateOnly.FromDateTime(DateTime.UtcNow.Date))
            .WithMessage("Purchase date cannot be in the past.");

        RuleFor(x => x.Tracker.WarrantyEnd)
            .GreaterThanOrEqualTo(x => x.Tracker.WarrantyStart)
            .When(x => x.Tracker.WarrantyStart.HasValue && x.Tracker.WarrantyEnd.HasValue)
            .WithMessage("Warranty end must be on or after warranty start.");

        RuleFor(x => x.Tracker.RelayOutput)
            .NotEmpty()
            .When(x => x.Tracker.SupportsEngineCutoff)
            .WithMessage("Relay output is required when engine immobilizer is enabled.");
    }
}

public class RegisterTrackerCommandHandler(ITrackerRegistrationService service)
    : IRequestHandler<RegisterTrackerCommand, ApiResponse<TrackerRegisteredDto>>
{
    public Task<ApiResponse<TrackerRegisteredDto>> Handle(RegisterTrackerCommand request, CancellationToken cancellationToken)
        => service.RegisterAsync(request.Tracker, cancellationToken);
}

public record UpdateTrackerCommand(int Id, UpdateTrackerDto Tracker) : IRequest<ApiResponse<bool>>;

public class UpdateTrackerCommandValidator : AbstractValidator<UpdateTrackerCommand>
{
    public UpdateTrackerCommandValidator()
    {
        RuleFor(x => x.Tracker.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Tracker.Category)
            .NotEmpty()
            .Must(c => TrackerCatalog.ValidCategories.Contains(c));
        RuleFor(x => x.Tracker)
            .Must(t => t.TrackerModelId > 0 || !string.IsNullOrWhiteSpace(t.TrackerModelKey))
            .WithMessage("Tracker model is required.");
        RuleFor(x => x.Tracker.WarrantyStart)
            .Must(d => !d.HasValue || d.Value >= DateOnly.FromDateTime(DateTime.UtcNow.Date));
        RuleFor(x => x.Tracker.PurchaseDate)
            .Must(d => !d.HasValue || d.Value >= DateOnly.FromDateTime(DateTime.UtcNow.Date));
        RuleFor(x => x.Tracker.RelayOutput)
            .NotEmpty()
            .When(x => x.Tracker.SupportsEngineCutoff);
    }
}

public class UpdateTrackerCommandHandler(ITrackerRegistrationService service)
    : IRequestHandler<UpdateTrackerCommand, ApiResponse<bool>>
{
    public Task<ApiResponse<bool>> Handle(UpdateTrackerCommand request, CancellationToken cancellationToken)
        => service.UpdateAsync(request.Id, request.Tracker, cancellationToken);
}

public record DeleteTrackerCommand(int Id) : IRequest<ApiResponse<bool>>;

public class DeleteTrackerCommandHandler(ITrackerRegistrationService service)
    : IRequestHandler<DeleteTrackerCommand, ApiResponse<bool>>
{
    public Task<ApiResponse<bool>> Handle(DeleteTrackerCommand request, CancellationToken cancellationToken)
        => service.DeleteAsync(request.Id, cancellationToken);
}

public record InstallTrackerCommand(int Id, InstallTrackerDto Body) : IRequest<ApiResponse<bool>>;

public class InstallTrackerCommandValidator : AbstractValidator<InstallTrackerCommand>
{
    public InstallTrackerCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Body.VehicleId).GreaterThan(0);
    }
}

public class InstallTrackerCommandHandler(ITrackerRegistrationService service)
    : IRequestHandler<InstallTrackerCommand, ApiResponse<bool>>
{
    public Task<ApiResponse<bool>> Handle(InstallTrackerCommand request, CancellationToken cancellationToken)
        => service.InstallAsync(request.Id, request.Body, cancellationToken);
}

public record UninstallTrackerCommand(int Id) : IRequest<ApiResponse<bool>>;

public class UninstallTrackerCommandValidator : AbstractValidator<UninstallTrackerCommand>
{
    public UninstallTrackerCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
    }
}

public class UninstallTrackerCommandHandler(ITrackerRegistrationService service)
    : IRequestHandler<UninstallTrackerCommand, ApiResponse<bool>>
{
    public Task<ApiResponse<bool>> Handle(UninstallTrackerCommand request, CancellationToken cancellationToken)
        => service.UninstallAsync(request.Id, cancellationToken);
}

public record SyncTrackerCommand(int Id) : IRequest<ApiResponse<TraccarSyncRunResult>>;

public class SyncTrackerCommandHandler(
    IDbConnectionFactory dbFactory,
    ITenantContext tenantContext,
    ITraccarSyncOrchestrator orchestrator)
    : IRequestHandler<SyncTrackerCommand, ApiResponse<TraccarSyncRunResult>>
{
    public async Task<ApiResponse<TraccarSyncRunResult>> Handle(SyncTrackerCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();
        var allowed = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            TrackerTenantSql.DeviceExistsForTenant,
            new { Id = request.Id, TenantId = tenantId },
            cancellationToken: cancellationToken));

        if (!allowed)
            return ApiResponse<TraccarSyncRunResult>.FailResponse("Tracker not found.");

        var result = await orchestrator.SyncTrackerAsync(request.Id, cancellationToken);
        return ApiResponse<TraccarSyncRunResult>.SuccessResponse(result);
    }
}

public record SyncAllTrackersCommand : IRequest<ApiResponse<TraccarSyncRunResult>>;

public class SyncAllTrackersCommandHandler(ITraccarSyncOrchestrator orchestrator)
    : IRequestHandler<SyncAllTrackersCommand, ApiResponse<TraccarSyncRunResult>>
{
    public async Task<ApiResponse<TraccarSyncRunResult>> Handle(SyncAllTrackersCommand request, CancellationToken cancellationToken)
    {
        var result = await orchestrator.RunManualSyncAsync(cancellationToken);
        return ApiResponse<TraccarSyncRunResult>.SuccessResponse(result);
    }
}
