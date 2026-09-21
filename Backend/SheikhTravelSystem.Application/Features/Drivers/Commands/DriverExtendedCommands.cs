using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Common.IO;
using SheikhTravelSystem.Application.Features.Drivers.DTOs;
using SheikhTravelSystem.Application.Features.Vehicles;

namespace SheikhTravelSystem.Application.Features.Drivers.Commands;

public record UploadDriverPhotoCommand(int DriverId, Stream FileStream, string FileName, string ContentType, long FileLength) : IRequest<ApiResponse<string>>, IAuditableCommand
{
    public string AuditAction => "Update";
    public string AuditEntityName => "Driver";
    public int? AuditEntityId => DriverId;
}

public class UploadDriverPhotoCommandValidator : AbstractValidator<UploadDriverPhotoCommand>
{
    public UploadDriverPhotoCommandValidator()
    {
        RuleFor(x => x.DriverId).GreaterThan(0);
        RuleFor(x => x.FileName).NotEmpty();
        RuleFor(x => x.FileLength).GreaterThan(0).LessThanOrEqualTo(VehicleUploadLimits.MaxFileBytes);
    }
}

public class UploadDriverPhotoCommandHandler(
    IDriverRepository driverRepository,
    ITenantContext tenantContext,
    IFileStorageService fileStorage)
    : IRequestHandler<UploadDriverPhotoCommand, ApiResponse<string>>
{
    public async Task<ApiResponse<string>> Handle(UploadDriverPhotoCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        if (!await driverRepository.ExistsAsync(tenantId, request.DriverId, cancellationToken))
            throw new Common.Exceptions.NotFoundException("Driver", request.DriverId);

        await using var boundedStream = new MaxLengthReadStream(request.FileStream, VehicleUploadLimits.MaxFileBytes);
        var stored = await fileStorage.SaveAsync(
            boundedStream, request.FileName, request.ContentType,
            $"drivers/{tenantId}/{request.DriverId}", cancellationToken);

        await driverRepository.UpdatePhotoUrlAsync(tenantId, request.DriverId, stored.StorageKey, cancellationToken);
        return ApiResponse<string>.SuccessResponse(stored.ReadUrl, "Photo uploaded.");
    }
}

public record UploadDriverDocumentCommand(
    int DriverId, Stream FileStream, string FileName, string ContentType,
    string DocumentType, DateTime? ExpiryDate, long FileLength) : IRequest<ApiResponse<UploadDriverDocumentResult>>, IAuditableCommand
{
    public string AuditAction => "Create";
    public string AuditEntityName => "DriverDocument";
    public int? AuditEntityId => null;
}

public class UploadDriverDocumentCommandValidator : AbstractValidator<UploadDriverDocumentCommand>
{
    private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "DrivingLicense", "CNIC", "MedicalCertificate", "BackgroundCheck"
    };

    public UploadDriverDocumentCommandValidator()
    {
        RuleFor(x => x.DriverId).GreaterThan(0);
        RuleFor(x => x.DocumentType).Must(t => AllowedTypes.Contains(t));
        RuleFor(x => x.FileLength).GreaterThan(0).LessThanOrEqualTo(VehicleUploadLimits.MaxFileBytes);
    }
}

public class UploadDriverDocumentCommandHandler(
    IDriverRepository driverRepository,
    ITenantContext tenantContext,
    IFileStorageService fileStorage)
    : IRequestHandler<UploadDriverDocumentCommand, ApiResponse<UploadDriverDocumentResult>>
{
    public async Task<ApiResponse<UploadDriverDocumentResult>> Handle(
        UploadDriverDocumentCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        if (!await driverRepository.ExistsAsync(tenantId, request.DriverId, cancellationToken))
            throw new Common.Exceptions.NotFoundException("Driver", request.DriverId);

        await using var boundedStream = new MaxLengthReadStream(request.FileStream, VehicleUploadLimits.MaxFileBytes);
        var stored = await fileStorage.SaveAsync(
            boundedStream, request.FileName, request.ContentType,
            $"drivers/{tenantId}/{request.DriverId}/documents", cancellationToken);

        await driverRepository.SoftDeleteDocumentsByTypeAsync(
            tenantId, request.DriverId, request.DocumentType, cancellationToken);

        var docId = await driverRepository.InsertDocumentAsync(
            tenantId, request.DriverId, request.DocumentType, stored.StorageKey, request.ExpiryDate, cancellationToken);

        return ApiResponse<UploadDriverDocumentResult>.SuccessResponse(
            new UploadDriverDocumentResult(docId, stored.ReadUrl, request.DocumentType),
            "Document uploaded.");
    }
}

public record UpdateDriverVerificationCommand(int DriverId, string VerificationStatus) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Update";
    public string AuditEntityName => "Driver";
    public int? AuditEntityId => DriverId;
}

public class UpdateDriverVerificationCommandHandler(IDriverRepository driverRepository, ITenantContext tenantContext)
    : IRequestHandler<UpdateDriverVerificationCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdateDriverVerificationCommand request, CancellationToken cancellationToken)
    {
        await driverRepository.UpdateVerificationStatusAsync(
            tenantContext.GetRequiredTenantId(), request.DriverId, request.VerificationStatus, cancellationToken);
        return ApiResponse<bool>.SuccessResponse(true, "Verification status updated.");
    }
}

public record AssignDriverVehicleCommand(int DriverId, AssignDriverVehicleRequest Body) : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => "Assign";
    public string AuditEntityName => "Driver";
    public int? AuditEntityId => DriverId;
}

public class AssignDriverVehicleCommandHandler(
    IDriverRepository driverRepository,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<AssignDriverVehicleCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(AssignDriverVehicleCommand request, CancellationToken cancellationToken)
    {
        var assignmentId = await driverRepository.AssignVehicleAsync(
            tenantContext.GetRequiredTenantId(),
            request.DriverId,
            request.Body,
            currentUser.UserId?.ToString() ?? "api",
            cancellationToken);
        return ApiResponse<int>.SuccessResponse(assignmentId, "Vehicle assigned to driver.");
    }
}
