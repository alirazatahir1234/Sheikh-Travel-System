using System.Data;
using Dapper;
using SheikhTravelSystem.Application.Features.Drivers;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.Assignments;

public static class AssignmentValidation
{
    public static async Task<AssignmentValidationResultDto> ValidateAsync(
        IDbConnection connection,
        int tenantId,
        ValidateAssignmentRequest request,
        CancellationToken cancellationToken,
        int? excludeAssignmentId = null)
    {
        var issues = new List<AssignmentValidationIssueDto>();

        try
        {
            await DriverAssignmentValidation.EnsureVehicleAssignableAsync(
                connection, tenantId, request.VehicleId, cancellationToken);
        }
        catch (Common.Exceptions.ConflictException ex)
        {
            issues.Add(new("VehicleInvalid", ex.Message, "Error"));
        }
        catch (Common.Exceptions.NotFoundException ex)
        {
            issues.Add(new("VehicleNotFound", ex.Message, "Error"));
        }

        var driver = await connection.QuerySingleOrDefaultAsync<(string? VerificationStatus, DateTime LicenseExpiry, bool IsActive, int Status, string FullName)>(
            new CommandDefinition(
                "SELECT VerificationStatus, LicenseExpiryDate, IsActive, Status, FullName FROM Drivers WHERE Id = @DriverId AND TenantId = @TenantId AND IsDeleted = 0",
                new { request.DriverId, TenantId = tenantId },
                cancellationToken: cancellationToken));

        if (driver.VerificationStatus is null)
        {
            issues.Add(new("DriverNotFound", $"Driver {request.DriverId} was not found.", "Error"));
        }
        else
        {
            try
            {
                DriverAssignmentGuard.EnsureAssignable(
                    driver.IsActive, (DriverStatus)driver.Status, driver.VerificationStatus, driver.LicenseExpiry);
            }
            catch (Common.Exceptions.ConflictException ex)
            {
                issues.Add(new("DriverInvalid", ex.Message, "Error"));
            }

            if (!request.SkipSoftWarnings
                && driver.LicenseExpiry.Date >= DateTime.UtcNow.Date
                && driver.LicenseExpiry.Date <= DateTime.UtcNow.Date.AddDays(30))
            {
                var days = (driver.LicenseExpiry.Date - DateTime.UtcNow.Date).Days;
                issues.Add(new("LicenseExpiring", $"Driver license expires in {days} days.", "Warning"));
            }
        }

        try
        {
            await DriverAssignmentValidation.EnsureDriverNotOnActiveTripAsync(
                connection, tenantId, request.DriverId, cancellationToken);
        }
        catch (Common.Exceptions.ConflictException ex)
        {
            issues.Add(new("DriverOnTrip", ex.Message, "Error"));
        }

        try
        {
            await DriverAssignmentValidation.EnsureVehicleAvailableForDriverAsync(
                connection, tenantId, request.VehicleId, request.DriverId, excludeAssignmentId, cancellationToken);
        }
        catch (Common.Exceptions.ConflictException ex)
        {
            issues.Add(new("VehicleConflict", ex.Message, "Error"));
        }

        var driverAssigned = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                @"SELECT CASE WHEN EXISTS(
                    SELECT 1 FROM AssignmentHistory
                    WHERE DriverId = @DriverId AND TenantId = @TenantId
                      AND Status IN (N'Active', N'Scheduled') AND IsDeleted = 0
                      AND (@ExcludeId IS NULL OR Id <> @ExcludeId)
                      AND VehicleId <> @VehicleId
                  ) THEN 1 ELSE 0 END",
                new { request.DriverId, TenantId = tenantId, request.VehicleId, ExcludeId = excludeAssignmentId },
                cancellationToken: cancellationToken));

        if (driverAssigned)
            issues.Add(new("DriverConflict", "Driver already has another active assignment.", "Error"));

        if (!request.SkipSoftWarnings)
        {
            var maintenanceDue = await connection.ExecuteScalarAsync<bool>(
                new CommandDefinition(
                    @"SELECT CASE WHEN EXISTS(
                        SELECT 1 FROM Maintenance m
                        INNER JOIN Vehicles v ON v.Id = m.VehicleId
                        WHERE m.VehicleId = @VehicleId AND v.TenantId = @TenantId AND m.IsDeleted = 0
                          AND m.Status IN (1, 2)
                          AND m.MaintenanceDate <= DATEADD(day, 1, CAST(GETUTCDATE() AS DATE))
                      ) THEN 1 ELSE 0 END",
                    new { request.VehicleId, TenantId = tenantId },
                    cancellationToken: cancellationToken));

            if (maintenanceDue)
                issues.Add(new("MaintenanceDue", "Vehicle has maintenance scheduled within 24 hours.", "Warning"));
        }

        var canProceed = !issues.Any(i => i.Severity == "Error");
        return new AssignmentValidationResultDto(canProceed, issues);
    }

    public static string ResolveInitialStatus(DateTime startDateUtc, string? assignmentType)
    {
        if (assignmentType is "Temporary" or "Emergency")
            return "PendingApproval";

        if (startDateUtc > DateTime.UtcNow)
            return "Scheduled";

        return "Active";
    }

    public static bool IsOpenStatus(string status)
        => status is "Active" or "Scheduled" or "PendingApproval" or "Assigned";
}
