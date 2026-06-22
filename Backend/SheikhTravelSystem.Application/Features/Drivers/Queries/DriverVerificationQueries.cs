using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Drivers.DTOs;

namespace SheikhTravelSystem.Application.Features.Drivers.Queries;

// ── GET /drivers/{id}/documents ───────────────────────────────────────────────

public record GetDriverDocumentsQuery(int DriverId)
    : IRequest<ApiResponse<IReadOnlyList<DriverDocumentDetailedDto>>>;

public class GetDriverDocumentsQueryHandler(
    IDbConnectionFactory dbFactory,
    ITenantContext tenantContext)
    : IRequestHandler<GetDriverDocumentsQuery, ApiResponse<IReadOnlyList<DriverDocumentDetailedDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<DriverDocumentDetailedDto>>> Handle(
        GetDriverDocumentsQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        var rows = (await connection.QueryAsync<DriverDocumentDetailedDto>(
            new CommandDefinition(
                @"SELECT Id,
                         DocumentType,
                         FileUrl,
                         ExpiryDate,
                         Status,
                         RejectionReason,
                         UpdatedBy   AS ReviewedBy,
                         UpdatedAt   AS ReviewedAt,
                         CreatedAt
                  FROM   ComplianceDocuments
                  WHERE  EntityType = N'Driver'
                    AND  EntityId   = @DriverId
                    AND  TenantId   = @TenantId
                    AND  IsDeleted  = 0
                  ORDER BY CreatedAt DESC",
                new { request.DriverId, TenantId = tenantId },
                cancellationToken: cancellationToken))).ToList();

        return ApiResponse<IReadOnlyList<DriverDocumentDetailedDto>>.SuccessResponse(rows);
    }
}

// ── GET /drivers/{id}/verification/review-notes ───────────────────────────────

public record GetDriverReviewNotesQuery(int DriverId)
    : IRequest<ApiResponse<IReadOnlyList<DriverReviewNoteDto>>>;

public class GetDriverReviewNotesQueryHandler(
    IDbConnectionFactory dbFactory,
    ITenantContext tenantContext)
    : IRequestHandler<GetDriverReviewNotesQuery, ApiResponse<IReadOnlyList<DriverReviewNoteDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<DriverReviewNoteDto>>> Handle(
        GetDriverReviewNotesQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        var rows = (await connection.QueryAsync<DriverReviewNoteDto>(
            new CommandDefinition(
                @"SELECT Id,
                         Note,
                         DocumentType,
                         CreatedBy,
                         CreatedAt
                  FROM   DriverReviewNotes
                  WHERE  DriverId  = @DriverId
                    AND  TenantId  = @TenantId
                    AND  IsDeleted = 0
                  ORDER BY CreatedAt DESC",
                new { request.DriverId, TenantId = tenantId },
                cancellationToken: cancellationToken))).ToList();

        return ApiResponse<IReadOnlyList<DriverReviewNoteDto>>.SuccessResponse(rows);
    }
}
