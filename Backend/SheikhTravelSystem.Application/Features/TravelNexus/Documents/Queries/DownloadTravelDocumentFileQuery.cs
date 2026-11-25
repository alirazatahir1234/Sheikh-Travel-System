using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Documents.Commands;
using SheikhTravelSystem.Application.Features.TravelNexus.Documents.DTOs;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Documents.Queries;

/// <summary>
/// Streams a stored document file. Requires Travel.Document.Download (controller) and a visible case.
/// The access is audited <b>fail-closed</b> before the file is opened: no audit row, no file.
/// </summary>
public record DownloadTravelDocumentFileQuery(int FileId) : IRequest<TravelDocumentDownload>;

public class DownloadTravelDocumentFileQueryHandler(
    ITravelDocumentRepository repository,
    ITravelVisaCaseRepository cases,
    ITravelDocumentStorage storage,
    IAuditEngine auditEngine,
    ITravelBranchScopeResolver scopeResolver,
    ITenantContext tenantContext)
    : IRequestHandler<DownloadTravelDocumentFileQuery, TravelDocumentDownload>
{
    public async Task<TravelDocumentDownload> Handle(DownloadTravelDocumentFileQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var scope = await scopeResolver.ResolveAsync(tenantId, cancellationToken);
        var file = await repository.GetFileAsync(request.FileId, tenantId, cancellationToken)
            ?? throw new NotFoundException("TravelDocumentFile", request.FileId);
        _ = await cases.GetStateAsync(file.VisaCaseId, tenantId, scope, cancellationToken)
            ?? throw new NotFoundException("TravelDocumentFile", request.FileId);

        await auditEngine.RecordAsync(new AuditEventWrite(
            EventKey: AuditEventRegistrySeed.ResolveEventKey("TravelDocumentFile", "Download"),
            EntityType: "TravelDocumentFile",
            EntityId: file.Id,
            Action: "Download",
            Message: $"Downloaded travel document file {file.Id} of visa case {file.VisaCaseId}",
            FailPolicy: AuditFailPolicy.FailClosed), cancellationToken);

        var stream = await storage.OpenReadAsync(file.StorageKey, cancellationToken);
        return new TravelDocumentDownload(stream, file.ContentType, file.OriginalFileName);
    }
}
