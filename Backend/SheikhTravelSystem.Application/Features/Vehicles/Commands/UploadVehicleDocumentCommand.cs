using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Common.IO;

namespace SheikhTravelSystem.Application.Features.Vehicles.Commands;

public record UploadVehicleDocumentCommand(
    int VehicleId, Stream FileStream, string FileName, string ContentType,
    string DocumentType, DateTime? ExpiryDate, string? Notes, long FileLength)
    : IRequest<ApiResponse<UploadVehicleDocumentResult>>, IAuditableCommand
{
    public string AuditAction => "Create";
    public string AuditEntityName => "VehicleDocument";
    public int? AuditEntityId => null;
}

public record UploadVehicleDocumentResult(int DocumentId, string FileUrl, string DocumentType);

public class UploadVehicleDocumentCommandValidator : AbstractValidator<UploadVehicleDocumentCommand>
{
    public UploadVehicleDocumentCommandValidator()
    {
        RuleFor(x => x.VehicleId).GreaterThan(0);
        RuleFor(x => x.DocumentType).NotEmpty().MaximumLength(80)
            .Must(VehicleDocumentTypes.IsAllowed)
            .WithMessage("Document type must be one of: VehicleImage, Registration, Insurance, RoadTax, Fitness, Permit.");
        RuleFor(x => x.FileName).NotEmpty();
        RuleFor(x => x.FileLength).GreaterThan(0);
        RuleFor(x => x.FileLength)
            .LessThanOrEqualTo(VehicleUploadLimits.MaxFileBytes)
            .WithMessage($"File exceeds maximum size of {VehicleUploadLimits.MaxFileMegabytes} MB.");
        RuleFor(x => x)
            .Must(x => VehicleUploadLimits.IsAllowedExtension(x.DocumentType, x.FileName))
            .WithMessage(x => VehicleDocumentTypes.IsVehicleImage(x.DocumentType)
                ? "Vehicle image must be a JPG, PNG, WEBP, or GIF file."
                : "Only JPG, PNG, and PDF files are allowed.");
    }
}

public class UploadVehicleDocumentCommandHandler(
    IVehicleRepository vehicleRepository,
    ITenantContext tenantContext,
    IFileStorageService fileStorage)
    : IRequestHandler<UploadVehicleDocumentCommand, ApiResponse<UploadVehicleDocumentResult>>
{
    public async Task<ApiResponse<UploadVehicleDocumentResult>> Handle(
        UploadVehicleDocumentCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();

        await using var boundedStream = new MaxLengthReadStream(request.FileStream, VehicleUploadLimits.MaxFileBytes);
        var stored = await fileStorage.SaveAsync(
            boundedStream, request.FileName, request.ContentType,
            $"vehicles/{tenantId}/{request.VehicleId}", cancellationToken);

        var docId = await vehicleRepository.PersistUploadedDocumentAsync(
            request.VehicleId, tenantId, request.DocumentType, stored.StorageKey,
            request.ExpiryDate, request.Notes, cancellationToken);

        return ApiResponse<UploadVehicleDocumentResult>.SuccessResponse(
            new UploadVehicleDocumentResult(docId, stored.ReadUrl, request.DocumentType),
            "Document uploaded.");
    }
}
