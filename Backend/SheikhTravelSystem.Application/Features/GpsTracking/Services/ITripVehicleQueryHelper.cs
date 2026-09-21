using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

public sealed record VehicleTripSource(
    int VehicleId,
    string? VehicleName,
    string? PlateNumber,
    int? GpsDeviceId,
    string? DeviceName,
    string? UniqueId,
    int? TraccarDeviceId,
    DateTime? LastSeenAt);

public interface ITripVehicleQueryHelper
{
    Task<VehicleTripSource?> ResolveVehicleByTraccarDeviceIdAsync(
        int traccarDeviceId,
        CancellationToken cancellationToken);

    Task<VehicleTripSource?> ResolveVehicleAsync(
        int vehicleId,
        CancellationToken cancellationToken);

    Task<TripDeviceContextDto?> BuildContextAsync(
        int vehicleId,
        CancellationToken cancellationToken);
}

/// <summary>Request validation for trip/history queries — no SQL.</summary>
public static class TripVehicleQueryValidation
{
    public static readonly TimeSpan MaxRange = TimeSpan.FromDays(30);
    public static readonly TimeSpan MaxHistoryRange = TimeSpan.FromDays(366);

    public static ApiResponse<T>? ValidateTripRequest<T>(int? vehicleId, DateTime fromDate, DateTime toDate)
    {
        if (fromDate > toDate)
            return ApiResponse<T>.FailResponse("End Date cannot be earlier than Start Date.");

        if (toDate - fromDate > MaxRange)
            return ApiResponse<T>.FailResponse("Date range cannot exceed 30 days.");

        if (!vehicleId.HasValue)
            return ApiResponse<T>.FailResponse("Select a vehicle to view trips.");

        return null;
    }

    public static ApiResponse<T>? ValidateHistoryRequest<T>(
        int? vehicleId,
        DateTime fromDate,
        DateTime toDate,
        int? deviceId = null)
    {
        if (fromDate > toDate)
            return ApiResponse<T>.FailResponse("End Date cannot be earlier than Start Date.");

        if (toDate - fromDate > MaxHistoryRange)
            return ApiResponse<T>.FailResponse("Date range cannot exceed 366 days.");

        if (!vehicleId.HasValue && !deviceId.HasValue)
            return ApiResponse<T>.FailResponse("Select a vehicle or device to view history.");

        return null;
    }
}
