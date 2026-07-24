using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using System.Data;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Stage 16 GPS Device Control Center — manufacturers/models extensions, capabilities,
/// command definitions/parameters/templates, transport prefs, permissions, menu.
/// </summary>
public static class GpsControlCenterFoundationMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        await ExtendBrandModelColumnsAsync(connection, cancellationToken);
        await CreateCapabilityTablesAsync(connection, cancellationToken);
        await CreateCommandTablesAsync(connection, cancellationToken);
        await SeedPermissionsAndMenuAsync(connection, cancellationToken);
        await SeedCapabilitiesAsync(connection, cancellationToken);
        await SeedEv26rAndCommandsAsync(connection, cancellationToken);
        await BackfillModelCapabilitiesAsync(connection, cancellationToken);

        logger.LogInformation("GpsControlCenterFoundationMigration applied successfully.");
    }

    private static async Task ExtendBrandModelColumnsAsync(IDbConnection connection, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            IF OBJECT_ID('TrackerBrands','U') IS NOT NULL
            BEGIN
                IF COL_LENGTH('TrackerBrands', 'VendorKey') IS NULL
                    ALTER TABLE TrackerBrands ADD VendorKey NVARCHAR(50) NULL;
                IF COL_LENGTH('TrackerBrands', 'DefaultProtocol') IS NULL
                    ALTER TABLE TrackerBrands ADD DefaultProtocol NVARCHAR(50) NULL;
                IF COL_LENGTH('TrackerBrands', 'SupportsTraccar') IS NULL
                    ALTER TABLE TrackerBrands ADD SupportsTraccar BIT NOT NULL CONSTRAINT DF_TrackerBrands_SupportsTraccar DEFAULT 1;
                IF COL_LENGTH('TrackerBrands', 'SupportsSms') IS NULL
                    ALTER TABLE TrackerBrands ADD SupportsSms BIT NOT NULL CONSTRAINT DF_TrackerBrands_SupportsSms DEFAULT 1;
                IF COL_LENGTH('TrackerBrands', 'UpdatedAt') IS NULL
                    ALTER TABLE TrackerBrands ADD UpdatedAt DATETIME2 NULL;
            END

            IF OBJECT_ID('TrackerModels','U') IS NOT NULL
            BEGIN
                IF COL_LENGTH('TrackerModels', 'FirmwareHint') IS NULL
                    ALTER TABLE TrackerModels ADD FirmwareHint NVARCHAR(100) NULL;
                IF COL_LENGTH('TrackerModels', 'Icon') IS NULL
                    ALTER TABLE TrackerModels ADD Icon NVARCHAR(100) NULL;
                IF COL_LENGTH('TrackerModels', 'UpdatedAt') IS NULL
                    ALTER TABLE TrackerModels ADD UpdatedAt DATETIME2 NULL;
            END

            IF OBJECT_ID('GpsDevices','U') IS NOT NULL AND COL_LENGTH('GpsDevices', 'FirmwareVersion') IS NULL
                ALTER TABLE GpsDevices ADD FirmwareVersion NVARCHAR(100) NULL;

            IF OBJECT_ID('GpsDeviceCommands','U') IS NOT NULL
            BEGIN
                IF COL_LENGTH('GpsDeviceCommands', 'CommandKey') IS NULL
                    ALTER TABLE GpsDeviceCommands ADD CommandKey NVARCHAR(80) NULL;
                IF COL_LENGTH('GpsDeviceCommands', 'Transport') IS NULL
                    ALTER TABLE GpsDeviceCommands ADD Transport NVARCHAR(40) NULL;
                IF COL_LENGTH('GpsDeviceCommands', 'RenderedPayload') IS NULL
                    ALTER TABLE GpsDeviceCommands ADD RenderedPayload NVARCHAR(MAX) NULL;
                IF COL_LENGTH('GpsDeviceCommands', 'ApprovalStatus') IS NULL
                    ALTER TABLE GpsDeviceCommands ADD ApprovalStatus NVARCHAR(40) NULL;
                IF COL_LENGTH('GpsDeviceCommands', 'ApprovedBy') IS NULL
                    ALTER TABLE GpsDeviceCommands ADD ApprovedBy NVARCHAR(100) NULL;
                IF COL_LENGTH('GpsDeviceCommands', 'ApprovedAt') IS NULL
                    ALTER TABLE GpsDeviceCommands ADD ApprovedAt DATETIME2 NULL;
                IF COL_LENGTH('GpsDeviceCommands', 'ParsedResultJson') IS NULL
                    ALTER TABLE GpsDeviceCommands ADD ParsedResultJson NVARCHAR(MAX) NULL;
            END
            """, cancellationToken: ct));
    }

    private static async Task CreateCapabilityTablesAsync(IDbConnection connection, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'GpsCapabilities')
            CREATE TABLE GpsCapabilities (
                CapabilityKey NVARCHAR(80) NOT NULL CONSTRAINT PK_GpsCapabilities PRIMARY KEY,
                DisplayName NVARCHAR(200) NOT NULL,
                Category NVARCHAR(100) NOT NULL CONSTRAINT DF_GpsCapabilities_Category DEFAULT (N'General'),
                Description NVARCHAR(500) NULL,
                SortOrder INT NOT NULL CONSTRAINT DF_GpsCapabilities_SortOrder DEFAULT (0),
                IsActive BIT NOT NULL CONSTRAINT DF_GpsCapabilities_IsActive DEFAULT (1),
                CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_GpsCapabilities_CreatedAt DEFAULT (SYSUTCDATETIME())
            );

            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'TrackerModelCapabilities')
            CREATE TABLE TrackerModelCapabilities (
                TrackerModelId INT NOT NULL,
                CapabilityKey NVARCHAR(80) NOT NULL,
                IsEnabled BIT NOT NULL CONSTRAINT DF_TrackerModelCapabilities_IsEnabled DEFAULT (1),
                CONSTRAINT PK_TrackerModelCapabilities PRIMARY KEY (TrackerModelId, CapabilityKey),
                CONSTRAINT FK_TrackerModelCapabilities_Models FOREIGN KEY (TrackerModelId) REFERENCES TrackerModels(Id),
                CONSTRAINT FK_TrackerModelCapabilities_Caps FOREIGN KEY (CapabilityKey) REFERENCES GpsCapabilities(CapabilityKey)
            );
            """, cancellationToken: ct));
    }

    private static async Task CreateCommandTablesAsync(IDbConnection connection, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'GpsCommandDefinitions')
            CREATE TABLE GpsCommandDefinitions (
                CommandKey NVARCHAR(80) NOT NULL CONSTRAINT PK_GpsCommandDefinitions PRIMARY KEY,
                DisplayName NVARCHAR(200) NOT NULL,
                Category NVARCHAR(100) NOT NULL,
                Description NVARCHAR(500) NULL,
                RequiredCapabilityKey NVARCHAR(80) NULL,
                DangerLevel NVARCHAR(40) NOT NULL CONSTRAINT DF_GpsCommandDefinitions_Danger DEFAULT (N'Low'),
                RequiresApproval BIT NOT NULL CONSTRAINT DF_GpsCommandDefinitions_Approval DEFAULT (0),
                RequiresReason BIT NOT NULL CONSTRAINT DF_GpsCommandDefinitions_Reason DEFAULT (0),
                SortOrder INT NOT NULL CONSTRAINT DF_GpsCommandDefinitions_Sort DEFAULT (0),
                IsActive BIT NOT NULL CONSTRAINT DF_GpsCommandDefinitions_Active DEFAULT (1),
                IsSystem BIT NOT NULL CONSTRAINT DF_GpsCommandDefinitions_System DEFAULT (1),
                CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_GpsCommandDefinitions_CreatedAt DEFAULT (SYSUTCDATETIME()),
                UpdatedAt DATETIME2 NULL
            );

            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'GpsCommandParameterDefinitions')
            CREATE TABLE GpsCommandParameterDefinitions (
                Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_GpsCommandParameterDefinitions PRIMARY KEY,
                CommandKey NVARCHAR(80) NOT NULL,
                ParamKey NVARCHAR(80) NOT NULL,
                DisplayName NVARCHAR(200) NOT NULL,
                DataType NVARCHAR(40) NOT NULL CONSTRAINT DF_GpsCmdParam_DataType DEFAULT (N'string'),
                IsRequired BIT NOT NULL CONSTRAINT DF_GpsCmdParam_Required DEFAULT (0),
                DefaultValue NVARCHAR(200) NULL,
                MinValue DECIMAL(18,4) NULL,
                MaxValue DECIMAL(18,4) NULL,
                SortOrder INT NOT NULL CONSTRAINT DF_GpsCmdParam_Sort DEFAULT (0),
                CONSTRAINT FK_GpsCmdParam_Definitions FOREIGN KEY (CommandKey) REFERENCES GpsCommandDefinitions(CommandKey),
                CONSTRAINT UQ_GpsCmdParam_CommandKey UNIQUE (CommandKey, ParamKey)
            );

            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'GpsCommandTemplates')
            CREATE TABLE GpsCommandTemplates (
                Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_GpsCommandTemplates PRIMARY KEY,
                TrackerModelId INT NOT NULL,
                CommandKey NVARCHAR(80) NOT NULL,
                Transport NVARCHAR(40) NOT NULL CONSTRAINT DF_GpsCmdTmpl_Transport DEFAULT (N'Auto'),
                PayloadTemplate NVARCHAR(MAX) NOT NULL,
                TraccarType NVARCHAR(80) NULL,
                AttributeJson NVARCHAR(MAX) NULL,
                ParserKey NVARCHAR(80) NULL,
                FirmwareMin NVARCHAR(40) NULL,
                FirmwareMax NVARCHAR(40) NULL,
                TemplateVersion INT NOT NULL CONSTRAINT DF_GpsCmdTmpl_Version DEFAULT (1),
                ValidFrom DATETIME2 NULL,
                ValidTo DATETIME2 NULL,
                IsActive BIT NOT NULL CONSTRAINT DF_GpsCmdTmpl_Active DEFAULT (1),
                CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_GpsCmdTmpl_CreatedAt DEFAULT (SYSUTCDATETIME()),
                UpdatedAt DATETIME2 NULL,
                CONSTRAINT FK_GpsCmdTmpl_Models FOREIGN KEY (TrackerModelId) REFERENCES TrackerModels(Id),
                CONSTRAINT FK_GpsCmdTmpl_Definitions FOREIGN KEY (CommandKey) REFERENCES GpsCommandDefinitions(CommandKey)
            );

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_GpsCommandTemplates_Model_Command' AND object_id = OBJECT_ID('GpsCommandTemplates'))
                CREATE INDEX IX_GpsCommandTemplates_Model_Command ON GpsCommandTemplates (TrackerModelId, CommandKey) WHERE IsActive = 1;
            """, cancellationToken: ct));
    }

    private static async Task SeedPermissionsAndMenuAsync(IDbConnection connection, CancellationToken ct)
    {
        if (await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT CASE WHEN EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Permissions') THEN 1 ELSE 0 END",
                cancellationToken: ct)) != 1)
            return;

        foreach (var (code, desc) in new[]
                 {
                     (PlatformPermissions.GpsManufacturersManage, "Manage GPS manufacturers"),
                     (PlatformPermissions.GpsModelsManage, "Manage GPS tracker models"),
                     (PlatformPermissions.GpsCommandsManage, "Manage GPS command library"),
                     (PlatformPermissions.GpsTemplatesManage, "Manage GPS command templates"),
                     (PlatformPermissions.GpsGatewaysManage, "Manage GPS transport gateways"),
                     (PlatformPermissions.GpsExecute, "Execute GPS control-center commands"),
                     (PlatformPermissions.GpsBulkExecute, "Bulk-execute GPS commands"),
                     (PlatformPermissions.GpsApprove, "Approve dangerous GPS commands"),
                     (PlatformPermissions.GpsHistoryView, "View GPS command history (platform)"),
                     (PlatformPermissions.GpsSimulatorUse, "Use GPS device simulator / testing console"),
                     (PlatformPermissions.GpsControlView, "View GPS Device Control Center"),
                 })
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF NOT EXISTS (SELECT 1 FROM Permissions WHERE PermissionCode = @Code)
                    INSERT INTO Permissions (ModuleName, PermissionCode, Description)
                    VALUES (N'Platform', @Code, @Description);
                """, new { Code = code, Description = desc }, cancellationToken: ct));
        }

        var codes = (await connection.QueryAsync<string>(new CommandDefinition(
            "SELECT PermissionCode FROM Permissions", cancellationToken: ct))).ToList();
        await TenantRolePermissionSeeder.AssignRolePermissionsForTenantAsync(
            connection, tenantId: 1, "SUPER_ADMIN", codes, ct);

        await connection.ExecuteAsync(new CommandDefinition("""
            IF EXISTS (SELECT 1 FROM PlatformModules WHERE ModuleKey = N'platform')
            AND NOT EXISTS (SELECT 1 FROM PlatformMenus WHERE Route = N'/platform/gps-control-center')
            BEGIN
                INSERT INTO PlatformMenus (ModuleId, ParentId, Name, Route, Icon, PermissionCode, SortOrder, IsActive,
                    DisplayName, Description, Category, Visible, FeatureKey, ModuleKey, IsMobileSupported, UpdatedAt)
                SELECT m.Id, NULL, N'GPS Control', N'/platform/gps-control-center', N'gps_fixed',
                       N'Platform.Gps.Control.View', 50, 1,
                       N'GPS Control Center', N'Manufacturers, models, commands, templates', N'Platform', 1,
                       N'gps-control', N'platform', 0, SYSUTCDATETIME()
                FROM PlatformModules m WHERE m.ModuleKey = N'platform';
            END
            """, cancellationToken: ct));
    }

    private static async Task SeedCapabilitiesAsync(IDbConnection connection, CancellationToken ct)
    {
        var caps = new (string Key, string Name, string Cat, int Sort)[]
        {
            ("EngineCut", "Engine Cut", "Security", 10),
            ("Relay", "Relay", "Security", 20),
            ("Sos", "SOS", "Security", 30),
            ("Defense", "Defense Mode", "Security", 40),
            ("Acc", "ACC / Ignition", "Sensors", 50),
            ("Door", "Door Sensor", "Sensors", 60),
            ("Fuel", "Fuel Sensor", "Sensors", 70),
            ("Temperature", "Temperature", "Sensors", 80),
            ("Mileage", "Mileage / Odometer", "Tracking", 90),
            ("Odometer", "Odometer", "Tracking", 95),
            ("Geofence", "On-device Geofence", "Tracking", 100),
            ("Overspeed", "Overspeed Alarm", "Alerts", 110),
            ("Crash", "Crash / Collision", "Alerts", 120),
            ("HarshBrake", "Harsh Brake / Accel", "Alerts", 130),
            ("TowAlarm", "Tow Alarm", "Alerts", 140),
            ("Camera", "Camera", "Media", 150),
            ("CanBus", "CAN Bus", "Vehicle", 160),
            ("Obd", "OBD", "Vehicle", 170),
            ("Ble", "BLE", "Connectivity", 180),
            ("DriverId", "Driver ID / RFID", "Identity", 190),
            ("Battery", "Battery Monitoring", "Power", 200),
            ("Ota", "OTA / Firmware", "Maintenance", 210),
            ("VoiceMonitor", "Voice Monitor", "Security", 220),
        };

        foreach (var c in caps)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF NOT EXISTS (SELECT 1 FROM GpsCapabilities WHERE CapabilityKey = @Key)
                    INSERT INTO GpsCapabilities (CapabilityKey, DisplayName, Category, SortOrder)
                    VALUES (@Key, @Name, @Cat, @Sort);
                """, new { c.Key, c.Name, c.Cat, c.Sort }, cancellationToken: ct));
        }
    }

    private static async Task SeedEv26rAndCommandsAsync(IDbConnection connection, CancellationToken ct)
    {
        // Ensure Jimi brand exists, then EV26R model
        var jimiId = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            "SELECT TOP 1 Id FROM TrackerBrands WHERE Name LIKE N'Jimi%' ORDER BY Id",
            cancellationToken: ct));
        if (jimiId is null)
        {
            jimiId = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
                INSERT INTO TrackerBrands (Name, VendorKey, DefaultProtocol, SupportsTraccar, SupportsSms, IsActive)
                OUTPUT INSERTED.Id
                VALUES (N'Jimi IoT', N'jimi', N'jimi', 1, 1, 1);
                """, cancellationToken: ct));
        }
        else
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE TrackerBrands SET VendorKey = COALESCE(VendorKey, N'jimi'),
                    DefaultProtocol = COALESCE(DefaultProtocol, N'jimi'),
                    SupportsTraccar = COALESCE(SupportsTraccar, 1),
                    SupportsSms = COALESCE(SupportsSms, 1)
                WHERE Id = @Id;
                """, new { Id = jimiId }, cancellationToken: ct));
        }

        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM TrackerModels WHERE CatalogKey = N'jimi_ev26r')
                INSERT INTO TrackerModels (
                    TrackerBrandId, Name, CatalogKey, Protocol, ProtocolLabel, DefaultPort,
                    SupportsEngineCutOff, SupportsRelay, SupportsIgnition, SupportsBatteryMonitoring,
                    FirmwareHint, Description, IsActive)
                VALUES (
                    @BrandId, N'EV26R', N'jimi_ev26r', N'gt06', N'GT06', 5023,
                    1, 1, 1, 1, N'1.0+', N'Jimi EV26R GPS tracker', 1);
            """, new { BrandId = jimiId }, cancellationToken: ct));

        var commands = new (string Key, string Name, string Cat, string? Cap, string Danger, bool Approval, bool Reason, int Sort)[]
        {
            ("engineStop", "Engine Stop", "Operations", "EngineCut", "Critical", true, true, 10),
            ("engineResume", "Engine Resume", "Operations", "EngineCut", "High", true, true, 20),
            ("positionSingle", "Request Position", "Operations", null, "Low", false, false, 30),
            ("restart", "Restart Device", "Operations", null, "Medium", false, true, 40),
            ("relayOn", "Relay ON", "Operations", "Relay", "High", true, true, 50),
            ("relayOff", "Relay OFF", "Operations", "Relay", "High", true, true, 60),
            ("status", "Device Status", "Diagnostics", null, "Low", false, false, 70),
            ("version", "Firmware Version", "Diagnostics", null, "Low", false, false, 80),
            ("iccid", "SIM ICCID", "Diagnostics", null, "Low", false, false, 90),
            ("imsi", "SIM IMSI", "Diagnostics", null, "Low", false, false, 100),
            ("param", "Basic Parameters", "Diagnostics", null, "Low", false, false, 110),
            ("battery", "Battery Info", "Diagnostics", "Battery", "Low", false, false, 120),
            ("signal", "Network / Signal", "Diagnostics", null, "Low", false, false, 130),
            ("apn", "Set APN", "Installation", null, "High", true, true, 140),
            ("server", "Server Settings", "Installation", null, "Critical", true, true, 150),
            ("heartbeat", "Heartbeat Interval", "Installation", null, "Medium", false, false, 160),
            ("timezone", "Timezone", "Installation", null, "Medium", false, false, 170),
            ("buzzer", "Buzzer", "Operations", null, "Low", false, false, 180),
            ("custom", "Custom Command", "Maintenance", null, "High", true, true, 900),
            ("customSms", "Custom SMS", "Maintenance", null, "High", true, true, 910),
        };

        foreach (var c in commands)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF NOT EXISTS (SELECT 1 FROM GpsCommandDefinitions WHERE CommandKey = @Key)
                    INSERT INTO GpsCommandDefinitions
                        (CommandKey, DisplayName, Category, RequiredCapabilityKey, DangerLevel, RequiresApproval, RequiresReason, SortOrder)
                    VALUES (@Key, @Name, @Cat, @Cap, @Danger, @Approval, @Reason, @Sort);
                """, new { c.Key, c.Name, c.Cat, c.Cap, c.Danger, c.Approval, c.Reason, c.Sort }, cancellationToken: ct));
        }

        // Parameters for heartbeat / apn / server / timezone
        await UpsertParamAsync(connection, "heartbeat", "intervalSeconds", "Interval (seconds)", "int", true, "60", 10, 3600, 1, ct);
        await UpsertParamAsync(connection, "apn", "apn", "APN", "string", true, null, null, null, 1, ct);
        await UpsertParamAsync(connection, "apn", "user", "Username", "string", false, null, null, null, 2, ct);
        await UpsertParamAsync(connection, "apn", "password", "Password", "string", false, null, null, null, 3, ct);
        await UpsertParamAsync(connection, "server", "host", "Server host", "string", true, null, null, null, 1, ct);
        await UpsertParamAsync(connection, "server", "port", "Port", "int", true, "5023", 1, 65535, 2, ct);
        await UpsertParamAsync(connection, "timezone", "offsetHours", "UTC offset (hours)", "int", true, "5", -12, 14, 1, ct);
        await UpsertParamAsync(connection, "custom", "payload", "Raw payload", "string", true, null, null, null, 1, ct);
        await UpsertParamAsync(connection, "customSms", "phone", "Phone", "string", true, null, null, null, 1, ct);
        await UpsertParamAsync(connection, "customSms", "message", "SMS body", "string", true, null, null, null, 2, ct);

        var modelId = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            "SELECT Id FROM TrackerModels WHERE CatalogKey = N'jimi_ev26r'", cancellationToken: ct));
        if (modelId is null) return;

        var templates = new (string Cmd, string Transport, string Payload, string? Traccar, string? Parser)[]
        {
            ("engineStop", "Traccar", "engineStop", "engineStop", null),
            ("engineResume", "Traccar", "engineResume", "engineResume", null),
            ("positionSingle", "Traccar", "positionSingle", "positionSingle", null),
            ("restart", "Auto", "RESET#", "rebootDevice", null),
            ("relayOn", "Auto", "RELAY,1#", "outputControl", null),
            ("relayOff", "Auto", "RELAY,0#", "outputControl", null),
            ("status", "Sms", "STATUS#", null, "status"),
            ("version", "Sms", "VERSION#", null, "version"),
            ("iccid", "Sms", "ICCID#", null, "iccid"),
            ("imsi", "Sms", "IMSI#", null, "imsi"),
            ("param", "Sms", "PARAM#", null, "param"),
            ("battery", "Sms", "STATUS#", null, "status"),
            ("signal", "Sms", "GPRSSET#", null, "signal"),
            ("apn", "Sms", "APN,{{apn}},{{user}},{{password}}#", null, null),
            ("server", "Sms", "SERVER,0,{{host}},{{port}}#", null, null),
            ("heartbeat", "Sms", "HBT,{{intervalSeconds}},{{intervalSeconds}}#", null, null),
            ("timezone", "Sms", "GMT,E,{{offsetHours}},0#", null, null),
            ("buzzer", "Traccar", "custom", "custom", null),
            ("custom", "Auto", "{{payload}}", "custom", null),
            ("customSms", "Sms", "{{message}}", null, null),
        };

        foreach (var t in templates)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF NOT EXISTS (
                    SELECT 1 FROM GpsCommandTemplates
                    WHERE TrackerModelId = @ModelId AND CommandKey = @Cmd AND TemplateVersion = 1)
                    INSERT INTO GpsCommandTemplates
                        (TrackerModelId, CommandKey, Transport, PayloadTemplate, TraccarType, ParserKey, TemplateVersion)
                    VALUES (@ModelId, @Cmd, @Transport, @Payload, @Traccar, @Parser, 1);
                """, new { ModelId = modelId, t.Cmd, t.Transport, Payload = t.Payload, Traccar = t.Traccar, Parser = t.Parser },
                cancellationToken: ct));
        }

        // Also seed Traccar-first templates for GT06-family models that already exist
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO GpsCommandTemplates (TrackerModelId, CommandKey, Transport, PayloadTemplate, TraccarType, TemplateVersion)
            SELECT m.Id, d.CommandKey, N'Traccar',
                   CASE d.CommandKey
                       WHEN N'restart' THEN N'RESET#'
                       WHEN N'relayOn' THEN N'RELAY,1#'
                       WHEN N'relayOff' THEN N'RELAY,0#'
                       ELSE d.CommandKey END,
                   CASE d.CommandKey
                       WHEN N'engineStop' THEN N'engineStop'
                       WHEN N'engineResume' THEN N'engineResume'
                       WHEN N'positionSingle' THEN N'positionSingle'
                       WHEN N'restart' THEN N'rebootDevice'
                       WHEN N'relayOn' THEN N'outputControl'
                       WHEN N'relayOff' THEN N'outputControl'
                       WHEN N'buzzer' THEN N'custom'
                       WHEN N'custom' THEN N'custom'
                       ELSE NULL END,
                   1
            FROM TrackerModels m
            CROSS JOIN GpsCommandDefinitions d
            WHERE m.CatalogKey IN (N'jimi_gt06', N'concox_gt06n', N'sinotrack_st901', N'jimi_vg03')
              AND d.CommandKey IN (N'engineStop', N'engineResume', N'positionSingle', N'restart', N'relayOn', N'relayOff', N'buzzer', N'custom')
              AND NOT EXISTS (
                  SELECT 1 FROM GpsCommandTemplates t
                  WHERE t.TrackerModelId = m.Id AND t.CommandKey = d.CommandKey AND t.TemplateVersion = 1);
            """, cancellationToken: ct));
    }

    private static async Task UpsertParamAsync(
        IDbConnection connection, string cmd, string key, string name, string type,
        bool required, string? def, decimal? min, decimal? max, int sort, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM GpsCommandParameterDefinitions WHERE CommandKey = @Cmd AND ParamKey = @Key)
                INSERT INTO GpsCommandParameterDefinitions
                    (CommandKey, ParamKey, DisplayName, DataType, IsRequired, DefaultValue, MinValue, MaxValue, SortOrder)
                VALUES (@Cmd, @Key, @Name, @Type, @Required, @Def, @Min, @Max, @Sort);
            """, new { Cmd = cmd, Key = key, Name = name, Type = type, Required = required, Def = def, Min = min, Max = max, Sort = sort },
            cancellationToken: ct));
    }

    private static async Task BackfillModelCapabilitiesAsync(IDbConnection connection, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO TrackerModelCapabilities (TrackerModelId, CapabilityKey, IsEnabled)
            SELECT m.Id, N'EngineCut', 1 FROM TrackerModels m
            WHERE m.SupportsEngineCutOff = 1
              AND NOT EXISTS (SELECT 1 FROM TrackerModelCapabilities c WHERE c.TrackerModelId = m.Id AND c.CapabilityKey = N'EngineCut');

            INSERT INTO TrackerModelCapabilities (TrackerModelId, CapabilityKey, IsEnabled)
            SELECT m.Id, N'Relay', 1 FROM TrackerModels m
            WHERE m.SupportsRelay = 1
              AND NOT EXISTS (SELECT 1 FROM TrackerModelCapabilities c WHERE c.TrackerModelId = m.Id AND c.CapabilityKey = N'Relay');

            INSERT INTO TrackerModelCapabilities (TrackerModelId, CapabilityKey, IsEnabled)
            SELECT m.Id, N'Acc', 1 FROM TrackerModels m
            WHERE m.SupportsIgnition = 1
              AND NOT EXISTS (SELECT 1 FROM TrackerModelCapabilities c WHERE c.TrackerModelId = m.Id AND c.CapabilityKey = N'Acc');

            INSERT INTO TrackerModelCapabilities (TrackerModelId, CapabilityKey, IsEnabled)
            SELECT m.Id, N'Battery', 1 FROM TrackerModels m
            WHERE m.SupportsBatteryMonitoring = 1
              AND NOT EXISTS (SELECT 1 FROM TrackerModelCapabilities c WHERE c.TrackerModelId = m.Id AND c.CapabilityKey = N'Battery');

            INSERT INTO TrackerModelCapabilities (TrackerModelId, CapabilityKey, IsEnabled)
            SELECT m.Id, N'Fuel', 1 FROM TrackerModels m
            WHERE m.SupportsFuelSensor = 1
              AND NOT EXISTS (SELECT 1 FROM TrackerModelCapabilities c WHERE c.TrackerModelId = m.Id AND c.CapabilityKey = N'Fuel');
            """, cancellationToken: ct));
    }
}
