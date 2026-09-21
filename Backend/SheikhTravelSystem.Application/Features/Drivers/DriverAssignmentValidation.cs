using System.Data;

namespace SheikhTravelSystem.Application.Features.Drivers;

/// <summary>
/// Connection helper retained for Trips/Maintenance/Booking flows that still open transactions.
/// SQL validation moved to Infrastructure DriverAssignmentOps / IDriverRepository.
/// </summary>
public static class DriverAssignmentValidation
{
    public static void OpenConnection(IDbConnection connection)
    {
        if (connection.State != ConnectionState.Open)
            connection.Open();
    }
}
