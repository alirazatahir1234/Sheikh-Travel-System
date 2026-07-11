namespace SheikhTravelSystem.Application.Features.Vehicles;

public static class VehicleUploadLimits
{
    public const long MaxFileBytes = 5 * 1024 * 1024;
    public const int MaxFileMegabytes = 5;

    public static readonly HashSet<string> VehicleImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".png", ".webp", ".gif" };

    public static readonly HashSet<string> DocumentExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".png", ".pdf" };

    public static bool IsAllowedExtension(string documentType, string fileName)
    {
        var ext = Path.GetExtension(fileName);
        if (VehicleDocumentTypes.IsVehicleImage(documentType))
            return VehicleImageExtensions.Contains(ext);
        return DocumentExtensions.Contains(ext);
    }
}
