using System.Text.Json;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.GpsTracking;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;
using SheikhTravelSystem.Application.Features.GpsTracking.Traccar;
using SheikhTravelSystem.Application.Features.Notifications;
using SheikhTravelSystem.Domain.Enums;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Commands;

public record CreateGpsAlertRuleCommand(CreateGpsAlertRuleDto Rule) : IRequest<ApiResponse<int>>;

public class CreateGpsAlertRuleCommandHandler(IGpsTrackingRepository gpsTrackingRepository)
    : IRequestHandler<CreateGpsAlertRuleCommand, ApiResponse<int>>
{
    public Task<ApiResponse<int>> Handle(CreateGpsAlertRuleCommand request, CancellationToken cancellationToken)
        => gpsTrackingRepository.CreateGpsAlertRuleAsync(request, cancellationToken);
}

public record AcknowledgeGpsAlertCommand(int Id) : IRequest<ApiResponse<bool>>;

public class AcknowledgeGpsAlertCommandHandler(IGpsTrackingRepository gpsTrackingRepository)
    : IRequestHandler<AcknowledgeGpsAlertCommand, ApiResponse<bool>>
{
    public Task<ApiResponse<bool>> Handle(AcknowledgeGpsAlertCommand request, CancellationToken cancellationToken)
        => gpsTrackingRepository.AcknowledgeGpsAlertAsync(request, cancellationToken);
}

public record MarkGpsAlertReadCommand(int Id) : IRequest<ApiResponse<bool>>;

public class MarkGpsAlertReadCommandHandler(IGpsTrackingRepository gpsTrackingRepository)
    : IRequestHandler<MarkGpsAlertReadCommand, ApiResponse<bool>>
{
    public Task<ApiResponse<bool>> Handle(MarkGpsAlertReadCommand request, CancellationToken cancellationToken)
        => gpsTrackingRepository.MarkGpsAlertReadAsync(request, cancellationToken);
}

public record ResolveGpsAlertCommand(int Id, ResolveGpsAlertDto Resolution) : IRequest<ApiResponse<bool>>;

public class ResolveGpsAlertCommandHandler(IGpsTrackingRepository gpsTrackingRepository)
    : IRequestHandler<ResolveGpsAlertCommand, ApiResponse<bool>>
{
    public Task<ApiResponse<bool>> Handle(ResolveGpsAlertCommand request, CancellationToken cancellationToken)
        => gpsTrackingRepository.ResolveGpsAlertAsync(request, cancellationToken);
}

public record ArchiveGpsAlertCommand(int Id, ArchiveGpsAlertDto Archive) : IRequest<ApiResponse<bool>>;

public class ArchiveGpsAlertCommandHandler(IGpsTrackingRepository gpsTrackingRepository)
    : IRequestHandler<ArchiveGpsAlertCommand, ApiResponse<bool>>
{
    public Task<ApiResponse<bool>> Handle(ArchiveGpsAlertCommand request, CancellationToken cancellationToken)
        => gpsTrackingRepository.ArchiveGpsAlertAsync(request, cancellationToken);
}

public record DeleteGpsAlertEventCommand(int Id) : IRequest<ApiResponse<bool>>;

public class DeleteGpsAlertEventCommandHandler(IGpsTrackingRepository gpsTrackingRepository)
    : IRequestHandler<DeleteGpsAlertEventCommand, ApiResponse<bool>>
{
    public Task<ApiResponse<bool>> Handle(DeleteGpsAlertEventCommand request, CancellationToken cancellationToken)
        => gpsTrackingRepository.DeleteGpsAlertEventAsync(request, cancellationToken);
}

public record UpdateAlertSettingsCommand(UpdateAlertSettingsDto Settings) : IRequest<ApiResponse<bool>>;

public class UpdateAlertSettingsCommandHandler(IGpsTrackingRepository gpsTrackingRepository)
    : IRequestHandler<UpdateAlertSettingsCommand, ApiResponse<bool>>
{
    public Task<ApiResponse<bool>> Handle(UpdateAlertSettingsCommand request, CancellationToken cancellationToken)
        => gpsTrackingRepository.UpdateAlertSettingsAsync(request, cancellationToken);
}

public record SendDeviceCommandCommand(SendDeviceCommandDto Command) : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => "Send";
    public string AuditEntityName => "GpsDeviceCommand";
    public int? AuditEntityId => null;
}

public class SendDeviceCommandCommandValidator : AbstractValidator<SendDeviceCommandCommand>
{
    public SendDeviceCommandCommandValidator()
    {
        RuleFor(x => x.Command.GpsDeviceId).GreaterThan(0);
        RuleFor(x => x.Command.CommandType).Must(t => GpsCommandCatalog.Find(t) is not null)
            .WithMessage("Unknown command type.");
    }
}

public class SendDeviceCommandCommandHandler(IGpsDeviceRepository gpsDeviceRepository)
    : IRequestHandler<SendDeviceCommandCommand, ApiResponse<int>>
{
    public Task<ApiResponse<int>> Handle(SendDeviceCommandCommand request, CancellationToken cancellationToken)
        => gpsDeviceRepository.SendDeviceCommandAsync(request, cancellationToken);
}

public record RetryDeviceCommandCommand(int Id) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Retry";
    public string AuditEntityName => "GpsDeviceCommand";
    public int? AuditEntityId => Id;
}

public class RetryDeviceCommandCommandHandler(IGpsDeviceRepository gpsDeviceRepository)
    : IRequestHandler<RetryDeviceCommandCommand, ApiResponse<bool>>
{
    public Task<ApiResponse<bool>> Handle(RetryDeviceCommandCommand request, CancellationToken cancellationToken)
        => gpsDeviceRepository.RetryDeviceCommandAsync(request, cancellationToken);
}

public record CancelDeviceCommandCommand(int Id, string? Reason) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Cancel";
    public string AuditEntityName => "GpsDeviceCommand";
    public int? AuditEntityId => Id;
}

public class CancelDeviceCommandCommandHandler(IGpsDeviceRepository gpsDeviceRepository)
    : IRequestHandler<CancelDeviceCommandCommand, ApiResponse<bool>>
{
    public Task<ApiResponse<bool>> Handle(CancelDeviceCommandCommand request, CancellationToken cancellationToken)
        => gpsDeviceRepository.CancelDeviceCommandAsync(request, cancellationToken);
}

public record CompleteDeviceCommandCommand(int Id, string UniqueId, string Status, string? ResponseText = null, string? ErrorMessage = null)
    : IRequest<ApiResponse<bool>>;

public class CompleteDeviceCommandCommandHandler(IGpsDeviceRepository gpsDeviceRepository)
    : IRequestHandler<CompleteDeviceCommandCommand, ApiResponse<bool>>
{
    public Task<ApiResponse<bool>> Handle(CompleteDeviceCommandCommand request, CancellationToken cancellationToken)
        => gpsDeviceRepository.CompleteDeviceCommandAsync(request, cancellationToken);
}
