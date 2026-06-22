using Dapper;
using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Drivers.DTOs;

namespace SheikhTravelSystem.Application.Features.Drivers.Commands;

// ── Update document status (Approve / Reject) ────────────────────────────────

public record UpdateDocumentStatusCommand(
    int DriverId,
    int DocumentId,
    string Status,
    string? RejectionReason = null) : IRequest<ApiResponse<bool>>;

public class UpdateDocumentStatusCommandValidator : AbstractValidator<UpdateDocumentStatusCommand>
{
    private static readonly HashSet<string> AllowedStatuses =
        new(StringComparer.OrdinalIgnoreCase) { "Approved", "Rejected" };

    public UpdateDocumentStatusCommandValidator()
    {
        RuleFor(x => x.DriverId).GreaterThan(0);
        RuleFor(x => x.DocumentId).GreaterThan(0);
        RuleFor(x => x.Status).Must(s => AllowedStatuses.Contains(s))
            .WithMessage("Status must be 'Approved' or 'Rejected'.");
        RuleFor(x => x.RejectionReason)
            .NotEmpty()
            .MaximumLength(500)
            .When(x => string.Equals(x.Status, "Rejected", StringComparison.OrdinalIgnoreCase));
    }
}

public class UpdateDocumentStatusCommandHandler(
    IDbConnectionFactory dbFactory,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdateDocumentStatusCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(
        UpdateDocumentStatusCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();
        var reviewer = currentUser.UserId?.ToString() ?? "api";

        var rows = await connection.ExecuteAsync(new CommandDefinition(
            @"UPDATE ComplianceDocuments
              SET    Status          = @Status,
                     RejectionReason = @RejectionReason,
                     UpdatedBy       = @Reviewer,
                     UpdatedAt       = GETUTCDATE()
              WHERE  Id          = @DocumentId
                AND  EntityType  = N'Driver'
                AND  EntityId    = @DriverId
                AND  TenantId    = @TenantId
                AND  IsDeleted   = 0",
            new
            {
                request.Status,
                request.RejectionReason,
                Reviewer  = reviewer,
                request.DocumentId,
                request.DriverId,
                TenantId  = tenantId
            },
            cancellationToken: cancellationToken));

        if (rows == 0)
            throw new NotFoundException("Document", request.DocumentId);

        return ApiResponse<bool>.SuccessResponse(true,
            request.Status == "Approved" ? "Document approved." : "Document rejected.");
    }
}

// ── Add reviewer note ─────────────────────────────────────────────────────────

public record AddDriverReviewNoteCommand(
    int DriverId,
    string Note,
    string? DocumentType = null) : IRequest<ApiResponse<DriverReviewNoteDto>>;

public class AddDriverReviewNoteCommandValidator : AbstractValidator<AddDriverReviewNoteCommand>
{
    public AddDriverReviewNoteCommandValidator()
    {
        RuleFor(x => x.DriverId).GreaterThan(0);
        RuleFor(x => x.Note).NotEmpty().MaximumLength(1000);
    }
}

public class AddDriverReviewNoteCommandHandler(
    IDbConnectionFactory dbFactory,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<AddDriverReviewNoteCommand, ApiResponse<DriverReviewNoteDto>>
{
    public async Task<ApiResponse<DriverReviewNoteDto>> Handle(
        AddDriverReviewNoteCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();
        var createdBy = currentUser.UserId?.ToString() ?? "api";

        var driverExists = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT CASE WHEN EXISTS(SELECT 1 FROM Drivers WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0) THEN 1 ELSE 0 END",
            new { Id = request.DriverId, TenantId = tenantId },
            cancellationToken: cancellationToken));

        if (!driverExists)
            throw new NotFoundException("Driver", request.DriverId);

        var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            @"INSERT INTO DriverReviewNotes (TenantId, DriverId, Note, DocumentType, CreatedBy, CreatedAt, IsDeleted)
              VALUES (@TenantId, @DriverId, @Note, @DocumentType, @CreatedBy, GETUTCDATE(), 0);
              SELECT CAST(SCOPE_IDENTITY() AS INT);",
            new
            {
                TenantId     = tenantId,
                request.DriverId,
                request.Note,
                request.DocumentType,
                CreatedBy    = createdBy
            },
            cancellationToken: cancellationToken));

        var dto = new DriverReviewNoteDto(id, request.Note, request.DocumentType, createdBy, DateTime.UtcNow);
        return ApiResponse<DriverReviewNoteDto>.SuccessResponse(dto, "Note added.");
    }
}
