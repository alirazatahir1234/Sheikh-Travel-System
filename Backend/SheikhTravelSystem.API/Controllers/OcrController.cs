using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.API.Authorization;
using SheikhTravelSystem.API.Models;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Ocr.DTOs;

namespace SheikhTravelSystem.API.Controllers;

[Authorize]
[RequirePermission(DriverPermissions.DriverCreate)]
public class OcrController(
    IIdentityOcrService ocrService,
    ILogger<OcrController> logger) : BaseApiController
{
    [HttpPost("extract-identity")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(15 * 1024 * 1024)]
    public async Task<IActionResult> ExtractIdentity(
        [FromForm] ExtractIdentityFormRequest form,
        CancellationToken cancellationToken)
    {
        var file = form.File;
        if (file is null || file.Length == 0)
            return BadRequest("File is required.");

        var ext = Path.GetExtension(file.FileName ?? string.Empty).ToLowerInvariant();
        var ct = (file.ContentType ?? string.Empty).ToLowerInvariant();
        if (ct.Contains("pdf", StringComparison.OrdinalIgnoreCase) || ext == ".pdf")
            return BadRequest("CNIC OCR accepts photos only (JPEG, PNG, or WebP). Export or screenshot the card as an image and upload again.");

        ExtractIdentityOcrRequest parsedRequest = new();
        if (!string.IsNullOrWhiteSpace(form.Request))
        {
            try
            {
                parsedRequest = JsonSerializer.Deserialize<ExtractIdentityOcrRequest>(
                    form.Request,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        Converters = { new JsonStringEnumConverter() }
                    }) ?? new ExtractIdentityOcrRequest();
            }
            catch (JsonException)
            {
                parsedRequest = new ExtractIdentityOcrRequest();
            }
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var result = await ocrService.ExtractAsync(stream, file.FileName ?? "upload.jpg", parsedRequest, cancellationToken);
            var api = IdentityOcrApiMapper.ToApiResponse(
                result,
                parsedRequest.Mode,
                parsedRequest.IncludeRawText,
                parsedRequest.ConfidenceThreshold);
            return Ok(api);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Azure OCR", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning(ex, "OCR extract-identity: Azure is not configured.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "CNIC OCR is temporarily unavailable (Azure not configured).");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "OCR extract-identity failed for {FileName}.", file.FileName);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "CNIC OCR failed. Try a clearer photo or enter details manually.");
        }
    }
}
