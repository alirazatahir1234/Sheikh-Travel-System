namespace SheikhTravelSystem.Application.Features.Vehicles;

internal static class VehicleImageNotes
{
    internal const string PrimarySuffix = "|primary";
    internal const string LegacyPrimary = "primary";
    internal static readonly string[] Angles = ["Front", "Side", "Back"];

    internal static string NormalizeAngle(string? notes)
    {
        var angle = ParseAngle(notes);
        return Angles.Contains(angle, StringComparer.OrdinalIgnoreCase) ? angle : "Side";
    }

    internal static string ParseAngle(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes)) return string.Empty;
        var value = notes.Trim();
        if (value.Equals(LegacyPrimary, StringComparison.OrdinalIgnoreCase)) return string.Empty;
        var pipe = value.IndexOf('|', StringComparison.Ordinal);
        return pipe >= 0 ? value[..pipe].Trim() : value;
    }

    internal static bool IsPrimary(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes)) return false;
        var value = notes.Trim();
        return value.Equals(LegacyPrimary, StringComparison.OrdinalIgnoreCase)
               || value.Contains(PrimarySuffix, StringComparison.OrdinalIgnoreCase);
    }

    internal static string WithAngle(string angle, bool isPrimary = false)
    {
        var normalized = NormalizeAngle(angle);
        return isPrimary ? normalized + PrimarySuffix : normalized;
    }

    internal static string SetPrimary(string? notes)
    {
        var cleared = ClearPrimary(notes);
        if (string.IsNullOrWhiteSpace(cleared)) return LegacyPrimary;
        return cleared + PrimarySuffix;
    }

    internal static string ClearPrimary(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes)) return string.Empty;
        var value = notes.Trim();
        if (value.Equals(LegacyPrimary, StringComparison.OrdinalIgnoreCase)) return string.Empty;
        return value.Replace(PrimarySuffix, "", StringComparison.OrdinalIgnoreCase).Trim();
    }
}
