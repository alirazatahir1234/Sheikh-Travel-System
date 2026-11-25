using System.Data;
using Dapper;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Documents;
using SheikhTravelSystem.Application.Features.TravelNexus.Documents.DTOs;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories.TravelNexus;

/// <summary>
/// Applicant document requirements and file metadata (file bytes live in ITravelDocumentStorage).
/// Root predicate <c>TenantId = @TenantId</c>; writes lock the case in its expected status and write history in the
/// same transaction.
/// </summary>
public sealed class TravelDocumentRepository(IDbConnectionFactory dbFactory) : ITravelDocumentRepository
{
    public async Task<IReadOnlyList<TravelApplicantDocumentDto>> GetCaseRequirementsAsync(int caseId, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return (await connection.QueryAsync<TravelApplicantDocumentDto>(new CommandDefinition("""
            SELECT r.Id, r.ApplicantId, a.TravelerId, CONCAT(t.GivenNames, ' ', t.Surname) AS TravelerName,
                   r.DocumentTypeId, d.Code AS DocumentTypeCode, d.Name AS DocumentTypeName, r.IsMandatory,
                   CAST(CASE WHEN r.ChecklistItemId IS NULL THEN 1 ELSE 0 END AS BIT) AS IsAdHoc,
                   r.MinValidityDays, r.Instructions, r.Status, r.ExpiryDate, r.ReviewNote,
                   u.FullName AS ReviewedByName, r.ReviewedAt, r.CurrentFileId
            FROM TravelApplicantDocuments r
            INNER JOIN TravelVisaApplicants a ON a.Id = r.ApplicantId AND a.TenantId = r.TenantId AND a.IsDeleted = 0
            INNER JOIN TravelTravelers t ON t.Id = a.TravelerId AND t.TenantId = r.TenantId
            INNER JOIN TravelDocumentTypes d ON d.Id = r.DocumentTypeId AND d.TenantId = r.TenantId
            LEFT JOIN Users u ON u.Id = r.ReviewedBy AND u.TenantId = r.TenantId
            WHERE r.VisaCaseId = @CaseId AND r.TenantId = @TenantId AND r.IsDeleted = 0
            ORDER BY a.Id, r.IsMandatory DESC, d.Name
            """, new { CaseId = caseId, TenantId = tenantId }, cancellationToken: cancellationToken))).ToList();
    }

    public async Task<IReadOnlyList<TravelDocumentFileDto>> GetCaseFilesAsync(int caseId, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return (await connection.QueryAsync<TravelDocumentFileDto>(new CommandDefinition("""
            SELECT f.Id, f.ApplicantDocumentId, f.OriginalFileName, f.ContentType, f.SizeBytes, u.FullName AS UploadedByName, f.UploadedAt
            FROM TravelDocumentFiles f
            INNER JOIN TravelApplicantDocuments r ON r.Id = f.ApplicantDocumentId AND r.TenantId = f.TenantId AND r.IsDeleted = 0
            LEFT JOIN Users u ON u.Id = f.UploadedBy AND u.TenantId = f.TenantId
            WHERE r.VisaCaseId = @CaseId AND f.TenantId = @TenantId AND f.IsDeleted = 0
            """, new { CaseId = caseId, TenantId = tenantId }, cancellationToken: cancellationToken))).ToList();
    }

    public async Task<IReadOnlyList<TravelRequirementState>> GetRequirementStatesAsync(int caseId, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return (await connection.QueryAsync<TravelRequirementState>(new CommandDefinition("""
            SELECT r.ApplicantId, r.IsMandatory, r.Status
            FROM TravelApplicantDocuments r
            INNER JOIN TravelVisaApplicants a ON a.Id = r.ApplicantId AND a.TenantId = r.TenantId
                   AND a.IsDeleted = 0 AND a.Decision <> N'Withdrawn'
            WHERE r.VisaCaseId = @CaseId AND r.TenantId = @TenantId AND r.IsDeleted = 0
            """, new { CaseId = caseId, TenantId = tenantId }, cancellationToken: cancellationToken))).ToList();
    }

    public async Task<TravelRequirementFacts?> GetRequirementAsync(int requirementId, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<TravelRequirementFacts>(new CommandDefinition("""
            SELECT r.Id, r.VisaCaseId, r.ApplicantId, r.DocumentTypeId, d.Name AS DocumentTypeName, r.IsMandatory,
                   r.MinValidityDays, r.Status, r.ChecklistItemId, r.CurrentFileId
            FROM TravelApplicantDocuments r
            INNER JOIN TravelDocumentTypes d ON d.Id = r.DocumentTypeId AND d.TenantId = r.TenantId
            WHERE r.Id = @Id AND r.TenantId = @TenantId AND r.IsDeleted = 0
            """, new { Id = requirementId, TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    public async Task<bool> RequirementExistsAsync(int tenantId, int applicantId, int documentTypeId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT COUNT(1) FROM TravelApplicantDocuments
            WHERE TenantId = @TenantId AND ApplicantId = @ApplicantId AND DocumentTypeId = @DocumentTypeId AND IsDeleted = 0
            """, new { TenantId = tenantId, ApplicantId = applicantId, DocumentTypeId = documentTypeId }, cancellationToken: cancellationToken)) > 0;
    }

    public async Task<bool> FileHashExistsAsync(int tenantId, int requirementId, string sha256, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT COUNT(1) FROM TravelDocumentFiles
            WHERE TenantId = @TenantId AND ApplicantDocumentId = @RequirementId AND Sha256 = @Sha AND IsDeleted = 0
            """, new { TenantId = tenantId, RequirementId = requirementId, Sha = sha256 }, cancellationToken: cancellationToken)) > 0;
    }

    public async Task<TravelDocumentFileFacts?> GetFileAsync(int fileId, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<TravelDocumentFileFacts>(new CommandDefinition("""
            SELECT f.Id, r.VisaCaseId, f.StorageKey, f.OriginalFileName, f.ContentType
            FROM TravelDocumentFiles f
            INNER JOIN TravelApplicantDocuments r ON r.Id = f.ApplicantDocumentId AND r.TenantId = f.TenantId
            WHERE f.Id = @Id AND f.TenantId = @TenantId AND f.IsDeleted = 0
            """, new { Id = fileId, TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    public async Task<int?> AddFileAsync(int tenantId, int caseId, int requirementId, string expectedCaseStatus, TravelNewDocumentFile file,
        int? userId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();

        var previous = await LockRequirementAsync(connection, tx, tenantId, caseId, requirementId, expectedCaseStatus, cancellationToken);
        if (previous is null)
        {
            tx.Rollback();
            return null;
        }

        var fileId = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            INSERT INTO TravelDocumentFiles (TenantId, ApplicantDocumentId, StorageKey, OriginalFileName, ContentType, SizeBytes, Sha256, UploadedBy)
            VALUES (@TenantId, @RequirementId, @StorageKey, @OriginalFileName, @ContentType, @SizeBytes, @Sha256, @UserId);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """,
            new { TenantId = tenantId, RequirementId = requirementId, file.StorageKey, file.OriginalFileName, file.ContentType, file.SizeBytes, file.Sha256, UserId = userId },
            tx, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE TravelApplicantDocuments SET Status = N'Uploaded', CurrentFileId = @FileId, ReviewNote = NULL,
                ReviewedBy = NULL, ReviewedAt = NULL, UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @RequirementId AND TenantId = @TenantId
            """, new { FileId = fileId, RequirementId = requirementId, TenantId = tenantId }, tx, cancellationToken: cancellationToken));

        await HistoryAsync(connection, tx, tenantId, caseId, requirementId, "DocumentUploaded", previous, "Uploaded", null, userId, cancellationToken);
        tx.Commit();
        return fileId;
    }

    public async Task<int?> RequestAsync(int tenantId, int caseId, string expectedCaseStatus, RequestTravelDocumentDto dto, int? userId,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();

        if (!await LockCaseAsync(connection, tx, tenantId, caseId, expectedCaseStatus, cancellationToken))
        {
            tx.Rollback();
            return null;
        }

        var id = await connection.ExecuteScalarAsync<int?>(new CommandDefinition("""
            INSERT INTO TravelApplicantDocuments (TenantId, VisaCaseId, ApplicantId, DocumentTypeId, ChecklistItemId, IsMandatory,
                Instructions, Status, CreatedBy)
            SELECT @TenantId, @CaseId, a.Id, d.Id, NULL, @IsMandatory, @Instructions, N'Missing', @UserId
            FROM TravelVisaApplicants a
            INNER JOIN TravelDocumentTypes d ON d.Id = @DocumentTypeId AND d.TenantId = @TenantId AND d.IsDeleted = 0
            WHERE a.Id = @ApplicantId AND a.VisaCaseId = @CaseId AND a.TenantId = @TenantId AND a.IsDeleted = 0
              AND NOT EXISTS (SELECT 1 FROM TravelApplicantDocuments x
                              WHERE x.TenantId = @TenantId AND x.ApplicantId = a.Id AND x.DocumentTypeId = d.Id AND x.IsDeleted = 0);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """,
            new { TenantId = tenantId, CaseId = caseId, dto.ApplicantId, dto.DocumentTypeId, dto.IsMandatory, dto.Instructions, UserId = userId },
            tx, cancellationToken: cancellationToken));
        if (id is null)
        {
            tx.Rollback();
            return null;
        }

        await HistoryAsync(connection, tx, tenantId, caseId, id.Value, "DocumentRequested", null, "Missing", dto.Instructions, userId, cancellationToken);
        tx.Commit();
        return id;
    }

    public async Task<bool> RemoveAdHocAsync(int tenantId, int caseId, int requirementId, string expectedCaseStatus, int? userId,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();

        var previous = await LockRequirementAsync(connection, tx, tenantId, caseId, requirementId, expectedCaseStatus, cancellationToken);
        if (previous is null)
        {
            tx.Rollback();
            return false;
        }

        var rows = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE TravelApplicantDocuments SET IsDeleted = 1, UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id AND TenantId = @TenantId AND ChecklistItemId IS NULL AND IsDeleted = 0
            """, new { Id = requirementId, TenantId = tenantId }, tx, cancellationToken: cancellationToken));
        if (rows == 0)
        {
            tx.Rollback();
            return false;
        }

        await HistoryAsync(connection, tx, tenantId, caseId, requirementId, "DocumentRemoved", previous, null, null, userId, cancellationToken);
        tx.Commit();
        return true;
    }

    public async Task<bool> ReviewAsync(int tenantId, int caseId, int requirementId, string expectedCaseStatus, IReadOnlyList<string> expectedStatuses,
        string newStatus, string? note, DateTime? expiryDate, int? userId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();

        var previous = await LockRequirementAsync(connection, tx, tenantId, caseId, requirementId, expectedCaseStatus, cancellationToken);
        if (previous is null || !expectedStatuses.Contains(previous))
        {
            tx.Rollback();
            return false;
        }

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE TravelApplicantDocuments SET
                Status = @NewStatus, ReviewNote = @Note, ExpiryDate = COALESCE(@ExpiryDate, ExpiryDate),
                ReviewedBy = @UserId, ReviewedAt = SYSUTCDATETIME(), UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id AND TenantId = @TenantId AND Status = @Previous
            """, new { Id = requirementId, TenantId = tenantId, NewStatus = newStatus, Note = note, ExpiryDate = expiryDate, UserId = userId, Previous = previous },
            tx, cancellationToken: cancellationToken));

        var eventType = newStatus switch { "Verified" => "DocumentVerified", "Rejected" => "DocumentRejected", _ => "DocumentWaived" };
        await HistoryAsync(connection, tx, tenantId, caseId, requirementId, eventType, previous, newStatus, note, userId, cancellationToken);
        tx.Commit();
        return true;
    }

    public async Task<PagedResult<TravelMissingDocumentsItemDto>> GetMissingAsync(int tenantId, TravelBranchScope scope, int page, int pageSize,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        var scopeClauses = new List<string> { "1 = 1" };
        TravelSql.ApplyBranchScope(scope, "v.BranchId", scopeClauses, p);
        var scopeSql = string.Join(" AND ", scopeClauses);

        var outstanding = $"""
            FROM TravelVisaCases v
            INNER JOIN TravelApplicantDocuments r ON r.VisaCaseId = v.Id AND r.TenantId = v.TenantId AND r.IsDeleted = 0
                   AND r.IsMandatory = 1 AND r.Status IN (N'Missing', N'Uploaded', N'Rejected')
            INNER JOIN TravelVisaApplicants a ON a.Id = r.ApplicantId AND a.TenantId = v.TenantId AND a.IsDeleted = 0 AND a.Decision <> N'Withdrawn'
            WHERE v.TenantId = @TenantId AND v.IsDeleted = 0 AND v.Status IN (N'DocumentsPending', N'DocumentsVerified') AND {scopeSql}
            """;

        var total = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            $"SELECT COUNT(DISTINCT v.Id) {outstanding}", p, cancellationToken: cancellationToken));

        p.Add("Offset", (page - 1) * pageSize);
        p.Add("PageSize", pageSize);
        var items = (await connection.QueryAsync<TravelMissingDocumentsItemDto>(new CommandDefinition($"""
            SELECT v.Id AS VisaCaseId, v.CaseNumber, v.CustomerId, c.FullName AS CustomerName, co.Name AS CountryName,
                   vt.Name AS VisaTypeName, v.Status, v.IntendedTravelDate, x.Outstanding AS MandatoryOutstanding
            FROM (SELECT v.Id, COUNT(1) AS Outstanding {outstanding} GROUP BY v.Id) x
            INNER JOIN TravelVisaCases v ON v.Id = x.Id AND v.TenantId = @TenantId
            INNER JOIN Customers c ON c.Id = v.CustomerId AND c.TenantId = v.TenantId
            INNER JOIN TravelCountries co ON co.Code = v.CountryCode
            INNER JOIN TravelVisaTypes vt ON vt.Id = v.VisaTypeId AND vt.TenantId = v.TenantId
            ORDER BY CASE WHEN v.IntendedTravelDate IS NULL THEN 1 ELSE 0 END, v.IntendedTravelDate, v.Id
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
            """, p, cancellationToken: cancellationToken))).ToList();

        return new PagedResult<TravelMissingDocumentsItemDto> { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static async Task<bool> LockCaseAsync(IDbConnection connection, IDbTransaction tx, int tenantId, int caseId, string expectedStatus, CancellationToken ct)
        => await connection.ExecuteScalarAsync<int?>(new CommandDefinition("""
            SELECT Id FROM TravelVisaCases WITH (UPDLOCK, ROWLOCK)
            WHERE Id = @CaseId AND TenantId = @TenantId AND IsDeleted = 0 AND Status = @Expected
            """, new { CaseId = caseId, TenantId = tenantId, Expected = expectedStatus }, tx, cancellationToken: ct)) is not null;

    /// <summary>Locks the case (expected status) and the requirement; returns the requirement's current status or null.</summary>
    private static async Task<string?> LockRequirementAsync(IDbConnection connection, IDbTransaction tx, int tenantId, int caseId,
        int requirementId, string expectedCaseStatus, CancellationToken ct)
    {
        if (!await LockCaseAsync(connection, tx, tenantId, caseId, expectedCaseStatus, ct)) return null;
        return await connection.ExecuteScalarAsync<string?>(new CommandDefinition("""
            SELECT Status FROM TravelApplicantDocuments WITH (UPDLOCK, ROWLOCK)
            WHERE Id = @Id AND VisaCaseId = @CaseId AND TenantId = @TenantId AND IsDeleted = 0
            """, new { Id = requirementId, CaseId = caseId, TenantId = tenantId }, tx, cancellationToken: ct));
    }

    /// <summary>Document events go to the case history; ApplicantId is filled from the requirement.</summary>
    private static Task HistoryAsync(IDbConnection connection, IDbTransaction tx, int tenantId, int caseId, int requirementId,
        string eventType, string? from, string? to, string? note, int? userId, CancellationToken ct)
        => connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO TravelVisaStatusHistory (TenantId, VisaCaseId, ApplicantId, EventType, FromValue, ToValue, Note, ChangedBy)
            SELECT @TenantId, @CaseId, r.ApplicantId, @EventType, @From, @To,
                   LEFT(CONCAT(d.Name, CASE WHEN @Note IS NULL THEN N'' ELSE CONCAT(N': ', @Note) END), 1000), @UserId
            FROM TravelApplicantDocuments r
            INNER JOIN TravelDocumentTypes d ON d.Id = r.DocumentTypeId AND d.TenantId = r.TenantId
            WHERE r.Id = @RequirementId AND r.TenantId = @TenantId
            """,
            new { TenantId = tenantId, CaseId = caseId, RequirementId = requirementId, EventType = eventType, From = from, To = to, Note = note, UserId = userId },
            tx, cancellationToken: ct));
}
