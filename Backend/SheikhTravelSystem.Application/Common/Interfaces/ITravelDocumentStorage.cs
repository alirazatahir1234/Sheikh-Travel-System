namespace SheikhTravelSystem.Application.Common.Interfaces;

/// <summary>
/// Private storage for TravelNexus applicant documents (passport scans, bank statements…).
/// Deliberately has <b>no URL method</b>: files are only ever streamed through the authenticated, audited
/// download endpoint. Never backed by the public <c>/uploads</c> folder or long-lived SAS links.
/// </summary>
public interface ITravelDocumentStorage
{
    /// <summary>Stores the bytes under a tenant/case-scoped random key and returns the key.</summary>
    Task<string> SaveAsync(byte[] content, string contentType, string extension, int tenantId, int visaCaseId,
        CancellationToken cancellationToken = default);

    /// <summary>Opens a read stream for a key previously returned by <see cref="SaveAsync"/>.</summary>
    Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default);

    Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default);
}
