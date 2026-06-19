namespace SheikhTravelSystem.Application.Features.Drivers;

internal static class DriverFieldHelper
{
    public static string BuildFullName(string firstName, string lastName)
    {
        var first = firstName.Trim();
        var last = lastName.Trim();
        return string.IsNullOrWhiteSpace(last) ? first : $"{first} {last}";
    }

    public static string GenerateDriverCode() =>
        $"DRV-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
}
