using System.Text.RegularExpressions;

namespace SheikhTravelSystem.Infrastructure.Services.Ocr;

/// <summary>
/// Heuristics for PK CNIC-style identity fields from plain line OCR text (Azure, Paddle, etc.).
/// </summary>
public static class CnicRawTextIdentityParser
{
    /// <summary>
    /// Azure <c>AnalyzeResult.Content</c> is often one fused block without line breaks before NADRA labels.
    /// Insert newlines so "Name … Father …" and DOB/Gender parsers work.
    /// </summary>
    public static string NormalizeOcrLayoutForCnic(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText)) return rawText;
        var t = rawText.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
        static string BreakBefore(string input, string pattern) =>
            Regex.Replace(input, @"(?<=[^\n])" + pattern, "\n", RegexOptions.IgnoreCase);

        t = BreakBefore(t, @"(?=\bFather(?:'s)?\s*Name\b)");
        t = BreakBefore(t, @"(?=\bFathers\s*Name\b)");
        t = BreakBefore(t, @"(?=\bMother(?:'s)?\s*Name\b)");
        t = BreakBefore(t, @"(?=\bHusband(?:'s)?\s*Name\b)");
        t = BreakBefore(t, @"(?=\bWife(?:'s)?\s*Name\b)");
        t = BreakBefore(t, @"(?=\bGender\b)");
        t = BreakBefore(t, @"(?=\bDate\s+of\s+Birth\b)");
        t = BreakBefore(t, @"(?=\bCountry\s+of\s+Stay\b)");
        t = BreakBefore(t, @"(?=\bNationality\b)");
        t = BreakBefore(t, @"(?=\bDate\s+of\s+Issue\b)");
        t = BreakBefore(t, @"(?=\bDate\s+of\s+Expiry\b)");
        t = BreakBefore(t, @"(?=\bIdentity\s+Number\b)");
        t = BreakBefore(t, @"(?=\bDocument\s+Number\b)");
        // NADRA back: Urdu address blocks often fused on one line.
        t = BreakBefore(t, @"(?=موجودہ\s*پتہ)");
        t = BreakBefore(t, @"(?=مستقل\s*پتہ)");
        // NADRA "Name" label often glued to previous token (e.g. "CardName Ali …").
        t = BreakBefore(t, @"(?=\bName\s*[:\s•·\.])");
        t = BreakBefore(t, @"(?=\bName\s+[A-Z][a-z])");
        t = Regex.Replace(t, @"(?<=[^\n])(?=\b\d{5}-\d{7}-\d\b)", "\n");
        return t;
    }

    /// <summary>Heuristic for PK CNIC backs: prefer English (Latin) address lines; Urdu only when no English detected.</summary>
    public static string? GuessAddressFromRaw(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText)) return null;
        rawText = NormalizeOcrLayoutForCnic(rawText);

        var latin = GuessLatinAddressLinesFromRaw(rawText);
        if (!string.IsNullOrWhiteSpace(latin))
            return latin.Trim();

        // Prefer permanent (Mustaqil) block over generic Urdu lines (often Maujuda first).
        var perm = ExtractPermanentUrduForTranslation(rawText);
        if (!string.IsNullOrWhiteSpace(perm))
            return perm.Trim();

        var urdu = GuessUrduAddressBlockFromRaw(rawText);
        return string.IsNullOrWhiteSpace(urdu) ? null : urdu.Trim();
    }

    private static string? GuessLatinAddressLinesFromRaw(string rawText)
    {
        var lines = rawText.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var addrHints = new Regex(
            @"house|street|road|phase|block|colony|town|sector|city|district|near|st\.|plot|village|tehsil|ضلع",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        var candidates = lines
            .Where(l => l.Length > 18 && addrHints.IsMatch(l) && !Regex.IsMatch(l, @"\b\d{5}-\d{7}-\d\b"))
            .Take(4)
            .ToArray();
        if (candidates.Length > 0)
            return string.Join(", ", candidates);

        var longLatin = lines
            .Where(l =>
                l.Length >= 22 &&
                l.Count(char.IsAsciiLetter) >= 16 &&
                CountArabicScriptLetters(l) < 4 &&
                (l.Any(char.IsDigit) || l.Length >= 36) &&
                !Regex.IsMatch(l, @"\b\d{5}-\d{7}-\d\b") &&
                !Regex.IsMatch(l, @"\b(Father|Mother|Gender|Country|Identity)\b", RegexOptions.IgnoreCase))
            .Take(3)
            .ToArray();
        return longLatin.Length == 0 ? null : string.Join(", ", longLatin);
    }

    private static string? GuessUrduAddressBlockFromRaw(string rawText)
    {
        var lines = rawText.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var parts = new List<string>();
        foreach (var line in lines)
        {
            if (line.Length < 12) continue;
            if (Regex.IsMatch(line, @"\b\d{5}-\d{7}-\d\b")) continue;
            if (IsAddressHeaderNoise(line)) continue;
            if (Regex.IsMatch(line, @"^موجودہ\s*پتہ")) continue;
            var arabic = CountArabicScriptLetters(line);
            if (arabic < 5) continue;
            parts.Add(line.Trim());
            if (parts.Count >= 4) break;
        }

        return parts.Count == 0 ? null : string.Join(" ", parts);
    }

    private static int CountArabicScriptLetters(string line) =>
        line.Count(static c => c is >= '\u0600' and <= '\u06FF'
            or >= '\u0750' and <= '\u077F'
            or >= '\u08A0' and <= '\u08FF'
            or >= '\uFB50' and <= '\uFDFF');

    private static bool IsAddressHeaderNoise(string line)
    {
        var t = line.TrimStart();
        return t.StartsWith("Pakistan", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("Islamic", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("National Identity", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("Identity Card", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("Registrar", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// NADRA CNIC often exposes the Latin holder name only in line OCR, not in idDocument key/value fields.
    /// Prefer explicit "Name" labels, then text before "Father", then best Latin line.
    /// </summary>
    public static string? GuessLatinNameFromRaw(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText)) return null;
        rawText = NormalizeOcrLayoutForCnic(rawText);

        var fromLabel = ExtractHolderNameFromLabeledRaw(rawText);
        if (!string.IsNullOrWhiteSpace(fromLabel)) return fromLabel;

        var fused = ExtractHolderNameFusedBeforeFather(rawText);
        if (!string.IsNullOrWhiteSpace(fused)) return fused;

        var beforeFather = ExtractHolderNameBeforeFatherBlock(rawText);
        if (!string.IsNullOrWhiteSpace(beforeFather)) return beforeFather;

        return PickBestLatinHolderNameLine(rawText);
    }

    /// <summary>Full OCR blob (often one line): "Ali Raza Tahir Father's Name …".</summary>
    private static string? ExtractHolderNameFusedBeforeFather(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText)) return null;
        var t = CollapseUnicodeSpaces(rawText);

        // Title Case / mixed case name immediately before Father label (same line).
        var patterns = new[]
        {
            @"(?is)(?<n>(?:\b[A-Z][a-z]+){2,5})\s+(?:Father'?\s*s?\s*Name|Father\s*Name)\b",
            @"(?is)(?<n>(?:\b[A-Z][a-z]+){2,5})\s+\bFather\b(?!\s*Name)",
            @"(?is)(?<n>(?:\b[A-Z]{2,}\s+){2,}[A-Z]{2,})\s+(?:Father'?\s*s?\s*Name|Father\s*Name)\b",
            @"(?is)(?<n>(?:\b[a-z][a-z]+){2,5})\s+(?:Father'?\s*s?\s*Name|Father\s*Name)\b",
        };
        foreach (var p in patterns)
        {
            var m = Regex.Match(t, p);
            if (!m.Success) continue;
            var n = NormalizeNameWhitespace(m.Groups["n"].Value);
            n = Regex.Replace(n, @"\s*[-–—|]+\s*$", "").Trim();
            if (Regex.IsMatch(n, @"\b(Father|Mother|Gender|Date|Country|Identity|National|Card)\b", RegexOptions.IgnoreCase)) continue;
            if (!IsLikelyLatinPersonName(n)) continue;
            return n;
        }

        return null;
    }

    private static string CollapseUnicodeSpaces(string s) =>
        Regex.Replace(s.Trim(), @"\p{Zs}+", " ");

    /// <summary>Same-line: "Name: Ali Raza Tahir" (must not be Father/Mother line).</summary>
    private static string? ExtractHolderNameFromLabeledRaw(string rawText)
    {
        foreach (var line in rawText.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (Regex.IsMatch(line, @"\b(Father|Mother|Husband|Wife)\b", RegexOptions.IgnoreCase)) continue;

            var sameLine = Regex.Match(
                line,
                @"(?i)(?:^|\s)(?:Name|Holder\s*Name)\s*[:\s.-]+\s*(?<n>.+)$");
            if (sameLine.Success)
            {
                var n = NormalizeNameWhitespace(sameLine.Groups["n"].Value);
                var cut = Regex.Match(n, @"\s+(?:Father|Fathar|Mother|Husband|Wife|Gender|Date|Country|Identity)\b", RegexOptions.IgnoreCase);
                if (cut.Success) n = n[..cut.Index].Trim();
                n = Regex.Replace(n, @"\s*[-–—|]+\s*$", "").Trim();
                if (IsLikelyLatinPersonName(n)) return n;
            }
        }

        var lines = rawText.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var i = 0; i < lines.Length - 1; i++)
        {
            if (!Regex.IsMatch(lines[i], @"^(?i)Name\s*:?\s*$")) continue;
            var next = NormalizeNameWhitespace(lines[i + 1]);
            if (IsLikelyLatinPersonName(next) && !Regex.IsMatch(next, @"\b(Father|Mother)\b", RegexOptions.IgnoreCase))
                return next;
        }

        // Full-text: "Name Ali Raza Tahir Father…" (possibly fused); prefer longest plausible capture.
        const string blobPattern =
            @"(?is)\bName\s*[:\s.-]+\s*(?<n>[A-Za-z][A-Za-z\s.'-]{2,120}?)(?=\s*(?:Father|Fathar|Mother|Husband|Wife|Gender|Date\s+of\s+Birth|Country|Identity|\z))";
        string? bestName = null;
        foreach (Match m in Regex.Matches(rawText, blobPattern))
        {
            var cand = NormalizeNameWhitespace(m.Groups["n"].Value);
            var cut = Regex.Match(cand, @"\s+(?:Father|Fathar|Mother|Husband|Wife|Gender|Date|Country|Identity)\b", RegexOptions.IgnoreCase);
            if (cut.Success) cand = cand[..cut.Index].Trim();
            cand = Regex.Replace(cand, @"\s*[-–—|]+\s*$", "").Trim();
            if (!IsLikelyLatinPersonName(cand)) continue;
            if (bestName is null || cand.Length > bestName.Length) bestName = cand;
        }

        if (bestName is not null) return bestName;

        return null;
    }

    /// <summary>Holder is usually the Latin line immediately above "Father …" on CNIC front, or on the same line before Father.</summary>
    private static string? ExtractHolderNameBeforeFatherBlock(string rawText)
    {
        var lines = rawText.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var i = 0; i < lines.Length; i++)
        {
            if (!Regex.IsMatch(lines[i], @"\b(?:Father|Fathar|Fathers|FATHER)\b", RegexOptions.IgnoreCase)) continue;

            var same = Regex.Match(
                lines[i],
                @"(?is)(?<n>(?:\b[A-Z][a-z]+){2,5})\s+(?:Father'?\s*s?\s*Name|Father)\b");
            if (same.Success)
            {
                var n0 = NormalizeNameWhitespace(same.Groups["n"].Value);
                n0 = Regex.Replace(n0, @"\s*[-–—|]+\s*$", "").Trim();
                if (IsLikelyLatinPersonName(n0)) return n0;
            }

            if (i <= 0)
                return null;

            for (var j = i - 1; j >= 0; j--)
            {
                var line = NormalizeNameWhitespace(CollapseUnicodeSpaces(lines[j]));
                if (line.Length < 4) continue;
                if (Regex.IsMatch(line, @"\b\d{5}-\d{7}-\d\b")) continue;
                if (Regex.IsMatch(line, @"^(?i)(?:Name|Mother|Country|Gender|Date|Identity|National|Card)\b")) continue;
                if (IsLikelyLatinPersonName(line)) return line;
            }

            return null;
        }

        return null;
    }

    private static string? PickBestLatinHolderNameLine(string rawText)
    {
        var lines = rawText.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        string? best = null;
        var bestScore = -1;

        foreach (var rawLine in lines)
        {
            var line = NormalizeNameWhitespace(CollapseUnicodeSpaces(rawLine));
            line = Regex.Replace(line, @"[\s.,;:|]+$", "").Trim();
            if (line.Length is < 4 or > 220) continue;
            if (Regex.IsMatch(line, @"\b\d{5}-\d{7}-\d\b")) continue;

            var lower = line.ToLowerInvariant();
            if (ContainsHeaderNoise(lower)) continue;
            if (Regex.IsMatch(line, @"\b(Father|Mother|Husband|Wife)\b", RegexOptions.IgnoreCase)) continue;

            if (!Regex.IsMatch(line, @"^[A-Za-z][A-Za-z\s.'-]+$")) continue;

            var tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var score = tokens.Length >= 3 ? 30 : tokens.Length * 10;
            if (Regex.IsMatch(line, @"^[A-Z][a-z]+(?:\s+[A-Z][a-z]+)+$")) score += 20;
            if (Regex.IsMatch(line, @"^[A-Z][A-Z\s.'-]+$") && tokens.Length >= 2) score += 10;

            if (score > bestScore && IsLikelyLatinPersonName(line))
            {
                bestScore = score;
                best = line;
            }
        }

        return best;
    }

    private static bool ContainsHeaderNoise(string lower) =>
        lower.Contains("pakistan", StringComparison.Ordinal) ||
        lower.Contains("islamic", StringComparison.Ordinal) ||
        lower.Contains("republic", StringComparison.Ordinal) ||
        lower.Contains("national identity", StringComparison.Ordinal) ||
        lower.Contains("identity card", StringComparison.Ordinal) ||
        lower.Contains("registrar general", StringComparison.Ordinal);

    private static string NormalizeNameWhitespace(string s) =>
        Regex.Replace(CollapseUnicodeSpaces(s.Trim()), @"\s+", " ");

    public static bool IsLikelyLatinPersonName(string s)
    {
        if (string.IsNullOrWhiteSpace(s) || s.Length is < 4 or > 120) return false;
        if (Regex.IsMatch(s, @"\d{5}-\d{7}-\d")) return false;

        var letters = s.Count(char.IsLetter);
        if (letters / (double)s.Length < 0.62) return false;

        var tokens = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return tokens.Length >= 2 || (tokens.Length == 1 && s.Length >= 10);
    }

    /// <summary>Azure "Name" fields sometimes map to UI labels (e.g. "Cnic page") instead of the holder.</summary>
    public static bool IsObviousNonPersonName(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return true;
        if (ContainsArabicScript(s)) return false;
        var t = s.Trim();
        if (t.Length < 2) return true;
        if (Regex.IsMatch(t, @"(?i)\b(cnic|id\s*card|identity\s*card|document|holder\s*photo|nadr|nadra|card\s*holder|page\s*\d|scan)\b"))
            return true;
        return false;
    }

    /// <summary>Urdu permanent-address block only (مستقل پتہ), for translation.</summary>
    public static string? ExtractPermanentUrduForTranslation(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText)) return null;
        rawText = NormalizeOcrLayoutForCnic(rawText);
        var lines = rawText.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        var start = -1;
        for (var i = 0; i < lines.Count; i++)
        {
            if (Regex.IsMatch(lines[i], @"مستقل\s*پتہ|(?i)PERMANENT\s+ADDRESS"))
            {
                start = i;
                break;
            }
        }

        if (start < 0)
            return TryExtractPermanentUrduSingleLine(rawText);

        var parts = new List<string>();
        var first = lines[start];
        var afterLabel = Regex.Replace(first, @"^.*?(?:مستقل\s*پتہ|(?i)PERMANENT\s+ADDRESS)\s*[:\s.-]*", "").Trim();
        if (afterLabel.Length >= 6 && CountArabicScriptLetters(afterLabel) >= 4)
            parts.Add(afterLabel);

        for (var j = start + 1; j < lines.Count; j++)
        {
            var line = lines[j].Trim();
            if (line.Length < 6) break;
            if (Regex.IsMatch(line, @"موجودہ\s*پتہ|(?i)CURRENT\s+ADDRESS|TEMPORARY")) break;
            if (IsAddressHeaderNoise(line)) break;
            if (Regex.IsMatch(line, @"^گمشدہ|^Registrar", RegexOptions.IgnoreCase)) break;
            if (Regex.IsMatch(line, @"^\d{12,}$")) break;
            if (CountArabicScriptLetters(line) < 3 && line.Count(char.IsAsciiLetter) > 20) break;
            parts.Add(line);
            if (parts.Count >= 6) break;
        }

        return parts.Count == 0 ? null : string.Join(" ", parts).Trim();
    }

    /// <summary>When OCR returns one long line, Mustaqil and Maujuda may appear without line breaks.</summary>
    private static string? TryExtractPermanentUrduSingleLine(string rawText)
    {
        var m = Regex.Match(
            rawText,
            @"(?s)مستقل\s*پتہ\s*[:\s\u0640.-]*\s*(?<u>[^\n]+?)(?=\s*موجودہ\s*پتہ|(?i)CURRENT\s+ADDRESS|\z)");
        if (!m.Success)
            m = Regex.Match(
                rawText,
                @"(?is)PERMANENT\s+ADDRESS\s*[:\s.-]*\s*(?<u>[^\n]+?)(?=\s*CURRENT\s+ADDRESS|\z)");
        if (!m.Success)
            return null;
        var u = m.Groups["u"].Value.Trim();
        u = Regex.Replace(u, @"^\s*[:\u061B\u061F؛]\s*", "").Trim();
        if (u.Length < 6 || CountArabicScriptLetters(u) < 4)
            return null;
        return u;
    }

    public static string? GuessFatherNameFromRaw(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText)) return null;
        rawText = NormalizeOcrLayoutForCnic(rawText);
        var lines = rawText.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var line in lines)
        {
            var m = Regex.Match(
                line,
                @"^(?i)(?:Father|Father'?s\s*Name|Husband|Husband'?s\s*Name)\s*[:\s.-]+\s*(?<n>.+)$");
            if (!m.Success) continue;
            var n = NormalizeNameWhitespace(m.Groups["n"].Value);
            n = Regex.Replace(n, @"\s*[-–—|]+\s*$", "").Trim();
            if (IsLikelyLatinPersonName(n)) return n;
        }

        var fatherIdx = -1;
        for (var i = 0; i < lines.Length; i++)
        {
            if (!Regex.IsMatch(lines[i], @"\b(?:Father|Fathar|Husband)\b", RegexOptions.IgnoreCase)) continue;
            fatherIdx = i;
            break;
        }

        if (fatherIdx >= 0)
        {
            var same = lines[fatherIdx];
            var inline = Regex.Match(same, @"^(?i)(?:Father|Fathar|Husband)\b\s*[:\s.-]+\s*(?<n>.+)$");
            if (inline.Success)
            {
                var n2 = NormalizeNameWhitespace(inline.Groups["n"].Value);
                if (IsLikelyLatinPersonName(n2)) return n2;
            }

            if (fatherIdx + 1 < lines.Length)
            {
                var next = NormalizeNameWhitespace(lines[fatherIdx + 1]);
                if (IsLikelyLatinPersonName(next) && !Regex.IsMatch(next, @"^(?i)(?:Mother|Gender|Country)")) return next;
            }
        }

        // NADRA back sometimes has والد کا نام (father's name) in Urdu only.
        var urduFather = Regex.Match(
            rawText,
            @"(?u)(?:والد|والدہ)\s*(?:کا\s*)?نام\s*[:\s\u0640.-]+\s*(?<n>[^\n\r]{3,120})");
        if (urduFather.Success)
        {
            var n3 = NormalizeNameWhitespace(urduFather.Groups["n"].Value);
            n3 = Regex.Replace(n3, @"\s*[-–—|]+\s*$", "").Trim();
            if (n3.Length >= 3 && (IsLikelyLatinPersonName(n3) || CountArabicScriptLetters(n3) >= 4))
                return n3;
        }

        return null;
    }

    public static string? GuessNationalityFromRaw(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText)) return null;
        rawText = NormalizeOcrLayoutForCnic(rawText);
        if (Regex.IsMatch(rawText, @"(?i)Country\s+of\s+Stay\s*[:\s.-]*\s*Pakistan")) return "Pakistani";
        if (Regex.IsMatch(rawText, @"(?i)Nationality\s*[:\s.-]*\s*Pakistan")) return "Pakistani";
        return null;
    }

    public static string? GuessGenderFromRaw(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText)) return null;
        rawText = NormalizeOcrLayoutForCnic(rawText);
        var m = Regex.Match(rawText, @"(?i)\bGender\s*[:\s.-]*\s*\n?\s*([MF])\b");
        if (!m.Success) return null;
        return m.Groups[1].Value.Equals("M", StringComparison.OrdinalIgnoreCase) ? "Male" : "Female";
    }

    /// <summary>PK CNIC DOB line often DD.MM.YYYY.</summary>
    public static string? GuessDateOfBirthFromRaw(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText)) return null;
        rawText = NormalizeOcrLayoutForCnic(rawText);
        var m = Regex.Match(rawText, @"(?i)\bDate\s+of\s+Birth\s*[:\s.-]*\s*(\d{2})\.(\d{2})\.(\d{4})\b");
        if (!m.Success)
            m = Regex.Match(rawText, @"(?i)\bDate\s+of\s+Birth\s*[:\s.-]*\s*\n\s*(\d{2})\.(\d{2})\.(\d{4})\b");
        if (!m.Success) return null;
        if (!int.TryParse(m.Groups[1].Value, out var dd) || !int.TryParse(m.Groups[2].Value, out var mm) || !int.TryParse(m.Groups[3].Value, out var yyyy))
            return null;
        try
        {
            var dt = new DateOnly(yyyy, mm, dd);
            return dt.ToString("yyyy-MM-dd");
        }
        catch
        {
            return null;
        }
    }

    public static bool ContainsArabicScript(string text) =>
        !string.IsNullOrEmpty(text) && CountArabicScriptLetters(text) >= 4;
}
