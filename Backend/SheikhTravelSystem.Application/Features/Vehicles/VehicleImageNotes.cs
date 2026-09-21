namespace SheikhTravelSystem.Application.Features.Vehicles;

public static class VehicleImageNotes
{
    public const string PrimarySuffix = "|primary";
    public const string LegacyPrimary = "primary";
    public static readonly string[] Angles = ["Front", "Side", "Back"];

    public static string NormalizeAngle(string? notes)
    {
        var angle = ParseAngle(notes);
        return Angles.Contains(angle, StringComparer.OrdinalIgnoreCase) ? angle : "Side";
    }

    public static string ParseAngle(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes)) return string.Empty;
        var value = notes.Trim();
        if (value.Equals(LegacyPrimary, StringComparison.OrdinalIgnoreCase)) return string.Empty;
        var pipe = value.IndexOf('|', StringComparison.Ordinal);
        return pipe >= 0 ? value[..pipe].Trim() : value;
    }

    public static bool IsPrimary(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes)) return false;
        var value = notes.Trim();
        return value.Equals(LegacyPrimary, StringComparison.OrdinalIgnoreCase)
               || value.Contains(PrimarySuffix, StringComparison.OrdinalIgnoreCase);
    }

    public static string WithAngle(string angle, bool isPrimary = false)
    {
        var normalized = NormalizeAngle(angle);
        return isPrimary ? normalized + PrimarySuffix : normalized;
    }

    public static string SetPrimary(string? notes)
    {
        var cleared = ClearPrimary(notes);
        if (string.IsNullOrWhiteSpace(cleared)) return LegacyPrimary;
        return cleared + PrimarySuffix;
    }

    public static string ClearPrimary(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes)) return string.Empty;
        var value = notes.Trim();
        if (value.Equals(LegacyPrimary, StringComparison.OrdinalIgnoreCase)) return string.Empty;
        return value.Replace(PrimarySuffix, "", StringComparison.OrdinalIgnoreCase).Trim();
    }
}
