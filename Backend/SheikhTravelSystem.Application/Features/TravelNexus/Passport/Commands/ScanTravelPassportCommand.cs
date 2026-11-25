using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Ocr.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.Documents;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Passport.Commands;

public class TravelPassportScanResultDto
{
    /// <summary>False when OCR is not configured or failed — the user enters details manually.</summary>
    public bool Available { get; set; }
    /// <summary>True when a machine-readable zone was found and parsed.</summary>
    public bool Found { get; set; }
    public MrzPassportData? Passport { get; set; }
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Reads a passport photo with the existing OCR service and parses the MRZ to pre-fill a traveler.
/// Nothing is stored: the image is discarded and OCR text is never logged.
/// </summary>
public record ScanTravelPassportCommand(string FileName, byte[] Content) : IRequest<ApiResponse<TravelPassportScanResultDto>>;

public class ScanTravelPassportCommandValidator : AbstractValidator<ScanTravelPassportCommand>
{
    public ScanTravelPassportCommandValidator()
    {
        RuleFor(x => x.FileName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Content).NotNull().Must(c => c.Length is > 0 and <= TravelDocumentFileValidator.MaxBytes)
            .WithMessage("Upload an image up to 10 MB.");
    }
}

public class ScanTravelPassportCommandHandler(IIdentityOcrService ocr, ILogger<ScanTravelPassportCommandHandler> logger)
    : IRequestHandler<ScanTravelPassportCommand, ApiResponse<TravelPassportScanResultDto>>
{
    public async Task<ApiResponse<TravelPassportScanResultDto>> Handle(ScanTravelPassportCommand request, CancellationToken cancellationToken)
    {
        var type = TravelDocumentFileValidator.Sniff(request.Content);
        if (type is not ("image/jpeg" or "image/png"))
            return ApiResponse<TravelPassportScanResultDto>.FailResponse("Upload a JPG or PNG photo of the passport's photo page.");

        IdentityOcrResult result;
        try
        {
            using var stream = new MemoryStream(request.Content, writable: false);
            result = await ocr.ExtractAsync(stream, request.FileName,
                new ExtractIdentityOcrRequest(OcrMode.Hybrid, IncludeRawText: true), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Message only — never the OCR payload (PII).
            logger.LogWarning("Passport OCR unavailable: {ErrorType}", ex.GetType().Name);
            return Ok(new TravelPassportScanResultDto { Available = false, Message = "Passport reading is not available. Enter the details manually." });
        }

        var mrz = MrzParser.Parse(result.RawText);
        if (mrz is null)
            return Ok(new TravelPassportScanResultDto { Available = true, Message = "No machine-readable zone found. Use a sharper photo of the photo page or enter details manually." });

        return Ok(new TravelPassportScanResultDto
        {
            Available = true,
            Found = true,
            Passport = mrz,
            Message = mrz.ChecksumValid
                ? "Passport read. Check the details before saving."
                : "Passport read, but some check digits did not match. Check the highlighted fields carefully."
        });
    }

    private static ApiResponse<TravelPassportScanResultDto> Ok(TravelPassportScanResultDto dto)
        => ApiResponse<TravelPassportScanResultDto>.SuccessResponse(dto, dto.Message);
}
