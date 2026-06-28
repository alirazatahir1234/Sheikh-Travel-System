namespace SheikhTravelSystem.Application.Features.GpsTracking.Trackers;

public record TrackerBrandDto(
    int Id,
    string Name,
    string? LogoUrl,
    bool IsActive);

public record TrackerModelDto(
    int Id,
    int TrackerBrandId,
    string BrandName,
    string Name,
    string Protocol,
    string ProtocolLabel,
    int DefaultPort,
    bool SupportsEngineCutOff,
    bool SupportsFuelSensor,
    bool SupportsTemperatureSensor,
    bool SupportsDriverIdentification,
    bool SupportsCanBus,
    bool SupportsObd,
    bool SupportsBle,
    bool SupportsCamera,
    bool SupportsRelay,
    bool SupportsDoorSensor,
    bool SupportsIgnition,
    bool SupportsOdometer,
    bool SupportsBatteryMonitoring,
    string? CatalogKey,
    string? Description,
    bool IsActive);

public record TrackerModelRecord(
    int Id,
    int TrackerBrandId,
    string BrandName,
    string Name,
    string CatalogKey,
    string Protocol,
    string ProtocolLabel,
    int DefaultPort,
    bool SupportsEngineCutOff,
    bool SupportsRelay);
