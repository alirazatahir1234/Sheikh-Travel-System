using System.Data;
using Dapper;
using SheikhTravelSystem.Application.Features.TravelNexus.Documents.DTOs;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories.TravelNexus;

/// <summary>Requirement inserts shared by the visa case repository (checklist snapshot) — callers own the transaction.</summary>
internal static class TravelDocumentSql
{
    /// <summary>
    /// Inserts one requirement per seed for the case's active applicant with that traveler, skipping document types the
    /// applicant already has. Returns the number of rows inserted.
    /// </summary>
    public static async Task<int> InsertSeedsAsync(IDbConnection connection, IDbTransaction tx, int tenantId, int caseId,
        IReadOnlyList<TravelRequirementSeed> seeds, int? userId, CancellationToken ct)
    {
        var inserted = 0;
        foreach (var s in seeds)
        {
            inserted += await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO TravelApplicantDocuments (TenantId, VisaCaseId, ApplicantId, DocumentTypeId, ChecklistItemId,
                    IsMandatory, MinValidityDays, Instructions, Status, CreatedBy)
                SELECT @TenantId, @CaseId, a.Id, @DocumentTypeId, @ChecklistItemId, @IsMandatory, @MinValidityDays, @Instructions,
                       N'Missing', @UserId
                FROM TravelVisaApplicants a
                WHERE a.TenantId = @TenantId AND a.VisaCaseId = @CaseId AND a.TravelerId = @TravelerId AND a.IsDeleted = 0
                  AND NOT EXISTS (
                      SELECT 1 FROM TravelApplicantDocuments d
                      WHERE d.TenantId = @TenantId AND d.ApplicantId = a.Id AND d.DocumentTypeId = @DocumentTypeId AND d.IsDeleted = 0)
                """,
                new
                {
                    TenantId = tenantId, CaseId = caseId, s.TravelerId, s.DocumentTypeId, s.ChecklistItemId, s.IsMandatory,
                    s.MinValidityDays, s.Instructions, UserId = userId
                }, tx, cancellationToken: ct));
        }
        return inserted;
    }
}
