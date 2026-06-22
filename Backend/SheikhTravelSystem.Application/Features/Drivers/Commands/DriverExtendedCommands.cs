using Dapper;
using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.IO;
using SheikhTravelSystem.Application.Features.Drivers.DTOs;
using SheikhTravelSystem.Application.Features.Vehicles;

namespace SheikhTravelSystem.Application.Features.Drivers.Commands;

public record UploadDriverPhotoCommand(int DriverId, Stream FileStream, string FileName, string ContentType, long FileLength)
    : IRequest<ApiResponse<string>>;

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
    IDbConnectionFactory dbFactory,
    ITenantContext tenantContext,
    IFileStorageService fileStorage)
    : IRequestHandler<UploadDriverPhotoCommand, ApiResponse<string>>
{
    public async Task<ApiResponse<string>> Handle(UploadDriverPhotoCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        var exists = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT CASE WHEN EXISTS(SELECT 1 FROM Drivers WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0) THEN 1 ELSE 0 END",
                new { Id = request.DriverId, TenantId = tenantId },
                cancellationToken: cancellationToken));

        if (!exists)
            throw new NotFoundException("Driver", request.DriverId);

        await using var boundedStream = new MaxLengthReadStream(request.FileStream, VehicleUploadLimits.MaxFileBytes);
        var stored = await fileStorage.SaveAsync(
            boundedStream,
            request.FileName,
            request.ContentType,
            $"drivers/{tenantId}/{request.DriverId}",
            cancellationToken);

        await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE Drivers SET PhotoUrl = @PhotoUrl, UpdatedAt = GETUTCDATE() WHERE Id = @Id AND TenantId = @TenantId",
                new { PhotoUrl = stored.StorageKey, Id = request.DriverId, TenantId = tenantId },
                cancellationToken: cancellationToken));

        return ApiResponse<string>.SuccessResponse(stored.ReadUrl, "Photo uploaded.");
    }
}

public record UploadDriverDocumentCommand(
    int DriverId,
    Stream FileStream,
    string FileName,
    string ContentType,
    string DocumentType,
    DateTime? ExpiryDate,
    long FileLength) : IRequest<ApiResponse<UploadDriverDocumentResult>>;

public class UploadDriverDocumentCommandValidator : AbstractValidator<UploadDriverDocumentCommand>
{
    private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "DrivingLicense", "MedicalCertificate", "BackgroundCheck"
    };

    public UploadDriverDocumentCommandValidator()
    {
        RuleFor(x => x.DriverId).GreaterThan(0);
        RuleFor(x => x.DocumentType).Must(t => AllowedTypes.Contains(t));
        RuleFor(x => x.FileLength).GreaterThan(0).LessThanOrEqualTo(VehicleUploadLimits.MaxFileBytes);
    }
}

public class UploadDriverDocumentCommandHandler(
    IDbConnectionFactory dbFactory,
    ITenantContext tenantContext,
    IFileStorageService fileStorage)
    : IRequestHandler<UploadDriverDocumentCommand, ApiResponse<UploadDriverDocumentResult>>
{
    public async Task<ApiResponse<UploadDriverDocumentResult>> Handle(
        UploadDriverDocumentCommand request,
        CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        var exists = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT CASE WHEN EXISTS(SELECT 1 FROM Drivers WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0) THEN 1 ELSE 0 END",
                new { Id = request.DriverId, TenantId = tenantId },
                cancellationToken: cancellationToken));

        if (!exists)
            throw new NotFoundException("Driver", request.DriverId);

        await using var boundedStream = new MaxLengthReadStream(request.FileStream, VehicleUploadLimits.MaxFileBytes);
        var stored = await fileStorage.SaveAsync(
            boundedStream,
            request.FileName,
            request.ContentType,
            $"drivers/{tenantId}/{request.DriverId}/documents",
            cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            @"UPDATE ComplianceDocuments SET IsDeleted = 1, UpdatedAt = GETUTCDATE()
              WHERE TenantId = @TenantId AND EntityType = N'Driver' AND EntityId = @DriverId
                AND DocumentType = @DocumentType AND IsDeleted = 0",
            new { TenantId = tenantId, request.DriverId, request.DocumentType },
            cancellationToken: cancellationToken));

        var docId = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                @"INSERT INTO ComplianceDocuments (TenantId, EntityType, EntityId, DocumentType, FileUrl,
                  ExpiryDate, Status, CreatedAt, IsDeleted)
                  VALUES (@TenantId, N'Driver', @DriverId, @DocumentType, @FileUrl, @ExpiryDate, N'Pending', GETUTCDATE(), 0);
                  SELECT CAST(SCOPE_IDENTITY() AS INT);",
                new
                {
                    TenantId = tenantId,
                    request.DriverId,
                    request.DocumentType,
                    FileUrl = stored.StorageKey,
                    request.ExpiryDate
                },
                cancellationToken: cancellationToken));

        return ApiResponse<UploadDriverDocumentResult>.SuccessResponse(
            new UploadDriverDocumentResult(docId, stored.ReadUrl, request.DocumentType),
            "Document uploaded.");
    }
}

public record UpdateDriverVerificationCommand(int DriverId, string VerificationStatus) : IRequest<ApiResponse<bool>>;

public class UpdateDriverVerificationCommandHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<UpdateDriverVerificationCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdateDriverVerificationCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        var rows = await connection.ExecuteAsync(
            new CommandDefinition(
                @"UPDATE Drivers SET VerificationStatus = @VerificationStatus, UpdatedAt = GETUTCDATE()
                  WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
                new { VerificationStatus = request.VerificationStatus.Trim(), Id = request.DriverId, TenantId = tenantId },
                cancellationToken: cancellationToken));

        if (rows == 0)
            throw new NotFoundException("Driver", request.DriverId);

        return ApiResponse<bool>.SuccessResponse(true, "Verification status updated.");
    }
}

public record AssignDriverVehicleCommand(int DriverId, AssignDriverVehicleRequest Body) : IRequest<ApiResponse<int>>;

public class AssignDriverVehicleCommandHandler(
    IDbConnectionFactory dbFactory,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<AssignDriverVehicleCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(AssignDriverVehicleCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        DriverAssignmentValidation.OpenConnection(connection);
        using var transaction = connection.BeginTransaction();

        try
        {
            var tenantId = tenantContext.GetRequiredTenantId();
            var body = request.Body;

            var driver = await connection.QuerySingleOrDefaultAsync<(string VerificationStatus, DateTime LicenseExpiry, bool IsActive, int Status)>(
                new CommandDefinition(
                    "SELECT VerificationStatus, LicenseExpiryDate, IsActive, Status FROM Drivers WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
                    new { Id = request.DriverId, TenantId = tenantId },
                    transaction: transaction,
                    cancellationToken: cancellationToken));

            if (driver.VerificationStatus is null)
                throw new NotFoundException("Driver", request.DriverId);

            DriverAssignmentGuard.EnsureAssignable(
                driver.IsActive,
                (Domain.Enums.DriverStatus)driver.Status,
                driver.VerificationStatus,
                driver.LicenseExpiry);

            await DriverAssignmentValidation.EnsureDriverNotOnActiveTripAsync(
                connection, tenantId, request.DriverId, cancellationToken, transaction);

            var vehicleExists = await connection.ExecuteScalarAsync<bool>(
                new CommandDefinition(
                    "SELECT CASE WHEN EXISTS(SELECT 1 FROM Vehicles WHERE Id = @VehicleId AND TenantId = @TenantId AND IsDeleted = 0) THEN 1 ELSE 0 END",
                    new { body.VehicleId, TenantId = tenantId },
                    transaction: transaction,
                    cancellationToken: cancellationToken));

            if (!vehicleExists)
                throw new NotFoundException("Vehicle", body.VehicleId);

            await DriverAssignmentValidation.CompleteActiveAssignmentsAsync(
                connection, tenantId, request.DriverId, body.VehicleId, transaction, cancellationToken);

            var assignmentType = string.IsNullOrWhiteSpace(body.AssignmentType) ? "Manual" : body.AssignmentType.Trim();
            var startAt = body.EffectiveFrom?.ToUniversalTime() ?? DateTime.UtcNow;
            var notes = string.IsNullOrWhiteSpace(body.Remarks) ? null : body.Remarks.Trim();
            if (body.EffectiveTo is DateTime plannedEnd)
            {
                var plannedNote = $"Planned end: {plannedEnd:yyyy-MM-dd}";
                notes = string.IsNullOrWhiteSpace(notes) ? plannedNote : $"{notes} ({plannedNote})";
            }

            var assignmentId = await connection.ExecuteScalarAsync<int>(
                new CommandDefinition(
                    @"INSERT INTO AssignmentHistory (TenantId, VehicleId, DriverId, BookingId, AssignmentType,
                      Status, StartAt, CreatedAt, CreatedBy, Notes, IsDeleted)
                      VALUES (@TenantId, @VehicleId, @DriverId, @BookingId, @AssignmentType,
                      N'Active', @StartAt, GETUTCDATE(), @CreatedBy, @Notes, 0);
                      SELECT CAST(SCOPE_IDENTITY() AS INT);",
                    new
                    {
                        TenantId = tenantId,
                        body.VehicleId,
                        request.DriverId,
                        body.BookingId,
                        AssignmentType = assignmentType,
                        StartAt = startAt,
                        Notes = notes,
                        CreatedBy = currentUser.UserId?.ToString() ?? "api"
                    },
                    transaction: transaction,
                    cancellationToken: cancellationToken));

            transaction.Commit();
            return ApiResponse<int>.SuccessResponse(assignmentId, "Vehicle assigned to driver.");
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }
}
