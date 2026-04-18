namespace SheikhTravelSystem.Application.Features.Tracking.DTOs;

public record UpdateLocationDto(int VehicleId, int? DriverId, int? BookingId, double Latitude, double Longitude, decimal Speed);

public record TrackingDto(int Id, int VehicleId, int? DriverId, int? BookingId, double Latitude, double Longitude, decimal Speed, DateTime Timestamp);
