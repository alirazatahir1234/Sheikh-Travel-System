namespace SheikhTravelSystem.Application.Common;

/// <summary>Named ASP.NET authorization policies for attendance endpoints.</summary>
public static class AttendanceAuthPolicies
{
    /// <summary>
    /// Authenticated driver with <c>driver_id</c> claim (legacy "Driver" or RBAC "DRIVER").
    /// </summary>
    public const string DriverSelf = "Attendance.DriverSelf";
}
