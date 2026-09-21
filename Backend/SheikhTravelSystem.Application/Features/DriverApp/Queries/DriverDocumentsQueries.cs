using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.DriverApp.DTOs;

namespace SheikhTravelSystem.Application.Features.DriverApp.Queries;

public record GetDriverAppDocumentsQuery : IRequest<ApiResponse<DriverAppDocumentsResponse>>;

public class GetDriverAppDocumentsQueryHandler(
    IDriverAppRepository driverAppRepository,
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

        var profile = await driverAppRepository.GetDriverComplianceProfileAsync(
            driverId.Value, tenantId, cancellationToken);
        var licenseExpiry = profile?.LicenseExpiry;
        var cnic = profile?.Cnic;

        var driverDocs = await driverAppRepository.GetDriverComplianceDocsAsync(
            driverId.Value, tenantId, cancellationToken);

        var docsByType = driverDocs
            .GroupBy(d => d.DocumentType, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var list = new List<DriverAppDocumentDto>();
        foreach (var (type, title) in DriverSlots)
        {
            if (docsByType.TryGetValue(type, out var doc))
            {
                list.Add(BuildSlot(doc.Id, "Driver", type, title, doc.FileUrl,
                    doc.ExpiryDate ?? (type == "DrivingLicense" ? licenseExpiry : null),
                    doc.Status, true, null, null));
            }
            else
            {
                list.Add(BuildSlot(null, "Driver", type, title, null,
                    type == "DrivingLicense" ? licenseExpiry : null,
                    null, true, null, null));
            }
        }

        var vehicle = await driverAppRepository.GetPrimaryAssignedVehicleAsync(
            driverId.Value, tenantId, cancellationToken);

        if (vehicle is not null)
        {
            var vDocs = await driverAppRepository.GetVehicleComplianceDocsAsync(
                vehicle.Value.Id, tenantId, cancellationToken);

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
            licenseExpiry,
            cnic,
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
