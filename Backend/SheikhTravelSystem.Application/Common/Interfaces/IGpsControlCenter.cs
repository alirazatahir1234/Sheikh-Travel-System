namespace SheikhTravelSystem.Application.Common.Interfaces;

public record GpsCommandDefinitionDto(
    string CommandKey,
    string DisplayName,
    string Category,
    string? Description,
    string? RequiredCapabilityKey,
    string DangerLevel,
    bool RequiresApproval,
    bool RequiresReason,
    int SortOrder,
    bool IsActive);

public record GpsCommandParameterDto(
    int Id,
    string CommandKey,
    string ParamKey,
    string DisplayName,
    string DataType,
    bool IsRequired,
    string? DefaultValue,
    decimal? MinValue,
    decimal? MaxValue,
    int SortOrder);

public record GpsCommandTemplateDto(
    int Id,
    int TrackerModelId,
    string? ModelName,
    string CommandKey,
    string Transport,
    string PayloadTemplate,
    string? TraccarType,
    string? ParserKey,
    string? FirmwareMin,
    string? FirmwareMax,
    int TemplateVersion,
    bool IsActive);

public record GpsCapabilityDto(
    string CapabilityKey,
    string DisplayName,
    string Category,
    string? Description,
    int SortOrder,
    bool IsActive);

public record GpsManufacturerDto(
    int Id,
    string Name,
    string? VendorKey,
    string? Website,
    string? Description,
    string? DefaultProtocol,
    bool SupportsTraccar,
    bool SupportsSms,
    bool IsActive);

public record GpsTrackerModelDto(
    int Id,
    int TrackerBrandId,
    string BrandName,
    string Name,
    string? CatalogKey,
    string Protocol,
    string ProtocolLabel,
    int DefaultPort,
    string? FirmwareHint,
    bool SupportsEngineCutOff,
    bool SupportsRelay,
    bool IsActive);

public record GpsControlDashboardDto(
    int Manufacturers,
    int Models,
    int Commands,
    int Templates,
    int Queued,
    int Failed,
    int OnlineDevices,
    int OfflineDevices);

public record GpsTranslateRequest(
    int? DeviceId,
    int? TrackerModelId,
    string CommandKey,
    IReadOnlyDictionary<string, string>? Parameters = null,
    string? FirmwareVersion = null,
    bool UseSimulator = false);

public record GpsTranslateResult(
    bool Success,
    string? Error,
    string CommandKey,
    string Transport,
    string RenderedPayload,
    string? TraccarType,
    int? TemplateId,
    string? ParserKey,
    bool RequiresApproval,
    string DangerLevel);

public record GpsTransportSendRequest(
    string Transport,
    int? TraccarDeviceId,
    string? TraccarType,
    string Payload,
    IDictionary<string, object>? Attributes,
    string? SmsPhone = null);

public record GpsTransportSendResult(
    bool Success,
    string Transport,
    string? ResponseText,
    string? Error,
    int? ExternalCommandId = null);

public interface IGpsCommandTranslator
{
    Task<GpsTranslateResult> TranslateAsync(GpsTranslateRequest request, CancellationToken cancellationToken = default);
}

public interface IGpsTransportProvider
{
    string Name { get; }
    bool CanHandle(string transport);
    Task<GpsTransportSendResult> SendAsync(GpsTransportSendRequest request, CancellationToken cancellationToken = default);
}

public interface IGpsCommandResultParser
{
    string ParserKey { get; }
    IReadOnlyDictionary<string, string>? Parse(string responseText);
}

public interface IGpsControlCenterService
{
    Task<GpsControlDashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GpsManufacturerDto>> GetManufacturersAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GpsTrackerModelDto>> GetModelsAsync(int? brandId = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GpsCapabilityDto>> GetCapabilitiesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetModelCapabilityKeysAsync(int modelId, CancellationToken cancellationToken = default);
    Task SetModelCapabilityAsync(int modelId, string capabilityKey, bool enabled, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GpsCommandDefinitionDto>> GetCommandDefinitionsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GpsCommandParameterDto>> GetCommandParametersAsync(string? commandKey = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GpsCommandTemplateDto>> GetTemplatesAsync(int? modelId = null, CancellationToken cancellationToken = default);
    Task<int> UpsertManufacturerAsync(GpsManufacturerDto dto, CancellationToken cancellationToken = default);
    Task<int> UpsertModelAsync(GpsTrackerModelDto dto, CancellationToken cancellationToken = default);
    Task<int> UpsertTemplateAsync(GpsCommandTemplateDto dto, CancellationToken cancellationToken = default);
}

public interface IGpsTransportRouter
{
    Task<GpsTransportSendResult> SendAsync(GpsTransportSendRequest request, CancellationToken cancellationToken = default);
}

public interface IGpsCommandResultParserRegistry
{
    IReadOnlyDictionary<string, string>? Parse(string? parserKey, string responseText);
}
