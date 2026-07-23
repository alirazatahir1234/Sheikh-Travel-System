namespace SheikhTravelSystem.Application.Common.Interfaces;

public record DatabaseResetResult(
    bool Success,
    string Message,
    int DeletedCompanies,
    int DeletedUsers,
    int DeletedTrips,
    int DeletedVehicles,
    int DeletedBookings,
    int DeletedDrivers,
    int DeletedCustomers,
    IReadOnlyList<string> ReseededTables);

public interface IDatabaseResetService
{
    /// <summary>
    /// Wipes all tenant/operational data, preserves platform schema and master lookups,
    /// restores the default tenant and system admin, then reseeds demo data.
    /// </summary>
    Task<DatabaseResetResult> ResetAsync(
        int performedByUserId,
        string? ipAddress,
        string? machineName,
        CancellationToken cancellationToken = default);
}
