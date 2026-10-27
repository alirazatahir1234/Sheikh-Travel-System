using SheikhTravelSystem.Domain.Common;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Domain.Entities;

public class Maintenance : BaseEntity
{
    public int VehicleId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Cost { get; set; }
    public DateTime MaintenanceDate { get; set; }
    public DateTime? NextDueDate { get; set; }
    public MaintenanceStatus Status { get; set; } = MaintenanceStatus.Scheduled;
    public string? ServiceProvider { get; set; }

    /// <summary>FK to Geofences.Id (fleet zone).</summary>
    public int? GeofenceId { get; set; }
    /// <summary>Denormalized Geofences.Name.</summary>
    public string? ZoneName { get; set; }
    /// <summary>Denormalized Geofences.Category.</summary>
    public string? ZoneCategory { get; set; }
    /// <summary>Denormalized Geofences.AreaType.</summary>
    public string? ZoneAreaType { get; set; }
}
