using SheikhTravelSystem.Application.Features.GpsTracking;

namespace SheikhTravelSystem.Application.Features.GpsTracking.DTOs;

public record FleetTripModeSettingsDto(
    FleetTripCreationMode TripCreationMode,
    string TimeZoneId,
    string ModeDescription);

public record UpsertFleetTripModeSettingsRequest(FleetTripCreationMode TripCreationMode);
