using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.Vehicles.DTOs;

/// <summary>
/// Dapper row for vehicle detail. Mutable class so nullable SQL columns and BIT map correctly;
/// <see cref="VehicleDto"/> positional records break materialization when SELECT column order
/// (e.g. CompanyId early) or reader nullability diverge from the record constructor signature.
/// </summary>
public sealed class VehicleDetailRow
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string RegistrationNumber { get; set; } = "";
    public string? VehicleCode { get; set; }
    public string? VIN { get; set; }
    public string? Make { get; set; }
    public string? Model { get; set; }
    public int? Year { get; set; }
    public string? Color { get; set; }
    public string? VehicleType { get; set; }
    public int SeatingCapacity { get; set; }
    public decimal FuelAverage { get; set; }
    public FuelType FuelType { get; set; }
    public string? EngineNo { get; set; }
    public string? ChassisNo { get; set; }
    public decimal CurrentMileage { get; set; }
    public DateTime? InsuranceExpiryDate { get; set; }
    public int? GpsDeviceId { get; set; }
    public DateTime? PurchaseDate { get; set; }
    public decimal? PurchasePrice { get; set; }
    public string? PurchaseCurrencyCode { get; set; }
    public int? BranchId { get; set; }
    public int? DepartmentId { get; set; }
    public VehicleStatus Status { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int? CompanyId { get; set; }
    public string? CompanyName { get; set; }
    public string? DriverName { get; set; }
    public int? DriverId { get; set; }
    public string? DriverPhone { get; set; }
    public string? DriverLicenseNumber { get; set; }
    public string? GpsImei { get; set; }
    public string? GpsSim { get; set; }
    public bool? EngineIgnition { get; set; }
    public DateTime? GpsLastSeenAt { get; set; }
    public bool GpsOnline { get; set; }
    public bool HasGpsDevice { get; set; }
    public double? LocationLatitude { get; set; }
    public double? LocationLongitude { get; set; }
    public DateTime? LocationLastUpdate { get; set; }
    public decimal? LocationSpeed { get; set; }
    public decimal? TotalDistanceKm { get; set; }
    public decimal? BatteryLevel { get; set; }
    public int? GsmSignal { get; set; }
    public string? Address { get; set; }
    public string? ImageUrl { get; set; }
    public string? TrackerName { get; set; }
    public string? TrackerModel { get; set; }
    public string? TrackerBrand { get; set; }
    public DateTime? TrackerInstallationDate { get; set; }
    public DateTime? NextServiceDue { get; set; }
    public decimal? NextDueMileage { get; set; }
    public string? ServiceAlert { get; set; }
}

public static class VehicleDetailMapper
{
    public static VehicleDto ToDto(VehicleDetailRow row) =>
        new(
            row.Id,
            row.Name,
            row.RegistrationNumber,
            row.VehicleCode,
            row.VIN,
            row.Make,
            row.Model,
            row.Year,
            row.Color,
            row.VehicleType,
            row.SeatingCapacity,
            row.FuelAverage,
            row.FuelType,
            row.EngineNo,
            row.ChassisNo,
            row.CurrentMileage,
            row.InsuranceExpiryDate,
            row.GpsDeviceId,
            row.PurchaseDate,
            row.PurchasePrice,
            row.PurchaseCurrencyCode,
            row.BranchId,
            row.DepartmentId,
            row.Status,
            row.IsActive,
            row.CreatedAt,
            row.UpdatedAt,
            DriverName: row.DriverName,
            DriverId: row.DriverId,
            DriverPhone: row.DriverPhone,
            DriverLicenseNumber: row.DriverLicenseNumber,
            GpsImei: row.GpsImei,
            GpsSim: row.GpsSim,
            EngineIgnition: row.EngineIgnition,
            GpsLastSeenAt: row.GpsLastSeenAt,
            GpsOnline: row.GpsOnline,
            HasGpsDevice: row.HasGpsDevice,
            LocationLatitude: row.LocationLatitude,
            LocationLongitude: row.LocationLongitude,
            LocationLastUpdate: row.LocationLastUpdate,
            LocationSpeed: row.LocationSpeed,
            TotalDistanceKm: row.TotalDistanceKm,
            BatteryLevel: row.BatteryLevel,
            GsmSignal: row.GsmSignal,
            Address: row.Address,
            ImageUrl: row.ImageUrl,
            TrackerName: row.TrackerName,
            TrackerModel: row.TrackerModel,
            TrackerBrand: row.TrackerBrand,
            TrackerInstallationDate: row.TrackerInstallationDate,
            NextServiceDue: row.NextServiceDue,
            NextDueMileage: row.NextDueMileage,
            ServiceAlert: row.ServiceAlert,
            CompanyId: row.CompanyId,
            CompanyName: row.CompanyName);
}
