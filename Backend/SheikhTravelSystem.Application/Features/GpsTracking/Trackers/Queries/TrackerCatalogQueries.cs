using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Trackers.Queries;

public record GetTrackerBrandsQuery : IRequest<ApiResponse<List<TrackerBrandDto>>>;

public class GetTrackerBrandsQueryHandler(IGpsDeviceRepository gpsDeviceRepository)
    : IRequestHandler<GetTrackerBrandsQuery, ApiResponse<List<TrackerBrandDto>>>
{
    public Task<ApiResponse<List<TrackerBrandDto>>> Handle(GetTrackerBrandsQuery request, CancellationToken cancellationToken)
        => gpsDeviceRepository.GetTrackerBrandsAsync(request, cancellationToken);
}

public record GetTrackerModelsQuery(int? BrandId = null) : IRequest<ApiResponse<List<TrackerModelDto>>>;

public class GetTrackerModelsQueryHandler(IGpsDeviceRepository gpsDeviceRepository)
    : IRequestHandler<GetTrackerModelsQuery, ApiResponse<List<TrackerModelDto>>>
{
    public Task<ApiResponse<List<TrackerModelDto>>> Handle(GetTrackerModelsQuery request, CancellationToken cancellationToken)
        => gpsDeviceRepository.GetTrackerModelsAsync(request, cancellationToken);
}

public static class TrackerCatalogSql
{
    public const string ModelById = """
        SELECT m.Id, m.TrackerBrandId, b.Name AS BrandName, m.Name,
               m.CatalogKey, m.Protocol, m.ProtocolLabel, m.DefaultPort,
               m.SupportsEngineCutOff, m.SupportsRelay, m.DefaultRelayOutput
        FROM TrackerModels m
        INNER JOIN TrackerBrands b ON b.Id = m.TrackerBrandId
        WHERE m.Id = @Id AND m.IsActive = 1 AND b.IsActive = 1
        """;

    public const string ModelByCatalogKey = """
        SELECT m.Id, m.TrackerBrandId, b.Name AS BrandName, m.Name,
               m.CatalogKey, m.Protocol, m.ProtocolLabel, m.DefaultPort,
               m.SupportsEngineCutOff, m.SupportsRelay, m.DefaultRelayOutput
        FROM TrackerModels m
        INNER JOIN TrackerBrands b ON b.Id = m.TrackerBrandId
        WHERE m.CatalogKey = @CatalogKey AND m.IsActive = 1 AND b.IsActive = 1
        """;
}
