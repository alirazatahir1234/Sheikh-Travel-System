using Dapper;
using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.DriverApp.DTOs;
using SheikhTravelSystem.Application.Features.Drivers.Commands;
using SheikhTravelSystem.Application.Features.Vehicles;
using SheikhTravelSystem.Application.Features.Vehicles.Commands;

namespace SheikhTravelSystem.Application.Features.DriverApp.Commands;

public record UploadDriverAppDocumentCommand(
    string DocumentType,
    DateTime? ExpiryDate,
    Stream FileStream,
    string FileName,
    string ContentType,
    long FileLength,
    int? VehicleId = null)
    : IRequest<ApiResponse<DriverAppDocumentDto>>;

public class UploadDriverAppDocumentCommandValidator : AbstractValidator<UploadDriverAppDocumentCommand>
{
    private static readonly HashSet<string> DriverTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "DrivingLicense", "CNIC", "MedicalCertificate", "BackgroundCheck"
    };

    private static readonly HashSet<string> VehicleTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        VehicleDocumentTypes.Registration,
        VehicleDocumentTypes.Insurance,
        VehicleDocumentTypes.Permit
    };

    public UploadDriverAppDocumentCommandValidator()
    {
        RuleFor(x => x.DocumentType).NotEmpty();
        RuleFor(x => x.FileLength).GreaterThan(0).LessThanOrEqualTo(VehicleUploadLimits.MaxFileBytes);
        RuleFor(x => x.FileName).NotEmpty();
        RuleFor(x => x).Must(x =>
                DriverTypes.Contains(x.DocumentType) ||
                (VehicleTypes.Contains(x.DocumentType) && x.VehicleId.HasValue))
            .WithMessage("Unsupported document type or missing vehicleId for vehicle documents.");
    }
}

public class UploadDriverAppDocumentCommandHandler(
    IDbConnectionFactory dbFactory,
    ICurrentUserService currentUser,
    ITenantContext tenantContext,
    IMediator mediator,
    IFileStorageService fileStorage)
    : IRequestHandler<UploadDriverAppDocumentCommand, ApiResponse<DriverAppDocumentDto>>
{
    private static readonly HashSet<string> DriverTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "DrivingLicense", "CNIC", "MedicalCertificate", "BackgroundCheck"
    };

    public async Task<ApiResponse<DriverAppDocumentDto>> Handle(
        UploadDriverAppDocumentCommand request, CancellationToken cancellationToken)
    {
        var driverId = currentUser.DriverId;
        if (!driverId.HasValue)
            return ApiResponse<DriverAppDocumentDto>.FailResponse("Driver identity required.");

        if (DriverTypes.Contains(request.DocumentType))
        {
            var result = await mediator.Send(new UploadDriverDocumentCommand(
                driverId.Value,
                request.FileStream,
                request.FileName,
                request.ContentType,
                request.DocumentType,
                request.ExpiryDate,
                request.FileLength), cancellationToken);

            if (!result.Success || result.Data is null)
                return ApiResponse<DriverAppDocumentDto>.FailResponse(result.Message ?? "Upload failed.");

            return ApiResponse<DriverAppDocumentDto>.SuccessResponse(
                ToDto(result.Data.DocumentId, "Driver", request.DocumentType, result.Data.FileUrl,
                    request.ExpiryDate, "Pending", true, null, null),
                "Document uploaded.");
        }

        // Vehicle document — must be assigned to this driver
        var vehicleId = request.VehicleId!.Value;
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();
        var assigned = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            @"SELECT CASE WHEN EXISTS(
                SELECT 1 FROM Trips WHERE DriverId = @DriverId AND VehicleId = @VehicleId
                  AND TenantId = @TenantId AND IsDeleted = 0 AND Status NOT IN (9,10,11)
                UNION ALL
                SELECT 1 FROM Bookings WHERE DriverId = @DriverId AND VehicleId = @VehicleId
                  AND TenantId = @TenantId AND IsDeleted = 0 AND Status IN (2,3)
              ) THEN 1 ELSE 0 END",
            new { DriverId = driverId.Value, VehicleId = vehicleId, TenantId = tenantId },
            cancellationToken: cancellationToken));

        if (!assigned)
            return ApiResponse<DriverAppDocumentDto>.FailResponse("Vehicle is not assigned to you.");

        var vehicleName = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT Name FROM Vehicles WHERE Id = @Id",
            new { Id = vehicleId },
            cancellationToken: cancellationToken));

        var upload = await mediator.Send(new UploadVehicleDocumentCommand(
            vehicleId,
            request.FileStream,
            request.FileName,
            request.ContentType,
            request.DocumentType,
            request.ExpiryDate,
            null,
            request.FileLength), cancellationToken);

        if (!upload.Success || upload.Data is null)
            return ApiResponse<DriverAppDocumentDto>.FailResponse(upload.Message ?? "Upload failed.");

        return ApiResponse<DriverAppDocumentDto>.SuccessResponse(
            ToDto(upload.Data.DocumentId, "Vehicle", request.DocumentType, upload.Data.FileUrl,
                request.ExpiryDate, "Valid", true, vehicleId, vehicleName),
            "Document uploaded.");
    }

    private DriverAppDocumentDto ToDto(
        int id, string scope, string type, string? url, DateTime? expiry, string status,
        bool canUpload, int? vehicleId, string? vehicleName)
    {
        var preview = string.IsNullOrWhiteSpace(url) ? null : fileStorage.ResolveReadUrl(url);
        var days = expiry.HasValue
            ? (int?)Math.Ceiling((expiry.Value.Date - DateTime.UtcNow.Date).TotalDays)
            : null;
        var expired = days is < 0;
        var expiring = days is >= 0 and <= 30;
        if (expired) status = "Expired";
        else if (expiring) status = "Expiring";

        var title = type switch
        {
            "DrivingLicense" => "Driving License",
            "CNIC" => "CNIC / National ID",
            "MedicalCertificate" => "Medical Certificate",
            "BackgroundCheck" => "Background Check",
            "Registration" => "Vehicle Registration",
            "Insurance" => "Insurance",
            "Permit" => "Permit",
            _ => type
        };

        return new DriverAppDocumentDto(
            id, scope, type, title, preview, expiry, status,
            expired, expiring, days, canUpload, vehicleId, vehicleName);
    }
}
