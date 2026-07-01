using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Trackers.Queries;

public record GetTrackerBrandsQuery : IRequest<ApiResponse<List<TrackerBrandDto>>>;

public class GetTrackerBrandsQueryHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<GetTrackerBrandsQuery, ApiResponse<List<TrackerBrandDto>>>
{
    public async Task<ApiResponse<List<TrackerBrandDto>>> Handle(GetTrackerBrandsQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.QueryAsync<TrackerBrandDto>(new CommandDefinition(
            """
            SELECT Id, Name, LogoUrl, IsActive
            FROM TrackerBrands
            WHERE IsActive = 1
            ORDER BY Name
            """,
            cancellationToken: cancellationToken));
        return ApiResponse<List<TrackerBrandDto>>.SuccessResponse(rows.ToList());
    }
}

public record GetTrackerModelsQuery(int? BrandId = null) : IRequest<ApiResponse<List<TrackerModelDto>>>;

public class GetTrackerModelsQueryHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<GetTrackerModelsQuery, ApiResponse<List<TrackerModelDto>>>
{
    public async Task<ApiResponse<List<TrackerModelDto>>> Handle(GetTrackerModelsQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var sql = """
            SELECT m.Id, m.TrackerBrandId, b.Name AS BrandName, m.Name, m.Protocol, m.ProtocolLabel,
                   m.DefaultPort, m.SupportsEngineCutOff, m.SupportsFuelSensor, m.SupportsTemperatureSensor,
                   m.SupportsDriverIdentification, m.SupportsCanBus, m.SupportsObd, m.SupportsBle,
                   m.SupportsCamera, m.SupportsRelay, m.SupportsDoorSensor, m.SupportsIgnition,
                   m.SupportsOdometer, m.SupportsBatteryMonitoring, m.DefaultRelayOutput,
                   m.CatalogKey, m.Description, m.IsActive
            FROM TrackerModels m
            INNER JOIN TrackerBrands b ON b.Id = m.TrackerBrandId AND b.IsActive = 1
            WHERE m.IsActive = 1
            """;

        if (request.BrandId.HasValue)
            sql += " AND m.TrackerBrandId = @BrandId";

        sql += " ORDER BY b.Name, m.Name";

        var rows = await connection.QueryAsync<TrackerModelDto>(new CommandDefinition(
            sql,
            new { request.BrandId },
            cancellationToken: cancellationToken));

        return ApiResponse<List<TrackerModelDto>>.SuccessResponse(rows.ToList());
    }
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
