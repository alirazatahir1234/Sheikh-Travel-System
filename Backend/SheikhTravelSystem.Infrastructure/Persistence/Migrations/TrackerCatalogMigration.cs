using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

public static class TrackerCatalogMigration
{
    public static async Task ApplyAsync(IDbConnectionFactory dbFactory, ILogger logger, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        try
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'TrackerBrands')
                CREATE TABLE TrackerBrands (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    Name NVARCHAR(100) NOT NULL,
                    LogoUrl NVARCHAR(500) NULL,
                    Website NVARCHAR(200) NULL,
                    Description NVARCHAR(500) NULL,
                    IsActive BIT NOT NULL CONSTRAINT DF_TrackerBrands_IsActive DEFAULT 1,
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_TrackerBrands_CreatedAt DEFAULT GETUTCDATE()
                );

                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'TrackerModels')
                CREATE TABLE TrackerModels (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    TrackerBrandId INT NOT NULL,
                    Name NVARCHAR(100) NOT NULL,
                    CatalogKey NVARCHAR(50) NULL,
                    Protocol NVARCHAR(50) NOT NULL,
                    ProtocolLabel NVARCHAR(100) NOT NULL,
                    DefaultPort INT NOT NULL CONSTRAINT DF_TrackerModels_DefaultPort DEFAULT 0,
                    SupportsEngineCutOff BIT NOT NULL CONSTRAINT DF_TrackerModels_EngineCutOff DEFAULT 0,
                    SupportsFuelSensor BIT NOT NULL CONSTRAINT DF_TrackerModels_Fuel DEFAULT 0,
                    SupportsTemperatureSensor BIT NOT NULL CONSTRAINT DF_TrackerModels_Temp DEFAULT 0,
                    SupportsDriverIdentification BIT NOT NULL CONSTRAINT DF_TrackerModels_DriverId DEFAULT 0,
                    SupportsCanBus BIT NOT NULL CONSTRAINT DF_TrackerModels_CanBus DEFAULT 0,
                    SupportsObd BIT NOT NULL CONSTRAINT DF_TrackerModels_Obd DEFAULT 0,
                    SupportsBle BIT NOT NULL CONSTRAINT DF_TrackerModels_Ble DEFAULT 0,
                    SupportsCamera BIT NOT NULL CONSTRAINT DF_TrackerModels_Camera DEFAULT 0,
                    SupportsRelay BIT NOT NULL CONSTRAINT DF_TrackerModels_Relay DEFAULT 0,
                    SupportsDoorSensor BIT NOT NULL CONSTRAINT DF_TrackerModels_Door DEFAULT 0,
                    SupportsIgnition BIT NOT NULL CONSTRAINT DF_TrackerModels_Ignition DEFAULT 1,
                    SupportsOdometer BIT NOT NULL CONSTRAINT DF_TrackerModels_Odometer DEFAULT 0,
                    SupportsBatteryMonitoring BIT NOT NULL CONSTRAINT DF_TrackerModels_Battery DEFAULT 0,
                    Description NVARCHAR(500) NULL,
                    IsActive BIT NOT NULL CONSTRAINT DF_TrackerModels_IsActive DEFAULT 1,
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_TrackerModels_CreatedAt DEFAULT GETUTCDATE(),
                    CONSTRAINT FK_TrackerModels_Brands FOREIGN KEY (TrackerBrandId) REFERENCES TrackerBrands(Id)
                );

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UQ_TrackerModels_CatalogKey' AND object_id = OBJECT_ID('TrackerModels'))
                    CREATE UNIQUE INDEX UQ_TrackerModels_CatalogKey ON TrackerModels(CatalogKey) WHERE CatalogKey IS NOT NULL;

                IF COL_LENGTH('GpsDevices', 'TrackerModelId') IS NULL
                    ALTER TABLE GpsDevices ADD TrackerModelId INT NULL;

                IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_GpsDevices_TrackerModels')
                    ALTER TABLE GpsDevices ADD CONSTRAINT FK_GpsDevices_TrackerModels
                        FOREIGN KEY (TrackerModelId) REFERENCES TrackerModels(Id);
                """, cancellationToken: cancellationToken));

            await SeedCatalogAsync(connection, cancellationToken);

            logger.LogInformation("TrackerCatalogMigration applied successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "TrackerCatalogMigration failed.");
            throw;
        }
    }

    private static async Task SeedCatalogAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        var brands = new (string Name, string Key, (string Name, string CatalogKey, string Protocol, string ProtocolLabel, int Port, bool EngineCutOff, bool Relay)[] Models)[]
        {
            ("Jimi IoT", "jimi", [
                ("VG03", "jimi_vg03", "jimi", "Jimi", 5000, true, true),
                ("VL802", "jimi_vl802", "jimi", "Jimi", 5000, false, false),
                ("VL103M", "jimi_vl103m", "jimi", "Jimi", 5000, false, false),
                ("GT06", "jimi_gt06", "gt06", "GT06", 5023, true, true),
            ]),
            ("Teltonika", "teltonika", [
                ("FMB920", "teltonika_fmb920", "teltonika", "Teltonika Binary", 5027, true, true),
                ("FMB125", "teltonika_fmb125", "teltonika", "Teltonika Binary", 5027, true, true),
                ("FMB140", "teltonika_fmb140", "teltonika", "Teltonika Binary", 5027, true, true),
                ("FMB001", "teltonika_fmb001", "teltonika", "Teltonika Binary", 5027, false, false),
                ("FMC920", "teltonika_fmc920", "teltonika", "Teltonika Binary", 5027, true, true),
                ("FMC130", "teltonika_fmc130", "teltonika", "Teltonika Binary", 5027, true, true),
                ("FMM130", "teltonika_fmm130", "teltonika", "Teltonika Binary", 5027, true, true),
                ("FMC001", "teltonika_fmc001", "teltonika", "Teltonika Binary", 5027, false, false),
            ]),
            ("Queclink", "queclink", [
                ("GV300", "queclink_gv300", "gl200", "GL200", 5024, true, true),
                ("GV350", "queclink_gv350", "gl200", "GL200", 5024, true, true),
                ("GV500", "queclink_gv500", "gl200", "GL200", 5024, true, true),
                ("GL300", "queclink_gl300", "gl200", "GL200", 5024, false, false),
                ("GV75", "queclink_gv75", "gl200", "GL200", 5024, true, true),
            ]),
            ("Concox", "concox", [
                ("GT06N", "concox_gt06n", "gt06", "GT06", 5023, true, true),
                ("JM01", "concox_jm01", "gt06", "GT06", 5023, false, false),
                ("AT6", "concox_at6", "gt06", "GT06", 5023, true, true),
            ]),
            ("Meitrack", "meitrack", [
                ("T366", "meitrack_t366", "meitrack", "Meitrack", 5020, true, true),
            ]),
            ("Ruptela", "ruptela", [
                ("FM-Eco4", "ruptela_fm_eco4", "ruptela", "Ruptela", 5046, true, true),
            ]),
            ("Sinotrack", "sinotrack", [
                ("ST-901", "sinotrack_st901", "gt06", "GT06", 5023, true, true),
            ]),
            ("Coban", "coban", [
                ("TK103", "coban_tk103", "gt06", "GT06", 5023, true, true),
            ]),
            ("Eelink", "eelink", [
                ("GPT06", "eelink_gpt06", "gt06", "GT06", 5023, true, true),
            ]),
        };

        foreach (var brand in brands)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF NOT EXISTS (SELECT 1 FROM TrackerBrands WHERE Name = @Name)
                    INSERT INTO TrackerBrands (Name, IsActive) VALUES (@Name, 1);
                """, new { brand.Name }, cancellationToken: ct));

            var brandId = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT Id FROM TrackerBrands WHERE Name = @Name",
                new { brand.Name }, cancellationToken: ct));

            foreach (var model in brand.Models)
            {
                await connection.ExecuteAsync(new CommandDefinition("""
                    IF NOT EXISTS (SELECT 1 FROM TrackerModels WHERE CatalogKey = @CatalogKey)
                        INSERT INTO TrackerModels (
                            TrackerBrandId, Name, CatalogKey, Protocol, ProtocolLabel, DefaultPort,
                            SupportsEngineCutOff, SupportsRelay, IsActive)
                        VALUES (
                            @BrandId, @Name, @CatalogKey, @Protocol, @ProtocolLabel, @Port,
                            @EngineCutOff, @Relay, 1);
                    ELSE
                        UPDATE TrackerModels SET
                            TrackerBrandId = @BrandId,
                            Name = @Name,
                            Protocol = @Protocol,
                            ProtocolLabel = @ProtocolLabel,
                            DefaultPort = @Port,
                            SupportsEngineCutOff = @EngineCutOff,
                            SupportsRelay = @Relay
                        WHERE CatalogKey = @CatalogKey;
                    """, new
                {
                    BrandId = brandId,
                    model.Name,
                    model.CatalogKey,
                    model.Protocol,
                    model.ProtocolLabel,
                    Port = model.Port,
                    EngineCutOff = model.EngineCutOff,
                    Relay = model.Relay
                }, cancellationToken: ct));
            }
        }

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE d SET d.TrackerModelId = m.Id
            FROM GpsDevices d
            INNER JOIN TrackerModels m ON m.CatalogKey = d.TrackerModelKey
            WHERE d.TrackerModelId IS NULL AND d.TrackerModelKey IS NOT NULL;
            """, cancellationToken: ct));
    }
}
