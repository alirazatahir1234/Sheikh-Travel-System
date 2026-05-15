using System.Globalization;
using System.Text.RegularExpressions;
using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Ocr.DTOs;

namespace SheikhTravelSystem.Infrastructure.Services.Ocr;

public class AzureDocumentIntelligenceProvider : IOcrProvider
{
    private readonly OcrOptions _options;

    public AzureDocumentIntelligenceProvider(IOptions<OcrOptions> options)
    {
        _options = options.Value;
    }

    public string ProviderName => "AzureDocumentIntelligence";

    public async Task<IdentityOcrResult> ExtractAsync(Stream fileStream, string fileName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.AzureEndpoint) || string.IsNullOrWhiteSpace(_options.AzureApiKey))
        {
            throw new InvalidOperationException("Azure OCR is not configured.");
        }

        var credential = new AzureKeyCredential(_options.AzureApiKey);
        var client = new DocumentAnalysisClient(new Uri(_options.AzureEndpoint), credential);

        if (fileStream.CanSeek)
            fileStream.Position = 0;

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _options.AzureTimeoutSeconds)));

        var operation = await client.AnalyzeDocumentAsync(
            WaitUntil.Completed,
            "prebuilt-idDocument",
            fileStream,
            cancellationToken: timeoutCts.Token);

        var analyze = operation.Value;
        var document = analyze.Documents.FirstOrDefault();
        if (document is null)
        {
            var raw = ExtractRawText(analyze);
            var cnic = !string.IsNullOrWhiteSpace(raw) ? OcrHeuristics.ExtractAndNormalizeIdentityNumber(raw) : null;
            var name = !string.IsNullOrWhiteSpace(raw) ? CnicRawTextIdentityParser.GuessLatinNameFromRaw(raw) : null;
            var addr = !string.IsNullOrWhiteSpace(raw) ? CnicRawTextIdentityParser.GuessAddressFromRaw(raw) : null;
            var father = !string.IsNullOrWhiteSpace(raw) ? CnicRawTextIdentityParser.GuessFatherNameFromRaw(raw) : null;
            var nat = !string.IsNullOrWhiteSpace(raw) ? CnicRawTextIdentityParser.GuessNationalityFromRaw(raw) : null;
            var dobFromRaw = !string.IsNullOrWhiteSpace(raw) ? CnicRawTextIdentityParser.GuessDateOfBirthFromRaw(raw) : null;
            var genderFromRaw = !string.IsNullOrWhiteSpace(raw) ? CnicRawTextIdentityParser.GuessGenderFromRaw(raw) : null;
            return new IdentityOcrResult(
                Provider: ProviderName,
                Confidence: 0,
                FallbackUsed: false,
                FullName: name,
                IdentityNumber: cnic,
                Address: addr,
                DateOfBirth: dobFromRaw,
                Gender: genderFromRaw,
                RawText: raw,
                FatherName: father,
                Nationality: nat);
        }

        var rawText = ExtractRawText(analyze);

        var fullName = BuildName(document);
        var identityNumber = FirstField(document, "DocumentNumber", "IdNumber", "IdentityNumber", "CNICNumber", "PersonalNumber");
        identityNumber = OcrHeuristics.ExtractAndNormalizeIdentityNumber(identityNumber ?? string.Empty) ?? identityNumber;
        if (string.IsNullOrWhiteSpace(identityNumber) && !string.IsNullOrWhiteSpace(rawText))
            identityNumber = OcrHeuristics.ExtractAndNormalizeIdentityNumber(rawText);

        var address = FirstField(document, "Address", "AddressStreet", "AddressCity", "Region");
        if (string.IsNullOrWhiteSpace(address) && !string.IsNullOrWhiteSpace(rawText))
            address = CnicRawTextIdentityParser.GuessAddressFromRaw(rawText);

        var dob = NormalizeIsoOrLocalDate(FirstField(document, "DateOfBirth", "BirthDate"));
        var gender = NormalizeSexDisplay(FirstField(document, "Sex", "Gender"));

        if (string.IsNullOrWhiteSpace(fullName) && !string.IsNullOrWhiteSpace(rawText))
            fullName = CnicRawTextIdentityParser.GuessLatinNameFromRaw(rawText);

        var fatherName = FirstField(document, "FatherName", "Father", "ParentName", "FathersName");
        if (string.IsNullOrWhiteSpace(fatherName) && !string.IsNullOrWhiteSpace(rawText))
            fatherName = CnicRawTextIdentityParser.GuessFatherNameFromRaw(rawText);

        var nationality = FirstField(document, "Nationality", "CountryOfStay", "Country");
        nationality = NormalizeNationalityField(nationality);
        if (string.IsNullOrWhiteSpace(nationality) && !string.IsNullOrWhiteSpace(rawText))
            nationality = CnicRawTextIdentityParser.GuessNationalityFromRaw(rawText);

        var confidence = ToPercent(AverageConfidence(
            document,
            "DocumentNumber", "IdNumber", "IdentityNumber", "FirstName", "LastName", "FullName", "Name", "GivenNames", "Address"));
        return new IdentityOcrResult(
            Provider: ProviderName,
            Confidence: confidence,
            FallbackUsed: false,
            FullName: fullName,
            IdentityNumber: identityNumber,
            Address: address,
            DateOfBirth: dob,
            Gender: gender,
            RawText: rawText,
            FatherName: fatherName,
            Nationality: nationality);
    }

    private static string? BuildName(AnalyzedDocument doc)
    {
        var fullName = FirstField(doc, "FullName", "Name", "DocumentName", "HolderName", "GivenNames", "CardHolderName", "SurnameAndGivenNames");
        if (!string.IsNullOrWhiteSpace(fullName)) return fullName.Trim();

        var first = FirstField(doc, "FirstName", "GivenName", "GivenNames");
        var middle = FirstField(doc, "MiddleName");
        var last = FirstField(doc, "LastName", "Surname", "FamilyName");
        var parts = new[] { first, middle, last }.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!.Trim());
        var joined = string.Join(" ", parts);
        return string.IsNullOrWhiteSpace(joined) ? null : joined;
    }

    private static string? FirstField(AnalyzedDocument doc, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!doc.Fields.TryGetValue(key, out var field) || field is null) continue;
            var t = TryGetFieldText(field);
            if (!string.IsNullOrWhiteSpace(t)) return t;
        }
        return null;
    }

    private static string? TryGetFieldText(DocumentField field)
    {
        if (!string.IsNullOrWhiteSpace(field.Content)) return field.Content.Trim();
        try
        {
            return field.FieldType switch
            {
                DocumentFieldType.String => field.Value.AsString()?.Trim(),
                DocumentFieldType.Date => DateOnly.FromDateTime(field.Value.AsDate().UtcDateTime.Date).ToString("yyyy-MM-dd"),
                DocumentFieldType.CountryRegion => field.Value.AsCountryRegion()?.Trim(),
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }

    private static string? NormalizeSexDisplay(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var t = Regex.Replace(s.Trim(), @"\s+", "");
        if (t.Equals("M", StringComparison.OrdinalIgnoreCase)) return "Male";
        if (t.Equals("F", StringComparison.OrdinalIgnoreCase)) return "Female";
        if (s.Contains("Male", StringComparison.OrdinalIgnoreCase)) return "Male";
        if (s.Contains("Female", StringComparison.OrdinalIgnoreCase)) return "Female";
        return s.Trim();
    }

    private static string? NormalizeIsoOrLocalDate(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        s = s.Trim();
        if (Regex.IsMatch(s, @"^\d{4}-\d{2}-\d{2}$")) return s;
        var m = Regex.Match(s, @"^(\d{2})[./-](\d{2})[./-](\d{4})$");
        if (m.Success
            && int.TryParse(m.Groups[1].Value, CultureInfo.InvariantCulture, out var d)
            && int.TryParse(m.Groups[2].Value, CultureInfo.InvariantCulture, out var mo)
            && int.TryParse(m.Groups[3].Value, CultureInfo.InvariantCulture, out var y))
        {
            try
            {
                return new DateOnly(y, mo, d).ToString("yyyy-MM-dd");
            }
            catch
            {
                return null;
            }
        }

        if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            return DateOnly.FromDateTime(dt).ToString("yyyy-MM-dd");
        return s;
    }

    private static string? NormalizeNationalityField(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var t = s.Trim();
        if (t.Equals("PAK", StringComparison.OrdinalIgnoreCase)) return "Pakistani";
        return t;
    }

    private static float AverageConfidence(AnalyzedDocument doc, params string[] keys)
    {
        var values = new List<float>();
        foreach (var key in keys)
        {
            if (doc.Fields.TryGetValue(key, out var field) && field is not null && field.Confidence.HasValue)
            {
                values.Add(field.Confidence.Value);
            }
        }
        return values.Count == 0 ? 0 : values.Average();
    }

    private static int ToPercent(float confidence)
    {
        if (confidence <= 0) return 0;
        return (int)Math.Round(Math.Clamp(confidence, 0, 1) * 100);
    }

    private static string? ExtractRawText(AnalyzeResult analyze)
    {
        var fromContent = analyze.Content?.Trim();
        var lineParts = analyze.Pages?
            .SelectMany(p => p.Lines)
            .Select(l => l.Content)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .ToArray() ?? Array.Empty<string>();
        var fromLines = lineParts.Length == 0 ? null : string.Join('\n', lineParts);

        if (string.IsNullOrEmpty(fromContent) && string.IsNullOrEmpty(fromLines))
            return null;

        string merged;
        if (string.IsNullOrEmpty(fromContent))
            merged = fromLines!;
        else if (string.IsNullOrEmpty(fromLines))
            merged = fromContent;
        else if (fromLines!.Contains(fromContent, StringComparison.Ordinal))
            merged = fromLines;
        else if (fromContent.Contains(fromLines, StringComparison.Ordinal))
            merged = fromContent;
        else
            merged = fromContent + "\n\n" + fromLines;

        return CnicRawTextIdentityParser.NormalizeOcrLayoutForCnic(merged);
    }

}
