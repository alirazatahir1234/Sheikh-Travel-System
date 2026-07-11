namespace SheikhTravelSystem.Application.Features.Vehicles;

public static class VehicleDocumentTypes
{
    public const string VehicleImage = "VehicleImage";
    public const string Registration = "Registration";
    public const string Insurance = "Insurance";
    public const string RoadTax = "RoadTax";
    public const string Fitness = "Fitness";
    public const string Permit = "Permit";

    public static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        VehicleImage,
        Registration,
        Insurance,
        RoadTax,
        Fitness,
        Permit
    };

    public static bool IsAllowed(string? documentType) =>
        !string.IsNullOrWhiteSpace(documentType) && Allowed.Contains(documentType.Trim());

    public static bool IsRegistration(string? documentType) =>
        string.Equals(documentType, Registration, StringComparison.OrdinalIgnoreCase);

    public static bool IsVehicleImage(string? documentType) =>
        string.Equals(documentType, VehicleImage, StringComparison.OrdinalIgnoreCase);
}
