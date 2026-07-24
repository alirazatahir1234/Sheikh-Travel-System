using Dapper;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Services.GpsControl;

public sealed class GpsControlCenterService(IDbConnectionFactory dbFactory) : IGpsControlCenterService
{
    public async Task<GpsControlDashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var row = await connection.QueryFirstAsync<(int Manufacturers, int Models, int Commands, int Templates, int Queued, int Failed, int OnlineDevices, int OfflineDevices)>(
            new CommandDefinition(
                """
                SELECT
                    (SELECT COUNT(*) FROM TrackerBrands WHERE IsActive = 1) AS Manufacturers,
                    (SELECT COUNT(*) FROM TrackerModels WHERE IsActive = 1) AS Models,
                    (SELECT COUNT(*) FROM GpsCommandDefinitions WHERE IsActive = 1) AS Commands,
                    (SELECT COUNT(*) FROM GpsCommandTemplates WHERE IsActive = 1) AS Templates,
                    (SELECT COUNT(*) FROM GpsDeviceCommands WHERE IsDeleted = 0 AND Status IN (N'pending', N'sent', N'PendingApproval')) AS Queued,
                    (SELECT COUNT(*) FROM GpsDeviceCommands WHERE IsDeleted = 0 AND Status IN (N'failed', N'timeout')) AS Failed,
                    (SELECT COUNT(*) FROM GpsDevices WHERE IsDeleted = 0 AND IsActive = 1
                        AND LastSeenAt IS NOT NULL AND LastSeenAt >= DATEADD(MINUTE, -30, SYSUTCDATETIME())) AS OnlineDevices,
                    (SELECT COUNT(*) FROM GpsDevices WHERE IsDeleted = 0 AND IsActive = 1
                        AND (LastSeenAt IS NULL OR LastSeenAt < DATEADD(MINUTE, -30, SYSUTCDATETIME()))) AS OfflineDevices
                """,
                cancellationToken: cancellationToken));

        return new GpsControlDashboardDto(
            row.Manufacturers, row.Models, row.Commands, row.Templates,
            row.Queued, row.Failed, row.OnlineDevices, row.OfflineDevices);
    }

    public async Task<IReadOnlyList<GpsManufacturerDto>> GetManufacturersAsync(CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.QueryAsync<GpsManufacturerDto>(new CommandDefinition(
            """
            SELECT Id, Name, VendorKey, Website, Description, DefaultProtocol,
                   ISNULL(SupportsTraccar, 1) AS SupportsTraccar,
                   ISNULL(SupportsSms, 1) AS SupportsSms,
                   IsActive
            FROM TrackerBrands
            ORDER BY Name
            """,
            cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<GpsTrackerModelDto>> GetModelsAsync(int? brandId = null, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var sql = """
            SELECT m.Id, m.TrackerBrandId, b.Name AS BrandName, m.Name, m.CatalogKey,
                   m.Protocol, m.ProtocolLabel, m.DefaultPort, m.FirmwareHint,
                   m.SupportsEngineCutOff, m.SupportsRelay, m.IsActive
            FROM TrackerModels m
            INNER JOIN TrackerBrands b ON b.Id = m.TrackerBrandId
            WHERE 1 = 1
            """;
        if (brandId is > 0)
            sql += " AND m.TrackerBrandId = @BrandId";
        sql += " ORDER BY b.Name, m.Name";

        var rows = await connection.QueryAsync<GpsTrackerModelDto>(new CommandDefinition(
            sql, new { BrandId = brandId }, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<GpsCapabilityDto>> GetCapabilitiesAsync(CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.QueryAsync<GpsCapabilityDto>(new CommandDefinition(
            """
            SELECT CapabilityKey, DisplayName, Category, Description, SortOrder, IsActive
            FROM GpsCapabilities
            WHERE IsActive = 1
            ORDER BY SortOrder, DisplayName
            """,
            cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<string>> GetModelCapabilityKeysAsync(int modelId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var keys = await connection.QueryAsync<string>(new CommandDefinition(
            """
            SELECT CapabilityKey FROM TrackerModelCapabilities
            WHERE TrackerModelId = @ModelId AND IsEnabled = 1
            """,
            new { ModelId = modelId },
            cancellationToken: cancellationToken));
        return keys.ToList();
    }

    public async Task SetModelCapabilityAsync(int modelId, string capabilityKey, bool enabled, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        if (enabled)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                IF EXISTS (SELECT 1 FROM TrackerModelCapabilities WHERE TrackerModelId = @ModelId AND CapabilityKey = @Key)
                    UPDATE TrackerModelCapabilities SET IsEnabled = 1
                    WHERE TrackerModelId = @ModelId AND CapabilityKey = @Key;
                ELSE
                    INSERT INTO TrackerModelCapabilities (TrackerModelId, CapabilityKey, IsEnabled)
                    VALUES (@ModelId, @Key, 1);
                """,
                new { ModelId = modelId, Key = capabilityKey },
                cancellationToken: cancellationToken));
        }
        else
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE TrackerModelCapabilities SET IsEnabled = 0
                WHERE TrackerModelId = @ModelId AND CapabilityKey = @Key;
                """,
                new { ModelId = modelId, Key = capabilityKey },
                cancellationToken: cancellationToken));
        }
    }

    public async Task<IReadOnlyList<GpsCommandDefinitionDto>> GetCommandDefinitionsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.QueryAsync<GpsCommandDefinitionDto>(new CommandDefinition(
            """
            SELECT CommandKey, DisplayName, Category, Description, RequiredCapabilityKey,
                   DangerLevel, RequiresApproval, RequiresReason, SortOrder, IsActive
            FROM GpsCommandDefinitions
            ORDER BY SortOrder, DisplayName
            """,
            cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<GpsCommandParameterDto>> GetCommandParametersAsync(
        string? commandKey = null, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var sql = """
            SELECT Id, CommandKey, ParamKey, DisplayName, DataType, IsRequired,
                   DefaultValue, MinValue, MaxValue, SortOrder
            FROM GpsCommandParameterDefinitions
            WHERE 1 = 1
            """;
        if (!string.IsNullOrWhiteSpace(commandKey))
            sql += " AND CommandKey = @CommandKey";
        sql += " ORDER BY CommandKey, SortOrder";

        var rows = await connection.QueryAsync<GpsCommandParameterDto>(new CommandDefinition(
            sql, new { CommandKey = commandKey }, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<GpsCommandTemplateDto>> GetTemplatesAsync(
        int? modelId = null, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var sql = """
            SELECT t.Id, t.TrackerModelId, m.Name AS ModelName, t.CommandKey, t.Transport,
                   t.PayloadTemplate, t.TraccarType, t.ParserKey, t.FirmwareMin, t.FirmwareMax,
                   t.TemplateVersion, t.IsActive
            FROM GpsCommandTemplates t
            INNER JOIN TrackerModels m ON m.Id = t.TrackerModelId
            WHERE 1 = 1
            """;
        if (modelId is > 0)
            sql += " AND t.TrackerModelId = @ModelId";
        sql += " ORDER BY m.Name, t.CommandKey, t.TemplateVersion DESC";

        var rows = await connection.QueryAsync<GpsCommandTemplateDto>(new CommandDefinition(
            sql, new { ModelId = modelId }, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<int> UpsertManufacturerAsync(GpsManufacturerDto dto, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        if (dto.Id > 0)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE TrackerBrands SET
                    Name = @Name,
                    VendorKey = @VendorKey,
                    Website = @Website,
                    Description = @Description,
                    DefaultProtocol = @DefaultProtocol,
                    SupportsTraccar = @SupportsTraccar,
                    SupportsSms = @SupportsSms,
                    IsActive = @IsActive,
                    UpdatedAt = SYSUTCDATETIME()
                WHERE Id = @Id
                """,
                dto,
                cancellationToken: cancellationToken));
            return dto.Id;
        }

        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            INSERT INTO TrackerBrands
                (Name, VendorKey, Website, Description, DefaultProtocol, SupportsTraccar, SupportsSms, IsActive)
            OUTPUT INSERTED.Id
            VALUES
                (@Name, @VendorKey, @Website, @Description, @DefaultProtocol, @SupportsTraccar, @SupportsSms, @IsActive)
            """,
            dto,
            cancellationToken: cancellationToken));
    }

    public async Task<int> UpsertModelAsync(GpsTrackerModelDto dto, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        if (dto.Id > 0)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE TrackerModels SET
                    TrackerBrandId = @TrackerBrandId,
                    Name = @Name,
                    CatalogKey = @CatalogKey,
                    Protocol = @Protocol,
                    ProtocolLabel = @ProtocolLabel,
                    DefaultPort = @DefaultPort,
                    FirmwareHint = @FirmwareHint,
                    SupportsEngineCutOff = @SupportsEngineCutOff,
                    SupportsRelay = @SupportsRelay,
                    IsActive = @IsActive,
                    UpdatedAt = SYSUTCDATETIME()
                WHERE Id = @Id
                """,
                dto,
                cancellationToken: cancellationToken));
            return dto.Id;
        }

        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            INSERT INTO TrackerModels
                (TrackerBrandId, Name, CatalogKey, Protocol, ProtocolLabel, DefaultPort,
                 FirmwareHint, SupportsEngineCutOff, SupportsRelay, IsActive)
            OUTPUT INSERTED.Id
            VALUES
                (@TrackerBrandId, @Name, @CatalogKey, @Protocol, @ProtocolLabel, @DefaultPort,
                 @FirmwareHint, @SupportsEngineCutOff, @SupportsRelay, @IsActive)
            """,
            dto,
            cancellationToken: cancellationToken));
    }

    public async Task<int> UpsertTemplateAsync(GpsCommandTemplateDto dto, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        if (dto.Id > 0)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE GpsCommandTemplates SET
                    TrackerModelId = @TrackerModelId,
                    CommandKey = @CommandKey,
                    Transport = @Transport,
                    PayloadTemplate = @PayloadTemplate,
                    TraccarType = @TraccarType,
                    ParserKey = @ParserKey,
                    FirmwareMin = @FirmwareMin,
                    FirmwareMax = @FirmwareMax,
                    TemplateVersion = @TemplateVersion,
                    IsActive = @IsActive,
                    UpdatedAt = SYSUTCDATETIME()
                WHERE Id = @Id
                """,
                dto,
                cancellationToken: cancellationToken));
            return dto.Id;
        }

        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            INSERT INTO GpsCommandTemplates
                (TrackerModelId, CommandKey, Transport, PayloadTemplate, TraccarType, ParserKey,
                 FirmwareMin, FirmwareMax, TemplateVersion, IsActive)
            OUTPUT INSERTED.Id
            VALUES
                (@TrackerModelId, @CommandKey, @Transport, @PayloadTemplate, @TraccarType, @ParserKey,
                 @FirmwareMin, @FirmwareMax, @TemplateVersion, @IsActive)
            """,
            dto,
            cancellationToken: cancellationToken));
    }
}
