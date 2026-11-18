using System.Data;
using Dapper;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Infrastructure.Services.Gps;

public sealed class GpsPositionIngestionHelper : IGpsPositionIngestionHelper
{
    public async Task<int?> ResolveActiveBookingIdAsync(
        IDbConnection connection,
        int tenantId,
        int vehicleId,
        int? explicitBookingId,
        CancellationToken cancellationToken)
    {
        if (explicitBookingId.HasValue)
        {
            // SG-01 Step 6a: an explicit booking is used only when it is this tenant's booking.
            return await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
                @"SELECT Id FROM Bookings WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
                new { Id = explicitBookingId.Value, TenantId = tenantId },
                cancellationToken: cancellationToken));
        }

        var confirmed = (int)BookingStatus.Confirmed;
        var started = (int)BookingStatus.Started;

        return await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            @"SELECT TOP 1 Id FROM Bookings
              WHERE VehicleId = @VehicleId AND TenantId = @TenantId AND IsDeleted = 0
              AND Status IN (@Confirmed, @Started)
              AND PickupTime <= DATEADD(HOUR, 2, GETUTCDATE())
              AND (DropoffTime IS NULL OR DropoffTime >= DATEADD(HOUR, -1, GETUTCDATE()))
              ORDER BY PickupTime DESC",
            new { VehicleId = vehicleId, TenantId = tenantId, Confirmed = confirmed, Started = started },
            cancellationToken: cancellationToken));
    }

    public async Task IngestAsync(
        IDbConnection connection,
        IngestPositionDto historyDto,
        DateTime recordedAt,
        GpsPositionIngestPlan? plan = null,
        IngestPositionDto? currentLocationDto = null,
        CancellationToken cancellationToken = default)
    {
        var dto = historyDto;
        var vclDto = currentLocationDto ?? historyDto;
        var isGpsValid = plan?.IsGpsValid ?? true;
        var qualityCode = plan?.QualityCode;
        var streak = plan?.IdenticalCoordinateStreak ?? 0;
        var clearStickyIgnition = plan?.ClearStickyIgnition == true;

        // SG-01 Step 6a: resolve the vehicle's tenant first and bind the booking / driver lookups to it.
        var tenantId = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            "SELECT TenantId FROM Vehicles WHERE Id = @Id AND IsDeleted = 0",
            new { Id = dto.VehicleId },
            cancellationToken: cancellationToken));

        var bookingId = tenantId is int vehicleTenantId
            ? await ResolveActiveBookingIdAsync(connection, vehicleTenantId, dto.VehicleId, dto.BookingId, cancellationToken)
            : null;
        var driverId = dto.DriverId;

        if (driverId is null && bookingId.HasValue)
        {
            driverId = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
                "SELECT DriverId FROM Bookings WHERE Id = @Id AND TenantId = @TenantId",
                new { Id = bookingId.Value, TenantId = tenantId },
                cancellationToken: cancellationToken));
        }

        await connection.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO GpsPositions
              (TenantId, VehicleId, GpsDeviceId, DriverId, BookingId, Latitude, Longitude, Speed, Heading, Altitude, Ignition, RecordedAt, CreatedAt,
               FuelLevel, BatteryLevel, GsmSignal, TotalDistanceKm, Address, AlarmType, Temperature,
               FuelConsumptionRateLph, ObdFuelUsedLiters, ObdOdometerKm, IsGpsValid, GpsQualityCode)
              VALUES (@TenantId, @VehicleId, @GpsDeviceId, @DriverId, @BookingId, @Latitude, @Longitude, @Speed, @Heading, @Altitude, @Ignition, @RecordedAt, @RecordedAt,
               @FuelLevel, @BatteryLevel, @GsmSignal, @TotalDistanceKm, @Address, @AlarmType, @Temperature,
               @FuelConsumptionRateLph, @ObdFuelUsedLiters, @ObdOdometerKm, @IsGpsValid, @GpsQualityCode)",
            new
            {
                TenantId = tenantId,
                dto.VehicleId,
                dto.GpsDeviceId,
                DriverId = driverId,
                BookingId = bookingId,
                dto.Latitude,
                dto.Longitude,
                dto.Speed,
                dto.Heading,
                dto.Altitude,
                dto.Ignition,
                RecordedAt = recordedAt,
                dto.FuelLevel,
                dto.BatteryLevel,
                dto.GsmSignal,
                dto.TotalDistanceKm,
                dto.Address,
                dto.AlarmType,
                dto.Temperature,
                dto.FuelConsumptionRateLph,
                dto.ObdFuelUsedLiters,
                dto.ObdOdometerKm,
                IsGpsValid = isGpsValid,
                GpsQualityCode = qualityCode
            },
            cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition(
            """
            MERGE VehicleCurrentLocation AS target
            USING (SELECT @VehicleId AS VehicleId) AS source
            ON target.VehicleId = source.VehicleId
            WHEN MATCHED THEN
              UPDATE SET GpsDeviceId = @GpsDeviceId, DriverId = @DriverId, BookingId = @BookingId,
                Latitude = @Latitude, Longitude = @Longitude, Speed = @Speed, Heading = @Heading,
                Ignition = CASE WHEN @ClearStickyIgnition = 1 THEN NULL ELSE COALESCE(@Ignition, target.Ignition) END,
                LastUpdate = @LastUpdate,
                FuelLevel = @FuelLevel, BatteryLevel = @BatteryLevel, GsmSignal = @GsmSignal,
                TotalDistanceKm = @TotalDistanceKm,
                Address = CASE
                    WHEN ABS(ISNULL(target.Latitude, 0) - @Latitude) > 0.00001
                      OR ABS(ISNULL(target.Longitude, 0) - @Longitude) > 0.00001
                    THEN @Address
                    ELSE COALESCE(@Address, target.Address)
                  END,
                AlarmType = @AlarmType,
                Temperature = @Temperature,
                FuelConsumptionRateLph = @FuelConsumptionRateLph,
                ObdFuelUsedLiters = @ObdFuelUsedLiters,
                ObdOdometerKm = @ObdOdometerKm,
                IsGpsValid = @IsGpsValid,
                GpsQualityCode = @GpsQualityCode,
                IdenticalCoordinateStreak = @IdenticalCoordinateStreak
            WHEN NOT MATCHED THEN
              INSERT (VehicleId, GpsDeviceId, DriverId, BookingId, Latitude, Longitude, Speed, Heading, Ignition, LastUpdate,
                FuelLevel, BatteryLevel, GsmSignal, TotalDistanceKm, Address, AlarmType, Temperature,
                FuelConsumptionRateLph, ObdFuelUsedLiters, ObdOdometerKm, IsGpsValid, GpsQualityCode, IdenticalCoordinateStreak)
              VALUES (@VehicleId, @GpsDeviceId, @DriverId, @BookingId, @Latitude, @Longitude, @Speed, @Heading, @Ignition, @LastUpdate,
                @FuelLevel, @BatteryLevel, @GsmSignal, @TotalDistanceKm, @Address, @AlarmType, @Temperature,
                @FuelConsumptionRateLph, @ObdFuelUsedLiters, @ObdOdometerKm, @IsGpsValid, @GpsQualityCode, @IdenticalCoordinateStreak);
            """,
            new
            {
                vclDto.VehicleId,
                vclDto.GpsDeviceId,
                DriverId = driverId,
                BookingId = bookingId,
                vclDto.Latitude,
                vclDto.Longitude,
                vclDto.Speed,
                vclDto.Heading,
                vclDto.Ignition,
                LastUpdate = recordedAt,
                vclDto.FuelLevel,
                vclDto.BatteryLevel,
                vclDto.GsmSignal,
                vclDto.TotalDistanceKm,
                vclDto.Address,
                vclDto.AlarmType,
                vclDto.Temperature,
                vclDto.FuelConsumptionRateLph,
                vclDto.ObdFuelUsedLiters,
                vclDto.ObdOdometerKm,
                IsGpsValid = isGpsValid,
                GpsQualityCode = qualityCode,
                IdenticalCoordinateStreak = streak,
                ClearStickyIgnition = clearStickyIgnition
            },
            cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO VehicleTracking
              (VehicleId, DriverId, BookingId, GpsDeviceId, Latitude, Longitude, Speed, Heading, Altitude, Ignition, Timestamp, CreatedAt, IsDeleted)
              VALUES (@VehicleId, @DriverId, @BookingId, @GpsDeviceId, @Latitude, @Longitude, @Speed, @Heading, @Altitude, @Ignition, @Timestamp, @Timestamp, 0)",
            new
            {
                vclDto.VehicleId,
                DriverId = driverId,
                BookingId = bookingId,
                vclDto.GpsDeviceId,
                vclDto.Latitude,
                vclDto.Longitude,
                vclDto.Speed,
                vclDto.Heading,
                vclDto.Altitude,
                vclDto.Ignition,
                Timestamp = recordedAt
            },
            cancellationToken: cancellationToken));
    }
}
