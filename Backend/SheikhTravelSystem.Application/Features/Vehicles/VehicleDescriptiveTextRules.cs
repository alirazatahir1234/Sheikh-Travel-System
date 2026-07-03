namespace SheikhTravelSystem.Application.Features.Vehicles;

internal static class VehicleDescriptiveTextRules
{
    internal static bool IsValid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return true;

        var text = value.Trim();
        if (text.All(char.IsDigit))
            return false;

        var letters = text.Count(char.IsLetter);
        if (letters == 0)
            return false;

        var digits = text.Count(char.IsDigit);
        if (digits > letters * 2)
            return false;

        if (digits > 0 && letters < 2 && text.Length > 4)
            return false;

        return true;
    }
}
