using Dapper;
using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.DriverApp.DTOs;

namespace SheikhTravelSystem.Application.Features.DriverApp.Commands;

public record DriverInspectionUploadFile(Stream Content, string FileName, string ContentType, long Length);

public record SubmitDriverInspectionCommand(
    int VehicleId,
    int? TemplateId,
    decimal? OdometerReading,
    string? Comments,
    IReadOnlyList<InspectionResultItemDto> Results,
    string? OverallResult,
    IReadOnlyList<DriverInspectionUploadFile>? Photos,
    DriverInspectionUploadFile? Signature)
    : IRequest<ApiResponse<int>>;

public class SubmitDriverInspectionCommandValidator : AbstractValidator<SubmitDriverInspectionCommand>
{
    public SubmitDriverInspectionCommandValidator()
    {
        RuleFor(x => x.VehicleId).GreaterThan(0);
        RuleFor(x => x.Results).NotEmpty();
        RuleForEach(x => x.Results).ChildRules(r =>
        {
            r.RuleFor(x => x.Key).NotEmpty();
            r.RuleFor(x => x.Status).Must(s =>
            {
                var n = s?.Trim();
                return n is "Pass" or "Warning" or "Fail" or "pass" or "warning" or "fail";
            });
        });
    }
}

public class SubmitDriverInspectionCommandHandler(
    IDbConnectionFactory dbFactory,
    ICurrentUserService currentUser,
    ITenantContext tenantContext,
    IFileStorageService fileStorage)
    : IRequestHandler<SubmitDriverInspectionCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(
        SubmitDriverInspectionCommand request, CancellationToken cancellationToken)
    {
        var driverId = currentUser.DriverId;
        if (!driverId.HasValue)
            return ApiResponse<int>.FailResponse("Driver identity required.");

        var tenantId = tenantContext.GetRequiredTenantId();
        using var connection = dbFactory.CreateConnection();

        var vehicleOk = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            @"SELECT CASE WHEN EXISTS(
                SELECT 1 FROM Vehicles WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0
              ) THEN 1 ELSE 0 END",
            new { Id = request.VehicleId, TenantId = tenantId },
            cancellationToken: cancellationToken));

        if (!vehicleOk)
            return ApiResponse<int>.FailResponse("Vehicle not found.");

        var templateId = request.TemplateId;
        string? checklistJson = null;
        if (templateId is null or <= 0)
        {
            var tmpl = await connection.QuerySingleOrDefaultAsync<(int Id, string ChecklistJson)?>(new CommandDefinition(
                @"SELECT TOP 1 Id, ChecklistJson FROM InspectionTemplates
                  WHERE IsDeleted = 0 AND IsActive = 1
                    AND (TenantId IS NULL OR TenantId = @TenantId)
                  ORDER BY CASE WHEN Name LIKE N'%Standard%' THEN 0 ELSE 1 END, Id",
                new { TenantId = tenantId },
                cancellationToken: cancellationToken));

            if (tmpl is null)
                return ApiResponse<int>.FailResponse("No inspection template configured.");
            templateId = tmpl.Value.Id;
            checklistJson = tmpl.Value.ChecklistJson;
        }
        else
        {
            checklistJson = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
                @"SELECT ChecklistJson FROM InspectionTemplates
                  WHERE Id = @Id AND IsDeleted = 0 AND IsActive = 1",
                new { Id = templateId.Value },
                cancellationToken: cancellationToken));
            if (checklistJson is null)
                return ApiResponse<int>.FailResponse("Inspection template not found.");
        }

        var items = InspectionResultCalculator.ParseChecklist(checklistJson);
        foreach (var required in items.Where(i => i.Required))
        {
            var answered = request.Results.FirstOrDefault(r =>
                string.Equals(r.Key, required.Key, StringComparison.OrdinalIgnoreCase));
            if (answered is null)
                return ApiResponse<int>.FailResponse($"Required checklist item missing: {required.Label}");
        }

        var overall = string.IsNullOrWhiteSpace(request.OverallResult)
            ? InspectionResultCalculator.ComputeOverall(request.Results)
            : request.OverallResult.Trim();
        overall = NormalizeStatus(overall);

        var normalized = request.Results
            .Select(r => r with { Status = NormalizeStatus(r.Status) })
            .ToList();

        var driverName = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT FullName FROM Drivers WHERE Id = @Id",
            new { Id = driverId.Value },
            cancellationToken: cancellationToken));

        var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            INSERT INTO Inspections
                (TenantId, VehicleId, TemplateId, DriverId, InspectedBy, InspectionDate,
                 OdometerReading, Result, ResultsJson, PhotosJson, Comments, CreatedAt, CreatedBy, IsDeleted)
            VALUES
                (@TenantId, @VehicleId, @TemplateId, @DriverId, @InspectedBy, GETUTCDATE(),
                 @Odometer, @Result, @ResultsJson, N'[]', @Comments, GETUTCDATE(), @CreatedBy, 0);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """,
            new
            {
                TenantId = tenantId,
                request.VehicleId,
                TemplateId = templateId,
                DriverId = driverId.Value,
                InspectedBy = driverName ?? currentUser.UserId?.ToString(),
                Odometer = request.OdometerReading,
                Result = overall,
                ResultsJson = InspectionResultCalculator.SerializeResults(normalized),
                Comments = request.Comments,
                CreatedBy = currentUser.UserId?.ToString()
            },
            cancellationToken: cancellationToken));

        var photoUrls = new List<string>();
        if (request.Photos is { Count: > 0 })
        {
            foreach (var photo in request.Photos.Take(8))
            {
                if (photo.Length <= 0 || photo.Length > 8 * 1024 * 1024) continue;
                var stored = await fileStorage.SaveAsync(
                    photo.Content,
                    photo.FileName,
                    photo.ContentType,
                    $"inspections/{tenantId}/{id}",
                    cancellationToken);
                photoUrls.Add(stored.ReadUrl);
            }
        }

        string? signatureUrl = null;
        if (request.Signature is { Length: > 0 and <= 4 * 1024 * 1024 })
        {
            var stored = await fileStorage.SaveAsync(
                request.Signature.Content,
                string.IsNullOrWhiteSpace(request.Signature.FileName) ? "signature.png" : request.Signature.FileName,
                request.Signature.ContentType,
                $"inspections/{tenantId}/{id}",
                cancellationToken);
            signatureUrl = stored.ReadUrl;
        }

        await connection.ExecuteAsync(new CommandDefinition(
            @"UPDATE Inspections SET PhotosJson = @Photos, SignatureUrl = @Signature WHERE Id = @Id",
            new
            {
                Id = id,
                Photos = InspectionResultCalculator.SerializePhotos(photoUrls),
                Signature = signatureUrl
            },
            cancellationToken: cancellationToken));

        return ApiResponse<int>.SuccessResponse(id, "Inspection submitted.");
    }

    private static string NormalizeStatus(string status)
    {
        var s = status.Trim();
        if (s.Equals("fail", StringComparison.OrdinalIgnoreCase)) return "Fail";
        if (s.Equals("warning", StringComparison.OrdinalIgnoreCase)) return "Warning";
        return "Pass";
    }
}
