namespace SheikhTravelSystem.Application.Common.Interfaces;

/// <summary>
/// Marker interface for commands that should be recorded in the AuditLogs table.
/// </summary>
public interface IAuditableCommand
{
    /// <summary>Action label: "Create", "Update", "Delete", "Assign", etc.</summary>
    string AuditAction { get; }

    /// <summary>Domain entity name, e.g. "Driver", "Booking", "Vehicle".</summary>
    string AuditEntityName { get; }

    /// <summary>
    /// ID of the entity being affected.
    /// Null for Create commands — the behavior reads the ID from the handler response.
    /// </summary>
    int? AuditEntityId { get; }
}
