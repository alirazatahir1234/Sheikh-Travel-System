using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Ocr.DTOs;

namespace SheikhTravelSystem.API.Controllers;

[Authorize]
public class OcrController : BaseApiController
{
    private readonly IIdentityOcrService _ocrService;

    public OcrController(IIdentityOcrService ocrService)
    {
        _ocrService = ocrService;
    }

    [HttpPost("extract-identity")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(15 * 1024 * 1024)]
    public async Task<IActionResult> ExtractIdentity(
        [FromForm] IFormFile file,
        [FromForm] string? request,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest("File is required.");

        var ext = Path.GetExtension(file.FileName ?? string.Empty).ToLowerInvariant();
        var ct = (file.ContentType ?? string.Empty).ToLowerInvariant();
        if (ct.Contains("pdf", StringComparison.OrdinalIgnoreCase) || ext == ".pdf")
            return BadRequest("CNIC OCR accepts photos only (JPEG, PNG, or WebP). Export or screenshot the card as an image and upload again.");

        ExtractIdentityOcrRequest parsedRequest = new();
        if (!string.IsNullOrWhiteSpace(request))
        {
            try
            {
                parsedRequest = JsonSerializer.Deserialize<ExtractIdentityOcrRequest>(
                    request,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        Converters = { new JsonStringEnumConverter() }
                    }) ?? new ExtractIdentityOcrRequest();
            }
            catch (JsonException)
            {
                // Keep OCR resilient: if optional settings are malformed,
                // continue with defaults rather than failing file extraction.
                parsedRequest = new ExtractIdentityOcrRequest();
            }
        }

        await using var stream = file.OpenReadStream();
        var result = await _ocrService.ExtractAsync(stream, file.FileName, parsedRequest, cancellationToken);
        var api = IdentityOcrApiMapper.ToApiResponse(
            result,
            parsedRequest.Mode,
            parsedRequest.IncludeRawText,
            parsedRequest.ConfidenceThreshold);
        return Ok(api);
    }
}
