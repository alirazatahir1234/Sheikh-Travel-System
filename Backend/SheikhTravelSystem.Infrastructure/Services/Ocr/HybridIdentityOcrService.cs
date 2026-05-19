using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Ocr.DTOs;

namespace SheikhTravelSystem.Infrastructure.Services.Ocr;

public class HybridIdentityOcrService : IIdentityOcrService
{
    private readonly PaddleOcrProvider _paddle;
    private readonly AzureDocumentIntelligenceProvider _azure;
    private readonly OcrOptions _options;
    private readonly IUrduToEnglishTranslator _translator;
    private readonly ILogger<HybridIdentityOcrService> _logger;

    public HybridIdentityOcrService(
        PaddleOcrProvider paddle,
        AzureDocumentIntelligenceProvider azure,
        IOptions<OcrOptions> options,
        IUrduToEnglishTranslator translator,
        ILogger<HybridIdentityOcrService> logger)
    {
        _paddle = paddle;
        _azure = azure;
        _options = options.Value;
        _translator = translator;
        _logger = logger;
    }

    public async Task<IdentityOcrResult> ExtractAsync(
        Stream fileStream,
        string fileName,
        ExtractIdentityOcrRequest request,
        CancellationToken cancellationToken = default)
    {
        var bytes = await ReadAllBytesAsync(fileStream, cancellationToken);

        return request.Mode switch
        {
            OcrMode.PaddleOnly => await PostProcessAsync(
                await _paddle.ExtractAsync(new MemoryStream(bytes), fileName, cancellationToken),
                cancellationToken),
            OcrMode.AzureOnly => await PostProcessAsync(
                await _azure.ExtractAsync(new MemoryStream(bytes), fileName, cancellationToken),
                cancellationToken),
            _ => await ExtractHybridAsync(bytes, fileName, request, cancellationToken)
        };
    }

    private bool IsAzureConfigured() =>
        !string.IsNullOrWhiteSpace(_options.AzureEndpoint) && !string.IsNullOrWhiteSpace(_options.AzureApiKey);

    private async Task<IdentityOcrResult> ExtractHybridAsync(
        byte[] fileBytes,
        string fileName,
        ExtractIdentityOcrRequest request,
        CancellationToken cancellationToken)
    {
        var allowPolicyFallback = request.EnableFallback && _options.EnableFallback;
        var paddle = await _paddle.ExtractAsync(new MemoryStream(fileBytes), fileName, cancellationToken);

        if (!IsAzureConfigured())
            return await PostProcessAsync(paddle with { FallbackUsed = false }, cancellationToken);

        var threshold = Math.Clamp(request.ConfidenceThreshold, 1, 99);
        if (IsPaddleStrongEnough(paddle, threshold))
        {
            return await PostProcessAsync(
                paddle with { Provider = "PaddleOCR", FallbackUsed = false },
                cancellationToken);
        }

        try
        {
            var azure = await _azure.ExtractAsync(new MemoryStream(fileBytes), fileName, cancellationToken);
            var merged = MergeHybridPaddleFirst(paddle, azure);
            return await PostProcessAsync(merged with { FallbackUsed = false }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Azure Document Intelligence failed for {FileName}. Using Paddle OCR result.", fileName);
            if (!allowPolicyFallback)
                _logger.LogWarning("OCR fallback is disabled by policy; returning Paddle result anyway to avoid failing the request.");
            return await PostProcessAsync(paddle with { FallbackUsed = true }, cancellationToken);
        }
    }

    private static bool IsPaddleStrongEnough(IdentityOcrResult paddle, int threshold) =>
        paddle.Confidence >= threshold
        && !string.IsNullOrWhiteSpace(paddle.IdentityNumber)
        && !string.IsNullOrWhiteSpace(paddle.FullName);

    private async Task<IdentityOcrResult> PostProcessAsync(IdentityOcrResult r, CancellationToken cancellationToken)
    {
        var raw = r.RawText ?? string.Empty;
        var guessedName = SafeParseHeuristic(() => CnicRawTextIdentityParser.GuessLatinNameFromRaw(raw), "full name");
        var fullName = PickHolderOrFatherName(r.FullName, guessedName);
        var guessedFather = SafeParseHeuristic(() => CnicRawTextIdentityParser.GuessFatherNameFromRaw(raw), "father name");
        var father = PickHolderOrFatherName(r.FatherName, guessedFather);
        var nationality = string.IsNullOrWhiteSpace(r.Nationality)
            ? SafeParseHeuristic(() => CnicRawTextIdentityParser.GuessNationalityFromRaw(raw), "nationality")
            : r.Nationality;
        var dob = string.IsNullOrWhiteSpace(r.DateOfBirth)
            ? SafeParseHeuristic(() => CnicRawTextIdentityParser.GuessDateOfBirthFromRaw(raw), "date of birth")
            : r.DateOfBirth;
        var gender = string.IsNullOrWhiteSpace(r.Gender)
            ? SafeParseHeuristic(() => CnicRawTextIdentityParser.GuessGenderFromRaw(raw), "gender")
            : r.Gender;

        var addr = r.Address;
        var addressTranslated = false;
        var permUrdu = SafeParseHeuristic(() => CnicRawTextIdentityParser.ExtractPermanentUrduForTranslation(raw), "permanent address");
        if (!string.IsNullOrWhiteSpace(permUrdu))
        {
            var en = await _translator.TranslateUrduToEnglishAsync(permUrdu, cancellationToken);
            if (!string.IsNullOrWhiteSpace(en))
            {
                addr = en;
                addressTranslated = true;
            }
            else
                addr = SafeParseHeuristic(() => CnicRawTextIdentityParser.GuessAddressFromRaw(raw), "address") ?? addr;
        }
        else if (!string.IsNullOrWhiteSpace(addr) && CnicRawTextIdentityParser.ContainsArabicScript(addr))
        {
            var en2 = await _translator.TranslateUrduToEnglishAsync(addr, cancellationToken);
            if (!string.IsNullOrWhiteSpace(en2))
            {
                addr = en2;
                addressTranslated = true;
            }
            else
                addr = SafeParseHeuristic(() => CnicRawTextIdentityParser.GuessAddressFromRaw(raw), "address") ?? addr;
        }

        return r with
        {
            FullName = fullName,
            Address = addr,
            FatherName = father,
            Nationality = nationality,
            DateOfBirth = dob,
            Gender = gender,
            AddressTranslated = addressTranslated
        };
    }

    /// <summary>Heuristic regex parsing must never fail the OCR request.</summary>
    private string? SafeParseHeuristic(Func<string?> parse, string fieldLabel)
    {
        try
        {
            return parse();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CNIC heuristic parsing failed for {Field}; continuing with structured OCR fields.", fieldLabel);
            return null;
        }
    }

    /// <summary>Prefer raw-text heuristics when Azure maps a label or junk into a person field.</summary>
    private static string? PickHolderOrFatherName(string? structured, string? guessed)
    {
        var s = structured?.Trim();
        var g = guessed?.Trim();
        var gOk = GuessIsPlausiblePersonToken(g);
        var sOk = !string.IsNullOrEmpty(s) && GuessIsPlausiblePersonToken(s)
                  && !CnicRawTextIdentityParser.IsObviousNonPersonName(s);

        if (gOk && (!sOk || CnicRawTextIdentityParser.IsObviousNonPersonName(s!)))
            return g;
        return string.IsNullOrEmpty(s) ? g : s;
    }

    private static bool GuessIsPlausiblePersonToken(string? g) =>
        !string.IsNullOrEmpty(g) &&
        (CnicRawTextIdentityParser.IsLikelyLatinPersonName(g)
         || (CnicRawTextIdentityParser.ContainsArabicScript(g) && g.Length >= 4));

    /// <summary>
    /// Hybrid: Paddle first; Azure when Paddle is weak. When Paddle is filename stub, Azure-first merge for text fields.
    /// </summary>
    private static IdentityOcrResult MergeHybridPaddleFirst(IdentityOcrResult paddle, IdentityOcrResult azure)
    {
        var paddleStub = IsLikelyPaddleFilenameStub(paddle);
        return paddle with
        {
            Provider = $"{paddle.Provider} → {azure.Provider}",
            FullName = paddleStub ? Coalesce(azure.FullName, paddle.FullName) : Coalesce(paddle.FullName, azure.FullName),
            IdentityNumber = paddleStub ? Coalesce(azure.IdentityNumber, paddle.IdentityNumber) : Coalesce(paddle.IdentityNumber, azure.IdentityNumber),
            Address = paddleStub ? Coalesce(azure.Address, paddle.Address) : Coalesce(paddle.Address, azure.Address),
            DateOfBirth = paddleStub ? Coalesce(azure.DateOfBirth, paddle.DateOfBirth) : Coalesce(paddle.DateOfBirth, azure.DateOfBirth),
            Gender = paddleStub ? Coalesce(azure.Gender, paddle.Gender) : Coalesce(paddle.Gender, azure.Gender),
            FatherName = paddleStub ? Coalesce(azure.FatherName, paddle.FatherName) : Coalesce(paddle.FatherName, azure.FatherName),
            Nationality = paddleStub ? Coalesce(azure.Nationality, paddle.Nationality) : Coalesce(paddle.Nationality, azure.Nationality),
            RawText = MergeHybridRawText(paddle.RawText, azure.RawText, paddleStub),
            Confidence = Math.Max(paddle.Confidence, azure.Confidence)
        };
    }

    private static bool IsLikelyPaddleFilenameStub(IdentityOcrResult paddle)
    {
        var r = paddle.RawText?.Trim() ?? string.Empty;
        if (r.Length == 0) return true;
        if (r.Contains("Father", StringComparison.OrdinalIgnoreCase)) return false;
        if (r.Contains("Pakistan", StringComparison.OrdinalIgnoreCase)) return false;
        var newlines = r.Count(static c => c is '\n' or '\r');
        if (newlines >= 2) return false;
        return r.Length < 120 || (r.Length < 200 && newlines == 0);
    }

    private static string? MergeHybridRawText(string? paddleRaw, string? azureRaw, bool paddleStub)
    {
        if (paddleStub)
            return Coalesce(azureRaw, paddleRaw);

        var p = paddleRaw?.Trim() ?? string.Empty;
        var a = azureRaw?.Trim() ?? string.Empty;
        if (a.Length == 0) return paddleRaw;
        if (p.Length == 0) return azureRaw;
        if (string.Equals(p, a, StringComparison.Ordinal)) return paddleRaw;
        return CnicRawTextIdentityParser.NormalizeOcrLayoutForCnic(p + "\n\n" + a);
    }

    private static string? Coalesce(string? primary, string? secondary) =>
        !string.IsNullOrWhiteSpace(primary) ? primary : secondary;

    private static async Task<byte[]> ReadAllBytesAsync(Stream stream, CancellationToken cancellationToken)
    {
        if (stream.CanSeek) stream.Position = 0;
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, cancellationToken);
        return ms.ToArray();
    }
}
