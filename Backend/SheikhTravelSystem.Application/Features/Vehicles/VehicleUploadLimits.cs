namespace SheikhTravelSystem.Application.Features.Vehicles;

public static class VehicleUploadLimits
{
    public const long MaxFileBytes = 2 * 1024 * 1024;
    public const int MaxFileMegabytes = 2;

    public static readonly HashSet<string> VehicleImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".png", ".webp", ".gif" };

    public static readonly HashSet<string> DocumentExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".png", ".pdf" };

    public static bool IsAllowedExtension(string documentType, string fileName)
    {
        var ext = Path.GetExtension(fileName);
        return string.Equals(documentType, "VehicleImage", StringComparison.OrdinalIgnoreCase)
            ? VehicleImageExtensions.Contains(ext)
            : DocumentExtensions.Contains(ext);
    }
}
