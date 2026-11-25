namespace SheikhTravelSystem.Infrastructure.Services.Storage;

/// <summary>Configuration section <c>TravelDocuments</c> for private TravelNexus document storage.</summary>
public sealed class TravelDocumentStorageOptions
{
    public const string SectionName = "TravelDocuments";

    /// <summary>Dedicated private Azure container (no anonymous access, no SAS ever issued).</summary>
    public string AzureContainerName { get; set; } = "travel-documents";

    /// <summary>
    /// Local root (relative to the content root unless absolute). Must stay outside the publicly served
    /// <c>FileStorage:RootPath</c> (<c>uploads</c>).
    /// </summary>
    public string LocalRoot { get; set; } = "private-storage/travel-documents";
}
