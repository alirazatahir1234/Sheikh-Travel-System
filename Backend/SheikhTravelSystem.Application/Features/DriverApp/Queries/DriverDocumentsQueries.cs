using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.DriverApp.DTOs;

namespace SheikhTravelSystem.Application.Features.DriverApp.Queries;

public record GetDriverAppDocumentsQuery : IRequest<ApiResponse<DriverAppDocumentsResponse>>;

public class GetDriverAppDocumentsQueryHandler(
    IDbConnectionFactory dbFactory,
    ICurrentUserService currentUser,
    ITenantContext tenantContext,
    IFileStorageService fileStorage)
    : IRequestHandler<GetDriverAppDocumentsQuery, ApiResponse<DriverAppDocumentsResponse>>
{
    private static readonly (string Type, string Title)[] DriverSlots =
    [
        ("DrivingLicense", "Driving License"),
        ("CNIC", "CNIC / National ID"),
        ("MedicalCertificate", "Medical Certificate"),
        ("BackgroundCheck", "Background Check")
    ];

    private static readonly (string Type, string Title)[] VehicleSlots =
    [
        ("Registration", "Vehicle Registration"),
        ("Insurance", "Insurance"),
        ("Permit", "Permit")
    ];

    public async Task<ApiResponse<DriverAppDocumentsResponse>> Handle(
        GetDriverAppDocumentsQuery request, CancellationToken cancellationToken)
    {
        var driverId = currentUser.DriverId;
        if (!driverId.HasValue)
            return ApiResponse<DriverAppDocumentsResponse>.FailResponse("Driver identity required.");

        var tenantId = tenantContext.GetRequiredTenantId();
        using var connection = dbFactory.CreateConnection();

        var profile = await connection.QuerySingleOrDefaultAsync<(string? Cnic, DateTime? LicenseExpiry)>(
            new CommandDefinition(
                "SELECT CNIC, LicenseExpiryDate FROM Drivers WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
                new { Id = driverId.Value, TenantId = tenantId },
                cancellationToken: cancellationToken));

        var driverDocs = (await connection.QueryAsync<(int Id, string DocumentType, string? FileUrl, DateTime? ExpiryDate, string Status)>(
            new CommandDefinition(
                @"SELECT Id, DocumentType, FileUrl, ExpiryDate, Status
                  FROM ComplianceDocuments
                  WHERE EntityType = N'Driver' AND EntityId = @DriverId AND TenantId = @TenantId AND IsDeleted = 0
                  ORDER BY CreatedAt DESC",
                new { DriverId = driverId.Value, TenantId = tenantId },
                cancellationToken: cancellationToken))).ToList();

        var docsByType = driverDocs
            .GroupBy(d => d.DocumentType, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var list = new List<DriverAppDocumentDto>();
        foreach (var (type, title) in DriverSlots)
        {
            if (docsByType.TryGetValue(type, out var doc))
            {
                list.Add(BuildSlot(doc.Id, "Driver", type, title, doc.FileUrl,
                    doc.ExpiryDate ?? (type == "DrivingLicense" ? profile.LicenseExpiry : null),
                    doc.Status, true, null, null));
            }
            else
            {
                list.Add(BuildSlot(null, "Driver", type, title, null,
                    type == "DrivingLicense" ? profile.LicenseExpiry : null,
                    null, true, null, null));
            }
        }

        var vehicle = await connection.QuerySingleOrDefaultAsync<(int Id, string Name)?>(new CommandDefinition(
            $@"SELECT TOP 1 v.Id, v.Name FROM (
                {DriverAppSql.AssignedVehicleIdsUnion}
              ) x
              INNER JOIN Vehicles v ON v.Id = x.VehicleId AND v.IsDeleted = 0
              ORDER BY CASE WHEN EXISTS(
                SELECT 1 FROM AssignmentHistory ah
                WHERE ah.DriverId = @DriverId AND ah.VehicleId = x.VehicleId
                  AND ah.TenantId = @TenantId AND ah.IsDeleted = 0 AND ah.Status = N'Active'
              ) THEN 0 ELSE 1 END",
            new { DriverId = driverId.Value, TenantId = tenantId },
            cancellationToken: cancellationToken));

        if (vehicle is not null)
        {
            var vDocs = (await connection.QueryAsync<(int Id, string DocumentType, string? FileUrl, DateTime? ExpiryDate)>(
                new CommandDefinition(
                    @"SELECT Id, DocumentType, FileUrl, ExpiryDate FROM (
                        SELECT Id, DocumentType, FileUrl, ExpiryDate, CreatedAt,
                          ROW_NUMBER() OVER (PARTITION BY DocumentType ORDER BY CreatedAt DESC) AS rn
                        FROM VehicleDocuments
                        WHERE VehicleId = @VehicleId AND TenantId = @TenantId AND IsDeleted = 0
                          AND DocumentType IN (N'Registration', N'Insurance', N'Permit')
                      ) r WHERE rn = 1",
                    new { VehicleId = vehicle.Value.Id, TenantId = tenantId },
                    cancellationToken: cancellationToken))).ToList();

            var vByType = vDocs.ToDictionary(d => d.DocumentType, d => d, StringComparer.OrdinalIgnoreCase);
            foreach (var (type, title) in VehicleSlots)
            {
                if (vByType.TryGetValue(type, out var doc))
                {
                    list.Add(BuildSlot(doc.Id, "Vehicle", type, title, doc.FileUrl, doc.ExpiryDate,
                        null, true, vehicle.Value.Id, vehicle.Value.Name));
                }
                else
                {
                    list.Add(BuildSlot(null, "Vehicle", type, title, null, null, null,
                        true, vehicle.Value.Id, vehicle.Value.Name));
                }
            }
        }
        else
        {
            foreach (var (type, title) in VehicleSlots)
            {
                list.Add(BuildSlot(null, "Vehicle", type, title, null, null, null,
                    false, null, null));
            }
        }

        return ApiResponse<DriverAppDocumentsResponse>.SuccessResponse(new DriverAppDocumentsResponse(
            list,
            profile.LicenseExpiry,
            profile.Cnic,
            list.Count(d => d.IsExpiringSoon),
            list.Count(d => d.IsExpired),
            list.Count(d => d.Status == "Missing")));
    }

    private DriverAppDocumentDto BuildSlot(
        int? id,
        string scope,
        string type,
        string title,
        string? fileUrl,
        DateTime? expiry,
        string? rawStatus,
        bool canUpload,
        int? vehicleId,
        string? vehicleName)
    {
        var preview = string.IsNullOrWhiteSpace(fileUrl) ? null : fileStorage.ResolveReadUrl(fileUrl);
        var hasFile = !string.IsNullOrWhiteSpace(preview);
        var (isExpired, isExpiring, days) = ExpiryFlags(expiry);
        var status = ResolveStatus(hasFile, rawStatus, isExpired, isExpiring);

        return new DriverAppDocumentDto(
            id, scope, type, title, preview, expiry, status,
            isExpired, isExpiring, days, canUpload, vehicleId, vehicleName);
    }

    private static string ResolveStatus(bool hasFile, string? raw, bool expired, bool expiring)
    {
        if (!hasFile) return "Missing";
        if (expired) return "Expired";
        if (expiring) return "Expiring";
        if (string.Equals(raw, "Rejected", StringComparison.OrdinalIgnoreCase)) return "Rejected";
        if (string.Equals(raw, "Pending", StringComparison.OrdinalIgnoreCase)) return "Pending";
        if (string.Equals(raw, "Approved", StringComparison.OrdinalIgnoreCase)) return "Approved";
        return "Valid";
    }

    private static (bool Expired, bool Expiring, int? Days) ExpiryFlags(DateTime? expiry)
    {
        if (!expiry.HasValue) return (false, false, null);
        var days = (int)Math.Ceiling((expiry.Value.Date - DateTime.UtcNow.Date).TotalDays);
        if (days < 0) return (true, false, days);
        if (days <= 30) return (false, true, days);
        return (false, false, days);
    }
}
