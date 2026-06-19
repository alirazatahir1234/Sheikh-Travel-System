using Dapper;
using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.IO;

namespace SheikhTravelSystem.Application.Features.Vehicles.Commands;

public record UploadVehicleDocumentCommand(
    int VehicleId,
    Stream FileStream,
    string FileName,
    string ContentType,
    string DocumentType,
    DateTime? ExpiryDate,
    string? Notes,
    long FileLength) : IRequest<ApiResponse<UploadVehicleDocumentResult>>;

public record UploadVehicleDocumentResult(int DocumentId, string FileUrl, string DocumentType);

public class UploadVehicleDocumentCommandValidator : AbstractValidator<UploadVehicleDocumentCommand>
{
    public UploadVehicleDocumentCommandValidator()
    {
        RuleFor(x => x.VehicleId).GreaterThan(0);
        RuleFor(x => x.DocumentType).NotEmpty().MaximumLength(80);
        RuleFor(x => x.FileName).NotEmpty();
        RuleFor(x => x.FileLength).GreaterThan(0);
        RuleFor(x => x.FileLength)
            .LessThanOrEqualTo(VehicleUploadLimits.MaxFileBytes)
            .WithMessage($"File exceeds maximum size of {VehicleUploadLimits.MaxFileMegabytes} MB.");
        RuleFor(x => x)
            .Must(x => VehicleUploadLimits.IsAllowedExtension(x.DocumentType, x.FileName))
            .WithMessage(x => string.Equals(x.DocumentType, "VehicleImage", StringComparison.OrdinalIgnoreCase)
                ? "Vehicle image must be a JPG, PNG, WEBP, or GIF file."
                : "Only JPG, PNG, and PDF files are allowed.");
    }
}

public class UploadVehicleDocumentCommandHandler(
    IDbConnectionFactory dbFactory,
    ITenantContext tenantContext,
    IFileStorageService fileStorage)
    : IRequestHandler<UploadVehicleDocumentCommand, ApiResponse<UploadVehicleDocumentResult>>
{
    public async Task<ApiResponse<UploadVehicleDocumentResult>> Handle(
        UploadVehicleDocumentCommand request,
        CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        var exists = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT CASE WHEN EXISTS(SELECT 1 FROM Vehicles WHERE Id = @VehicleId AND TenantId = @TenantId AND IsDeleted = 0) THEN 1 ELSE 0 END",
                new { request.VehicleId, TenantId = tenantId },
                cancellationToken: cancellationToken));

        if (!exists)
            throw new NotFoundException("Vehicle", request.VehicleId);

        await using var boundedStream = new MaxLengthReadStream(request.FileStream, VehicleUploadLimits.MaxFileBytes);
        var stored = await fileStorage.SaveAsync(
            boundedStream,
            request.FileName,
            request.ContentType,
            $"vehicles/{tenantId}/{request.VehicleId}",
            cancellationToken);

        string? notes = request.Notes;
        var isVehicleImage = string.Equals(request.DocumentType, "VehicleImage", StringComparison.OrdinalIgnoreCase);
        if (isVehicleImage)
        {
            var angle = VehicleImageNotes.NormalizeAngle(request.Notes);
            var anglePattern = angle + "|%";
            var hasPrimary = await connection.ExecuteScalarAsync<bool>(
                new CommandDefinition(
                    @"SELECT CASE WHEN EXISTS(
                        SELECT 1 FROM VehicleDocuments
                        WHERE VehicleId = @VehicleId AND TenantId = @TenantId
                          AND DocumentType = N'VehicleImage' AND IsDeleted = 0
                          AND (
                            Notes LIKE N'%|primary%'
                            OR LOWER(LTRIM(RTRIM(Notes))) = N'primary'
                          )
                    ) THEN 1 ELSE 0 END",
                    new { request.VehicleId, TenantId = tenantId },
                    cancellationToken: cancellationToken));

            var replacingPrimary = await connection.ExecuteScalarAsync<bool>(
                new CommandDefinition(
                    @"SELECT CASE WHEN EXISTS(
                        SELECT 1 FROM VehicleDocuments
                        WHERE VehicleId = @VehicleId AND TenantId = @TenantId
                          AND DocumentType = N'VehicleImage' AND IsDeleted = 0
                          AND (Notes = @Angle OR Notes LIKE @AnglePattern)
                          AND (
                            Notes LIKE N'%|primary%'
                            OR LOWER(LTRIM(RTRIM(Notes))) = N'primary'
                          )
                    ) THEN 1 ELSE 0 END",
                    new { request.VehicleId, TenantId = tenantId, Angle = angle, AnglePattern = anglePattern },
                    cancellationToken: cancellationToken));

            notes = VehicleImageNotes.WithAngle(angle, isPrimary: !hasPrimary || replacingPrimary);

            await connection.ExecuteAsync(new CommandDefinition(
                @"UPDATE VehicleDocuments
                  SET IsDeleted = 1, UpdatedAt = GETUTCDATE()
                  WHERE VehicleId = @VehicleId AND TenantId = @TenantId
                    AND DocumentType = N'VehicleImage' AND IsDeleted = 0
                    AND (Notes = @Angle OR Notes LIKE @AnglePattern)",
                new
                {
                    request.VehicleId,
                    TenantId = tenantId,
                    Angle = angle,
                    AnglePattern = anglePattern
                },
                cancellationToken: cancellationToken));
        }
        else
        {
            await connection.ExecuteAsync(new CommandDefinition(
                @"UPDATE VehicleDocuments
                  SET IsDeleted = 1, UpdatedAt = GETUTCDATE()
                  WHERE VehicleId = @VehicleId AND TenantId = @TenantId
                    AND DocumentType = @DocumentType AND IsDeleted = 0",
                new
                {
                    request.VehicleId,
                    TenantId = tenantId,
                    request.DocumentType
                },
                cancellationToken: cancellationToken));
        }

        var docId = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                @"INSERT INTO VehicleDocuments (TenantId, VehicleId, DocumentType, FileUrl, ExpiryDate, Notes, CreatedAt, IsDeleted)
                  VALUES (@TenantId, @VehicleId, @DocumentType, @FileUrl, @ExpiryDate, @Notes, GETUTCDATE(), 0);
                  SELECT CAST(SCOPE_IDENTITY() AS INT);",
                new
                {
                    TenantId = tenantId,
                    request.VehicleId,
                    request.DocumentType,
                    FileUrl = stored.StorageKey,
                    request.ExpiryDate,
                    Notes = notes
                },
                cancellationToken: cancellationToken));

        var result = new UploadVehicleDocumentResult(docId, stored.ReadUrl, request.DocumentType);
        return ApiResponse<UploadVehicleDocumentResult>.SuccessResponse(result, "Document uploaded.");
    }
}
