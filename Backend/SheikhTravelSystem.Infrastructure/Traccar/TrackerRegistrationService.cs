using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.GpsTracking.Trackers.Queries;
using SheikhTravelSystem.Application.Features.GpsTracking.Traccar;
using SheikhTravelSystem.Application.Features.GpsTracking.Trackers;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Infrastructure.Traccar;

public sealed class TrackerRegistrationService(
    IDbConnectionFactory dbFactory,
    ITenantContext tenantContext,
    ICurrentUserService currentUser,
    ITraccarClient traccar,
    IOptions<TraccarOptions> traccarOptions,
    ILogger<TrackerRegistrationService> logger) : ITrackerRegistrationService
{
    public async Task<ApiResponse<TrackerRegisteredDto>> RegisterAsync(RegisterTrackerDto dto, CancellationToken ct = default)
    {
        if (!TrackerCatalog.ValidCategories.Contains(dto.Category))
            return ApiResponse<TrackerRegisteredDto>.FailResponse("Invalid device category.");

        using var connection = dbFactory.CreateConnection();

        var model = await ResolveModelAsync(connection, dto.TrackerModelId, dto.TrackerModelKey, ct);
        if (model is null)
            return ApiResponse<TrackerRegisteredDto>.FailResponse("Invalid tracker model.");

        var duplicate = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT CASE WHEN EXISTS(SELECT 1 FROM GpsDevices WHERE UniqueId = @UniqueId AND IsDeleted = 0) THEN 1 ELSE 0 END",
            new { dto.UniqueId }, cancellationToken: ct));
        if (duplicate)
            return ApiResponse<TrackerRegisteredDto>.FailResponse("IMEI already registered.");

        if (dto.VehicleId.HasValue)
        {
            var vehicleError = await ValidateVehicleAsync(connection, dto.VehicleId.Value, ct);
            if (vehicleError is not null)
                return ApiResponse<TrackerRegisteredDto>.FailResponse(vehicleError);
        }

        if (dto.DriverId.HasValue)
        {
            var driverError = await ValidateDriverAsync(connection, dto.DriverId.Value, ct);
            if (driverError is not null)
                return ApiResponse<TrackerRegisteredDto>.FailResponse(driverError);
        }

        var opts = traccarOptions.Value;
        if (!opts.IsConfigured || !opts.Enabled)
            return ApiResponse<TrackerRegisteredDto>.FailResponse(
                "Traccar is not configured. Set Traccar:BaseUrl, credentials, and Enabled=true before registering trackers.");

        var existingTraccar = await traccar.GetDeviceByUniqueIdAsync(dto.UniqueId, ct);
        if (existingTraccar is not null)
            return ApiResponse<TrackerRegisteredDto>.FailResponse("IMEI already exists on Traccar server.");

        var traccarPayload = new TraccarDevicePayload(
            dto.Name,
            dto.UniqueId,
            dto.Category.ToLowerInvariant(),
            dto.Phone,
            model.Name,
            dto.Contact,
            dto.Disabled);

        var traccarResult = await traccar.CreateDeviceAsync(traccarPayload, ct);
        if (!traccarResult.Success || traccarResult.Value is null)
        {
            logger.LogWarning("Traccar registration failed for IMEI {Imei}: {Error}", dto.UniqueId, traccarResult.ErrorMessage);
            return ApiResponse<TrackerRegisteredDto>.FailResponse(
                traccarResult.ErrorMessage ?? "Failed to create device in Traccar.");
        }

        var traccarDeviceId = traccarResult.Value.Id;
        var tenantId = tenantContext.TenantId;
        var createdBy = currentUser.UserId?.ToString();

        try
        {
            var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                @"INSERT INTO GpsDevices (
                    TenantId, VehicleId, DriverId, UniqueId, Name, Category, Phone, Contact, Disabled,
                    Protocol, Model, Vendor, TrackerModelKey, TrackerModelId, SupportsEngineCutoff, RelayOutput,
                    SerialNumber, InstallationDate, InstalledBy, InstallationNotes,
                    CountryCode, SIMProvider, SIMPackage, MonthlySIMCost,
                    WarrantyStart, WarrantyEnd, PurchaseDate, PurchasePrice, CurrentStatus,
                    TraccarDeviceId, IsActive, CreatedAt, CreatedBy, IsDeleted)
                  OUTPUT INSERTED.Id
                  VALUES (
                    @TenantId, @VehicleId, @DriverId, @UniqueId, @Name, @Category, @Phone, @Contact, @Disabled,
                    @Protocol, @Model, @Vendor, @TrackerModelKey, @TrackerModelId, @SupportsEngineCutoff, @RelayOutput,
                    @SerialNumber, @InstallationDate, @InstalledBy, @InstallationNotes,
                    @CountryCode, @SIMProvider, @SIMPackage, @MonthlySIMCost,
                    @WarrantyStart, @WarrantyEnd, @PurchaseDate, @PurchasePrice, @CurrentStatus,
                    @TraccarDeviceId, 1, GETUTCDATE(), @CreatedBy, 0)",
                BuildInsertParams(dto, model, traccarDeviceId, tenantId, createdBy),
                cancellationToken: ct));

            var vehicleInfo = await GetVehicleInfoAsync(connection, dto.VehicleId, ct);

            return ApiResponse<TrackerRegisteredDto>.SuccessResponse(
                new TrackerRegisteredDto(
                    id,
                    dto.Name,
                    dto.UniqueId,
                    model.ProtocolLabel,
                    string.IsNullOrWhiteSpace(vehicleInfo.Name) ? null : vehicleInfo.Name,
                    string.IsNullOrWhiteSpace(vehicleInfo.Plate) ? null : vehicleInfo.Plate,
                    traccarDeviceId,
                    "Waiting for first GPS signal"),
                "Tracker registered successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SheikhGo tracker insert failed after Traccar create {TraccarId}; rolling back", traccarDeviceId);
            await traccar.DeleteDeviceAsync(traccarDeviceId, ct);
            return ApiResponse<TrackerRegisteredDto>.FailResponse("Failed to save tracker. Traccar device was rolled back.");
        }
    }

    public async Task<ApiResponse<bool>> UpdateAsync(int id, UpdateTrackerDto dto, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();

        var model = await ResolveModelAsync(connection, dto.TrackerModelId, dto.TrackerModelKey, ct);
        if (model is null)
            return ApiResponse<bool>.FailResponse("Invalid tracker model.");

        var existing = await connection.QueryFirstOrDefaultAsync<(int Id, int? TraccarDeviceId, string UniqueId)>(
            new CommandDefinition(
                "SELECT Id, TraccarDeviceId, UniqueId FROM GpsDevices WHERE Id = @Id AND IsDeleted = 0",
                new { Id = id }, cancellationToken: ct));

        if (existing.Id == 0)
            return ApiResponse<bool>.FailResponse("Tracker not found.");

        if (dto.VehicleId.HasValue)
        {
            var vehicleError = await ValidateVehicleAsync(connection, dto.VehicleId.Value, ct);
            if (vehicleError is not null)
                return ApiResponse<bool>.FailResponse(vehicleError);
        }

        if (dto.DriverId.HasValue)
        {
            var driverError = await ValidateDriverAsync(connection, dto.DriverId.Value, ct);
            if (driverError is not null)
                return ApiResponse<bool>.FailResponse(driverError);
        }

        var opts = traccarOptions.Value;
        if (opts.IsConfigured && opts.Enabled && existing.TraccarDeviceId.HasValue)
        {
            var updateResult = await traccar.UpdateDeviceAsync(new TraccarUpdateDevicePayload(
                existing.TraccarDeviceId.Value,
                dto.Name,
                existing.UniqueId,
                dto.Category.ToLowerInvariant(),
                dto.Phone,
                model.Name,
                dto.Contact,
                dto.Disabled), ct);

            if (!updateResult.Success)
                return ApiResponse<bool>.FailResponse(updateResult.ErrorMessage ?? "Traccar sync failed.");
        }

        var rows = await connection.ExecuteAsync(new CommandDefinition(
            @"UPDATE GpsDevices SET
                VehicleId = @VehicleId, DriverId = @DriverId, Name = @Name,
                Category = @Category, Phone = @Phone, Contact = @Contact, Disabled = @Disabled,
                Protocol = @Protocol, Model = @Model, Vendor = @Vendor,
                TrackerModelKey = @TrackerModelKey, TrackerModelId = @TrackerModelId,
                SupportsEngineCutoff = @SupportsEngineCutoff, RelayOutput = @RelayOutput,
                SerialNumber = @SerialNumber, InstallationDate = @InstallationDate,
                InstalledBy = @InstalledBy, InstallationNotes = @InstallationNotes,
                CountryCode = @CountryCode, SIMProvider = @SIMProvider, SIMPackage = @SIMPackage,
                MonthlySIMCost = @MonthlySIMCost, WarrantyStart = @WarrantyStart, WarrantyEnd = @WarrantyEnd,
                PurchaseDate = @PurchaseDate, PurchasePrice = @PurchasePrice, CurrentStatus = @CurrentStatus,
                IsActive = @IsActive, UpdatedAt = GETUTCDATE(), UpdatedBy = @UpdatedBy
              WHERE Id = @Id AND IsDeleted = 0",
            new
            {
                Id = id,
                dto.VehicleId,
                dto.DriverId,
                dto.Name,
                Category = dto.Category.ToLowerInvariant(),
                dto.Phone,
                dto.Contact,
                dto.Disabled,
                Protocol = model.Protocol,
                Model = FormatModelLabel(model),
                Vendor = model.BrandName,
                TrackerModelKey = model.CatalogKey,
                TrackerModelId = model.Id,
                dto.SupportsEngineCutoff,
                RelayOutput = dto.SupportsEngineCutoff ? dto.RelayOutput : null,
                dto.SerialNumber,
                dto.InstallationDate,
                dto.InstalledBy,
                dto.InstallationNotes,
                dto.CountryCode,
                dto.SIMProvider,
                dto.SIMPackage,
                dto.MonthlySIMCost,
                dto.WarrantyStart,
                dto.WarrantyEnd,
                dto.PurchaseDate,
                dto.PurchasePrice,
                dto.CurrentStatus,
                dto.IsActive,
                UpdatedBy = currentUser.UserId?.ToString()
            },
            cancellationToken: ct));

        return rows == 0
            ? ApiResponse<bool>.FailResponse("Tracker not found.")
            : ApiResponse<bool>.SuccessResponse(true, "Tracker updated.");
    }

    public async Task<ApiResponse<bool>> DeleteAsync(int id, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();

        var existing = await connection.QueryFirstOrDefaultAsync<(int Id, int? TraccarDeviceId)>(
            new CommandDefinition(
                "SELECT Id, TraccarDeviceId FROM GpsDevices WHERE Id = @Id AND IsDeleted = 0",
                new { Id = id }, cancellationToken: ct));

        if (existing.Id == 0)
            return ApiResponse<bool>.FailResponse("Tracker not found.");

        var rows = await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE GpsDevices SET IsDeleted = 1, UpdatedAt = GETUTCDATE() WHERE Id = @Id",
            new { Id = id }, cancellationToken: ct));

        if (rows == 0)
            return ApiResponse<bool>.FailResponse("Tracker not found.");

        var opts = traccarOptions.Value;
        if (opts.IsConfigured && opts.Enabled && existing.TraccarDeviceId.HasValue)
            await traccar.DeleteDeviceAsync(existing.TraccarDeviceId.Value, ct);

        return ApiResponse<bool>.SuccessResponse(true, "Tracker removed.");
    }

    private static object BuildInsertParams(
        RegisterTrackerDto dto, TrackerModelRecord model, int traccarDeviceId, int? tenantId, string? createdBy) => new
    {
        TenantId = tenantId,
        dto.VehicleId,
        dto.DriverId,
        dto.UniqueId,
        dto.Name,
        Category = dto.Category.ToLowerInvariant(),
        dto.Phone,
        dto.Contact,
        dto.Disabled,
        Protocol = model.Protocol,
        Model = FormatModelLabel(model),
        Vendor = model.BrandName,
        TrackerModelKey = model.CatalogKey,
        TrackerModelId = model.Id,
        dto.SupportsEngineCutoff,
        RelayOutput = dto.SupportsEngineCutoff ? dto.RelayOutput : null,
        dto.SerialNumber,
        dto.InstallationDate,
        dto.InstalledBy,
        dto.InstallationNotes,
        dto.CountryCode,
        dto.SIMProvider,
        dto.SIMPackage,
        dto.MonthlySIMCost,
        dto.WarrantyStart,
        dto.WarrantyEnd,
        dto.PurchaseDate,
        dto.PurchasePrice,
        CurrentStatus = dto.CurrentStatus ?? "Installed",
        TraccarDeviceId = traccarDeviceId,
        CreatedBy = createdBy
    };

    private static async Task<TrackerModelRecord?> ResolveModelAsync(
        System.Data.IDbConnection connection, int modelId, string? catalogKey, CancellationToken ct)
    {
        if (modelId > 0)
        {
            return await connection.QueryFirstOrDefaultAsync<TrackerModelRecord>(new CommandDefinition(
                TrackerCatalogSql.ModelById,
                new { Id = modelId },
                cancellationToken: ct));
        }

        if (!string.IsNullOrWhiteSpace(catalogKey))
        {
            return await connection.QueryFirstOrDefaultAsync<TrackerModelRecord>(new CommandDefinition(
                TrackerCatalogSql.ModelByCatalogKey,
                new { CatalogKey = catalogKey },
                cancellationToken: ct));
        }

        return null;
    }

    private static string FormatModelLabel(TrackerModelRecord model)
        => $"{model.BrandName} {model.Name}";

    private async Task<string?> ValidateVehicleAsync(System.Data.IDbConnection connection, int vehicleId, CancellationToken ct)
    {
        var status = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            "SELECT Status FROM Vehicles WHERE Id = @Id AND IsDeleted = 0",
            new { Id = vehicleId }, cancellationToken: ct));

        if (status is null)
            return "Vehicle not found.";

        if (status == (int)VehicleStatus.Draft)
            return "Cannot link a tracker to a draft vehicle. Complete the vehicle first.";

        return null;
    }

    private async Task<string?> ValidateDriverAsync(System.Data.IDbConnection connection, int driverId, CancellationToken ct)
    {
        var exists = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT CASE WHEN EXISTS(SELECT 1 FROM Drivers WHERE Id = @Id AND IsDeleted = 0) THEN 1 ELSE 0 END",
            new { Id = driverId }, cancellationToken: ct));

        return exists ? null : "Driver not found.";
    }

    private static async Task<(string? Name, string? Plate)> GetVehicleInfoAsync(
        System.Data.IDbConnection connection, int? vehicleId, CancellationToken ct)
    {
        if (!vehicleId.HasValue) return (null, null);

        return await connection.QueryFirstOrDefaultAsync<(string Name, string Plate)>(new CommandDefinition(
            @"SELECT Name, RegistrationNumber AS Plate FROM Vehicles
              WHERE Id = @Id AND IsDeleted = 0 AND Status <> 5",
            new { Id = vehicleId.Value }, cancellationToken: ct));
    }
}
