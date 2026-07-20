using System.Globalization;
using System.Text.RegularExpressions;
using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.DriverApp.DTOs;
using SheikhTravelSystem.Application.Features.FuelLogs.Commands;
using SheikhTravelSystem.Application.Features.FuelLogs.DTOs;
using SheikhTravelSystem.Application.Features.Ocr.DTOs;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.DriverApp.Commands;

public record DriverFuelReceiptUploadFile(Stream Content, string FileName, string ContentType, long Length);

public record DriverSubmitFuelReceiptFormCommand(
    int VehicleId,
    decimal Liters,
    decimal PricePerLiter,
    decimal OdometerReading,
    FuelType FuelType,
    DateTime? FuelDate,
    string? Station,
    DriverFuelReceiptUploadFile? Receipt)
    : IRequest<ApiResponse<int>>;

public class DriverSubmitFuelReceiptFormCommandValidator : AbstractValidator<DriverSubmitFuelReceiptFormCommand>
{
    public DriverSubmitFuelReceiptFormCommandValidator()
    {
        RuleFor(x => x.VehicleId).GreaterThan(0);
        RuleFor(x => x.Liters).GreaterThan(0);
        RuleFor(x => x.PricePerLiter).GreaterThan(0);
        RuleFor(x => x.OdometerReading).GreaterThan(0);
        RuleFor(x => x.FuelType).IsInEnum();
    }
}

public class DriverSubmitFuelReceiptFormCommandHandler(
    IMediator mediator,
    ICurrentUserService currentUser,
    ITenantContext tenantContext,
    IFileStorageService fileStorage)
    : IRequestHandler<DriverSubmitFuelReceiptFormCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(
        DriverSubmitFuelReceiptFormCommand request, CancellationToken cancellationToken)
    {
        var driverId = currentUser.DriverId;
        if (!driverId.HasValue)
            return ApiResponse<int>.FailResponse("Driver identity required.");

        string? receiptUrl = null;
        if (request.Receipt is { Length: > 0 })
        {
            if (request.Receipt.Length > 8 * 1024 * 1024)
                return ApiResponse<int>.FailResponse("Receipt image must be under 8 MB.");

            var tenantId = tenantContext.GetRequiredTenantId();
            var stored = await fileStorage.SaveAsync(
                request.Receipt.Content,
                string.IsNullOrWhiteSpace(request.Receipt.FileName) ? "receipt.jpg" : request.Receipt.FileName,
                string.IsNullOrWhiteSpace(request.Receipt.ContentType) ? "image/jpeg" : request.Receipt.ContentType,
                $"fuel-receipts/{tenantId}/{driverId.Value}",
                cancellationToken);
            receiptUrl = stored.StorageKey;
        }

        return await mediator.Send(new CreateFuelLogCommand(new CreateFuelLogDto(
            request.VehicleId,
            driverId.Value,
            request.Liters,
            request.PricePerLiter,
            request.OdometerReading,
            request.FuelType,
            request.FuelDate ?? DateTime.UtcNow,
            request.Station,
            receiptUrl)), cancellationToken);
    }
}

public record ScanFuelReceiptOcrCommand(
    DriverFuelReceiptUploadFile Receipt)
    : IRequest<ApiResponse<FuelReceiptOcrSuggestionDto>>;

public class ScanFuelReceiptOcrCommandHandler(IIdentityOcrService identityOcr)
    : IRequestHandler<ScanFuelReceiptOcrCommand, ApiResponse<FuelReceiptOcrSuggestionDto>>
{
    public async Task<ApiResponse<FuelReceiptOcrSuggestionDto>> Handle(
        ScanFuelReceiptOcrCommand request, CancellationToken cancellationToken)
    {
        if (request.Receipt.Length <= 0)
            return ApiResponse<FuelReceiptOcrSuggestionDto>.FailResponse("Receipt image is required.");
        if (request.Receipt.Length > 8 * 1024 * 1024)
            return ApiResponse<FuelReceiptOcrSuggestionDto>.FailResponse("Receipt image must be under 8 MB.");

        var ocr = await identityOcr.ExtractAsync(
            request.Receipt.Content,
            request.Receipt.FileName,
            new ExtractIdentityOcrRequest(OcrMode.PaddleOnly, IncludeRawText: true),
            cancellationToken);

        var suggestion = FuelReceiptOcrParser.Parse(ocr.RawText, ocr.Confidence);
        return ApiResponse<FuelReceiptOcrSuggestionDto>.SuccessResponse(suggestion);
    }
}

public static class FuelReceiptOcrParser
{
    private static readonly Regex DecimalRx = new(
        @"(\d{1,5}(?:[.,]\d{1,3})?)",
        RegexOptions.Compiled);

    public static FuelReceiptOcrSuggestionDto Parse(string? rawText, int confidence)
    {
        if (string.IsNullOrWhiteSpace(rawText))
            return new FuelReceiptOcrSuggestionDto(null, null, null, null, null, confidence, rawText);

        var text = rawText.Replace('\r', '\n');
        var lower = text.ToLowerInvariant();

        string? fuelType = null;
        if (lower.Contains("diesel") || lower.Contains("hsd"))
            fuelType = "Diesel";
        else if (lower.Contains("cng"))
            fuelType = "CNG";
        else if (lower.Contains("petrol") || lower.Contains("gasoline") || lower.Contains("super") || lower.Contains("pmg"))
            fuelType = "Petrol";

        decimal? liters = FindLabeled(text, ["liter", "litre", "liters", "litres", "qty", "volume", "ltr", "ltrs"]);
        decimal? price = FindLabeled(text, ["price/l", "price / l", "per liter", "per litre", "rate", "unit price", "rs/l", "pkr/l"]);
        decimal? total = FindLabeled(text, ["total", "amount", "grand total", "net amount", "payable"]);

        if (liters is null)
        {
            // Prefer values near "L" suffix: 42.5 L
            var m = Regex.Match(text, @"(\d{1,3}(?:[.,]\d{1,3})?)\s*[Ll]\b");
            if (m.Success && TryDec(m.Groups[1].Value, out var v) && v is >= 1 and <= 500)
                liters = v;
        }

        if (price is null && liters is > 0 && total is > 0)
            price = Math.Round(total.Value / liters.Value, 2);
        if (total is null && liters is > 0 && price is > 0)
            total = Math.Round(liters.Value * price.Value, 2);

        string? station = null;
        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (line.Length is < 3 or > 60) continue;
            if (DecimalRx.IsMatch(line) && line.Any(char.IsDigit) && line.Count(char.IsLetter) < 3) continue;
            if (line.Contains("receipt", StringComparison.OrdinalIgnoreCase)) continue;
            if (line.Contains("invoice", StringComparison.OrdinalIgnoreCase)) continue;
            station = line;
            break;
        }

        return new FuelReceiptOcrSuggestionDto(liters, price, total, station, fuelType, confidence, rawText);
    }

    private static decimal? FindLabeled(string text, IEnumerable<string> labels)
    {
        foreach (var label in labels)
        {
            var pattern = $@"(?i){Regex.Escape(label)}\s*[:#=\-]?\s*{DecimalRx}";
            var m = Regex.Match(text, pattern);
            if (m.Success && TryDec(m.Groups[1].Value, out var v))
                return v;
        }
        return null;
    }

    private static bool TryDec(string s, out decimal value)
    {
        var normalized = s.Replace(',', '.');
        return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }
}
