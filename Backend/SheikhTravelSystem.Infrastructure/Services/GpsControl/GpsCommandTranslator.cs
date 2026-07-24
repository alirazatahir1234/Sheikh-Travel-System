using Dapper;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;

namespace SheikhTravelSystem.Infrastructure.Services.GpsControl;

public sealed class GpsCommandTranslator(IDbConnectionFactory dbFactory) : IGpsCommandTranslator
{
    public async Task<GpsTranslateResult> TranslateAsync(
        GpsTranslateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.CommandKey))
            return Fail(request.CommandKey, "Command key is required.");

        using var connection = dbFactory.CreateConnection();

        var definition = await connection.QueryFirstOrDefaultAsync<DefinitionRow>(new CommandDefinition(
            """
            SELECT CommandKey, DisplayName, RequiredCapabilityKey, DangerLevel, RequiresApproval, IsActive
            FROM GpsCommandDefinitions
            WHERE CommandKey = @CommandKey
            """,
            new { request.CommandKey },
            cancellationToken: cancellationToken));

        if (definition is null)
            return Fail(request.CommandKey, $"Unknown command '{request.CommandKey}'.");

        if (!definition.IsActive)
            return Fail(request.CommandKey, "Command is inactive.");

        var modelId = request.TrackerModelId;
        string? firmware = request.FirmwareVersion;

        if (request.DeviceId is > 0)
        {
            var device = await connection.QueryFirstOrDefaultAsync<(int? TrackerModelId, string? FirmwareVersion)>(
                new CommandDefinition(
                    "SELECT TrackerModelId, FirmwareVersion FROM GpsDevices WHERE Id = @Id AND IsDeleted = 0",
                    new { Id = request.DeviceId.Value },
                    cancellationToken: cancellationToken));
            modelId ??= device.TrackerModelId;
            firmware ??= device.FirmwareVersion;
        }

        if (modelId is null or <= 0)
            return Fail(request.CommandKey, "No tracker model resolved for translation.");

        if (!string.IsNullOrWhiteSpace(definition.RequiredCapabilityKey))
        {
            var hasCap = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
                """
                SELECT CASE WHEN EXISTS (
                    SELECT 1 FROM TrackerModelCapabilities
                    WHERE TrackerModelId = @ModelId AND CapabilityKey = @Cap AND IsEnabled = 1
                ) OR EXISTS (
                    SELECT 1 FROM TrackerModels
                    WHERE Id = @ModelId AND (
                        (@Cap = N'EngineCut' AND SupportsEngineCutOff = 1) OR
                        (@Cap = N'Relay' AND SupportsRelay = 1) OR
                        (@Cap = N'Battery' AND SupportsBatteryMonitoring = 1) OR
                        (@Cap = N'Acc' AND SupportsIgnition = 1)
                    )
                ) THEN 1 ELSE 0 END
                """,
                new { ModelId = modelId.Value, Cap = definition.RequiredCapabilityKey },
                cancellationToken: cancellationToken));

            if (!hasCap)
                return Fail(request.CommandKey, $"Model lacks required capability '{definition.RequiredCapabilityKey}'.");
        }

        var templates = (await connection.QueryAsync<TemplateRow>(new CommandDefinition(
            """
            SELECT Id, Transport, PayloadTemplate, TraccarType, ParserKey, FirmwareMin, FirmwareMax, TemplateVersion
            FROM GpsCommandTemplates
            WHERE TrackerModelId = @ModelId AND CommandKey = @CommandKey AND IsActive = 1
              AND (ValidFrom IS NULL OR ValidFrom <= SYSUTCDATETIME())
              AND (ValidTo IS NULL OR ValidTo >= SYSUTCDATETIME())
            """,
            new { ModelId = modelId.Value, request.CommandKey },
            cancellationToken: cancellationToken))).ToList();

        var best = GpsCommandTemplateRenderer.ResolveBestTemplate(
            templates,
            firmware,
            t => t.FirmwareMin,
            t => t.FirmwareMax,
            t => t.TemplateVersion);

        if (best is null)
            return Fail(request.CommandKey, "No command template for this model/firmware.");

        var paramsMerged = await MergeParams(connection, request.CommandKey, request.Parameters, cancellationToken);

        var missing = await connection.QueryAsync<string>(new CommandDefinition(
            """
            SELECT ParamKey FROM GpsCommandParameterDefinitions
            WHERE CommandKey = @CommandKey AND IsRequired = 1
            """,
            new { request.CommandKey },
            cancellationToken: cancellationToken));

        foreach (var key in missing)
        {
            if (!paramsMerged.TryGetValue(key, out var v) || string.IsNullOrWhiteSpace(v))
                return Fail(request.CommandKey, $"Required parameter '{key}' is missing.");
        }

        var rendered = GpsCommandTemplateRenderer.Render(best.PayloadTemplate, paramsMerged);
        var transport = request.UseSimulator
            ? "Simulator"
            : string.IsNullOrWhiteSpace(best.Transport) || best.Transport.Equals("Auto", StringComparison.OrdinalIgnoreCase)
                ? (best.TraccarType is not null ? "Traccar" : "Sms")
                : best.Transport;

        return new GpsTranslateResult(
            true,
            null,
            request.CommandKey,
            transport,
            rendered,
            best.TraccarType,
            best.Id,
            best.ParserKey,
            definition.RequiresApproval,
            definition.DangerLevel);
    }

    private static async Task<Dictionary<string, string>> MergeParams(
        System.Data.IDbConnection connection,
        string commandKey,
        IReadOnlyDictionary<string, string>? provided,
        CancellationToken cancellationToken)
    {
        var defaults = await connection.QueryAsync<(string ParamKey, string? DefaultValue)>(new CommandDefinition(
            """
            SELECT ParamKey, DefaultValue FROM GpsCommandParameterDefinitions
            WHERE CommandKey = @CommandKey
            """,
            new { CommandKey = commandKey },
            cancellationToken: cancellationToken));

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var d in defaults)
        {
            if (!string.IsNullOrEmpty(d.DefaultValue))
                map[d.ParamKey] = d.DefaultValue;
        }

        if (provided is not null)
        {
            foreach (var kv in provided)
                map[kv.Key] = kv.Value ?? string.Empty;
        }

        return map;
    }

    private static GpsTranslateResult Fail(string? key, string error) =>
        new(false, error, key ?? string.Empty, string.Empty, string.Empty, null, null, null, false, "Low");

    private sealed class DefinitionRow
    {
        public string CommandKey { get; init; } = "";
        public string DisplayName { get; init; } = "";
        public string? RequiredCapabilityKey { get; init; }
        public string DangerLevel { get; init; } = "Low";
        public bool RequiresApproval { get; init; }
        public bool IsActive { get; init; }
    }

    private sealed class TemplateRow
    {
        public int Id { get; init; }
        public string Transport { get; init; } = "";
        public string PayloadTemplate { get; init; } = "";
        public string? TraccarType { get; init; }
        public string? ParserKey { get; init; }
        public string? FirmwareMin { get; init; }
        public string? FirmwareMax { get; init; }
        public int TemplateVersion { get; init; }
    }
}
