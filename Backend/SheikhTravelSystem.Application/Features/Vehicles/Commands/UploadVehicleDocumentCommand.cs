using Dapper;
using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Application.Features.Vehicles.Commands;

public record UploadVehicleDocumentCommand(
    int VehicleId,
    Stream FileStream,
    string FileName,
    string ContentType,
    string DocumentType,
    DateTime? ExpiryDate,
    string? Notes) : IRequest<ApiResponse<UploadVehicleDocumentResult>>;

public record UploadVehicleDocumentResult(int DocumentId, string FileUrl, string DocumentType);

public class UploadVehicleDocumentCommandValidator : AbstractValidator<UploadVehicleDocumentCommand>
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".png", ".pdf" };

    public UploadVehicleDocumentCommandValidator()
    {
        RuleFor(x => x.VehicleId).GreaterThan(0);
        RuleFor(x => x.DocumentType).NotEmpty().MaximumLength(80);
        RuleFor(x => x.FileName).NotEmpty();
        RuleFor(x => x).Must(x => AllowedExtensions.Contains(Path.GetExtension(x.FileName)))
            .WithMessage("Only JPG, PNG, and PDF files are allowed.");
    }
}

public class UploadVehicleDocumentCommandHandler(
    IDbConnectionFactory dbFactory,
    ITenantContext tenantContext,
    IFileStorageService fileStorage)
    : IRequestHandler<UploadVehicleDocumentCommand, ApiResponse<UploadVehicleDocumentResult>>
{
    private const long MaxImageBytes = 5 * 1024 * 1024;
    private const long MaxPdfBytes = 10 * 1024 * 1024;

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

        var ext = Path.GetExtension(request.FileName).ToLowerInvariant();
        var maxBytes = ext == ".pdf" ? MaxPdfBytes : MaxImageBytes;
        if (request.FileStream.CanSeek && request.FileStream.Length > maxBytes)
            throw new ValidationException($"File exceeds maximum size of {maxBytes / (1024 * 1024)} MB.");

        var stored = await fileStorage.SaveAsync(
            request.FileStream,
            request.FileName,
            request.ContentType,
            $"vehicles/{tenantId}/{request.VehicleId}",
            cancellationToken);

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
                    FileUrl = stored.RelativeUrl,
                    request.ExpiryDate,
                    request.Notes
                },
                cancellationToken: cancellationToken));

        var result = new UploadVehicleDocumentResult(docId, stored.RelativeUrl, request.DocumentType);
        return ApiResponse<UploadVehicleDocumentResult>.SuccessResponse(result, "Document uploaded.");
    }
}
