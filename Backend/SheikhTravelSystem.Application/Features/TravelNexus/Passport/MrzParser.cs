using System.Text;
using SheikhTravelSystem.Application.Features.TravelNexus.Configuration;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Passport;

public sealed record MrzPassportData(
    string Surname,
    string GivenNames,
    string PassportNumber,
    string? IssuingCountryCode,
    string? NationalityCode,
    DateTime? DateOfBirth,
    string? Gender,
    DateTime? ExpiryDate,
    bool ChecksumValid,
    IReadOnlyList<string> InvalidFields);

/// <summary>
/// ICAO 9303 TD3 (passport) machine-readable zone parser. Finds the two 44-character lines in OCR text,
/// corrects common OCR confusions in numeric fields, validates every check digit and maps country codes
/// to ISO alpha-2. Pure and side-effect free.
/// </summary>
public static class MrzParser
{
    private const int LineLength = 44;

    public static MrzPassportData? Parse(string? rawText, DateTime? today = null)
    {
        var (line1, line2) = FindLines(rawText);
        if (line1 is null || line2 is null) return null;
        var now = (today ?? DateTime.UtcNow).Date;

        line2 = FixNumeric(line2, [9, 13, 14, 15, 16, 17, 18, 19, 21, 22, 23, 24, 25, 26, 27, 42, 43]);

        var invalid = new List<string>();
        var number = line2[..9];
        if (!CheckDigitOk(number, line2[9])) invalid.Add("PassportNumber");
        if (!CheckDigitOk(line2[13..19], line2[19])) invalid.Add("DateOfBirth");
        if (!CheckDigitOk(line2[21..27], line2[27])) invalid.Add("ExpiryDate");
        if (line2[42] != '<' || line2[28..42].Any(c => c != '<'))
        {
            if (!CheckDigitOk(line2[28..42], line2[42])) invalid.Add("PersonalNumber");
        }
        var composite = line2[..10] + line2[13..20] + line2[21..43];
        if (!CheckDigitOk(composite, line2[43])) invalid.Add("Composite");

        var names = line1[5..].Split("<<", 2);
        var surname = Clean(names[0]);
        var given = names.Length > 1 ? Clean(names[1]) : string.Empty;

        return new MrzPassportData(
            surname,
            given,
            number.Replace("<", string.Empty),
            Country(line1[2..5]),
            Country(line2[10..13]),
            ParseDate(line2[13..19], now, isExpiry: false),
            line2[20] switch { 'M' => "Male", 'F' => "Female", _ => null },
            ParseDate(line2[21..27], now, isExpiry: true),
            invalid.Count == 0,
            invalid);
    }

    /// <summary>ICAO 7-3-1 weighted check digit; '&lt;' = 0, A–Z = 10–35.</summary>
    public static int CheckDigit(string value)
    {
        int[] weights = [7, 3, 1];
        var sum = 0;
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            var v = c switch
            {
                >= '0' and <= '9' => c - '0',
                >= 'A' and <= 'Z' => c - 'A' + 10,
                _ => 0
            };
            sum += v * weights[i % 3];
        }
        return sum % 10;
    }

    private static bool CheckDigitOk(string value, char digit)
        => digit is >= '0' and <= '9' && CheckDigit(value) == digit - '0';

    private static (string? Line1, string? Line2) FindLines(string? rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText)) return (null, null);
        var lines = rawText.Replace('«', '<').Replace("\r", string.Empty)
            .Split('\n')
            .Select(l => new string(l.ToUpperInvariant().Where(c => c is (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '<').ToArray()))
            .Where(l => l.Length >= 30)
            .ToList();

        for (var i = 0; i < lines.Count - 1; i++)
        {
            var l1 = lines[i];
            if (l1.Length >= 30 && l1[0] == 'P' && l1.Contains("<<", StringComparison.Ordinal))
                return (Normalize(l1), Normalize(lines[i + 1]));
        }
        return (null, null);
    }

    private static string Normalize(string line)
        => line.Length >= LineLength ? line[..LineLength] : line.PadRight(LineLength, '<');

    /// <summary>OCR often reads 0 as O, 1 as I, 5 as S, 8 as B in numeric MRZ fields.</summary>
    private static string FixNumeric(string line, int[] positions)
    {
        var sb = new StringBuilder(line);
        foreach (var p in positions)
        {
            sb[p] = sb[p] switch { 'O' or 'Q' or 'D' => '0', 'I' or 'L' => '1', 'S' => '5', 'B' => '8', 'Z' => '2', var c => c };
        }
        return sb.ToString();
    }

    private static string Clean(string s) => string.Join(' ', s.Replace('<', ' ').Split(' ', StringSplitOptions.RemoveEmptyEntries));

    private static string? Country(string code)
    {
        var trimmed = code.Replace("<", string.Empty);
        return TravelCountryCatalog.Alpha3ToAlpha2.TryGetValue(trimmed, out var a2) ? a2 : null;
    }

    private static DateTime? ParseDate(string yymmdd, DateTime today, bool isExpiry)
    {
        if (!int.TryParse(yymmdd[..2], out var yy) || !int.TryParse(yymmdd[2..4], out var mm) || !int.TryParse(yymmdd[4..6], out var dd))
            return null;
        // Birth dates are in the past; expiry dates within this century.
        var century = isExpiry ? 2000 : (yy > today.Year % 100 ? 1900 : 2000);
        try { return new DateTime(century + yy, mm, dd); }
        catch (ArgumentOutOfRangeException) { return null; }
    }
}
