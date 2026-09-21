using MediatR;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Commands;

public record ListAnalyticsReportSchedulesQuery() : IRequest<ApiResponse<IReadOnlyList<AnalyticsReportScheduleDto>>>;

public record CreateAnalyticsReportScheduleCommand(CreateAnalyticsReportScheduleDto Body)
    : IRequest<ApiResponse<int>>;

public record UpdateAnalyticsReportScheduleCommand(int Id, UpdateAnalyticsReportScheduleDto Body)
    : IRequest<ApiResponse<bool>>;

public record DeleteAnalyticsReportScheduleCommand(int Id) : IRequest<ApiResponse<bool>>;

public class ListAnalyticsReportSchedulesQueryHandler(IGpsTrackingRepository gpsTrackingRepository)
    : IRequestHandler<ListAnalyticsReportSchedulesQuery, ApiResponse<IReadOnlyList<AnalyticsReportScheduleDto>>>
{
    public Task<ApiResponse<IReadOnlyList<AnalyticsReportScheduleDto>>> Handle(ListAnalyticsReportSchedulesQuery request, CancellationToken cancellationToken)
        => gpsTrackingRepository.ListAnalyticsReportSchedulesAsync(request, cancellationToken);
}

public class CreateAnalyticsReportScheduleCommandHandler(IGpsTrackingRepository gpsTrackingRepository)
    : IRequestHandler<CreateAnalyticsReportScheduleCommand, ApiResponse<int>>
{
    public Task<ApiResponse<int>> Handle(CreateAnalyticsReportScheduleCommand request, CancellationToken cancellationToken)
        => gpsTrackingRepository.CreateAnalyticsReportScheduleAsync(request, cancellationToken);
}

public class UpdateAnalyticsReportScheduleCommandHandler(IGpsTrackingRepository gpsTrackingRepository)
    : IRequestHandler<UpdateAnalyticsReportScheduleCommand, ApiResponse<bool>>
{
    public Task<ApiResponse<bool>> Handle(UpdateAnalyticsReportScheduleCommand request, CancellationToken cancellationToken)
        => gpsTrackingRepository.UpdateAnalyticsReportScheduleAsync(request, cancellationToken);
}

public class DeleteAnalyticsReportScheduleCommandHandler(IGpsTrackingRepository gpsTrackingRepository)
    : IRequestHandler<DeleteAnalyticsReportScheduleCommand, ApiResponse<bool>>
{
    public Task<ApiResponse<bool>> Handle(DeleteAnalyticsReportScheduleCommand request, CancellationToken cancellationToken)
        => gpsTrackingRepository.DeleteAnalyticsReportScheduleAsync(request, cancellationToken);
}

public sealed record AnalyticsReportScheduleRow(
    int Id, string ReportType, string? FiltersJson, string Frequency, string Recipients,
    DateTime? NextRunAt, DateTime? LastRunAt, string? LastRunStatus, bool IsActive)
{
    public AnalyticsReportScheduleDto ToDto() => new(
        Id, ReportType, AnalyticsReportHelper.ParseFilters(FiltersJson),
        Frequency, Recipients, NextRunAt, LastRunAt, LastRunStatus, IsActive);
}
