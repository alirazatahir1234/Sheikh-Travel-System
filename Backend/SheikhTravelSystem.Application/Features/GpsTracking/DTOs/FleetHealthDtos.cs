namespace SheikhTravelSystem.Application.Features.GpsTracking.DTOs;

/// <summary>Fleet-level explainable health summary for Live Map.</summary>
public record FleetHealthSummaryDto(
    int? AssessedPercent,
    int Optimal,
    int Healthy,
    int Attention,
    int Critical,
    int Unknown,
    int Total,
    int Assessed,
    IReadOnlyList<FleetVehicleHealthDto> Vehicles);

public record FleetVehicleHealthDto(
    int VehicleId,
    string VehicleName,
    string RegistrationNumber,
    string Band,
    int? Score,
    IReadOnlyList<string> Reasons,
    IReadOnlyList<FleetHealthFactorDto> Factors);

public record FleetHealthFactorDto(
    string Key,
    string State,
    string Detail);
