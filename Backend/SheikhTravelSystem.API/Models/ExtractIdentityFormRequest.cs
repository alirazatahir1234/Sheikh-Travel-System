namespace SheikhTravelSystem.API.Models;

/// <summary>Multipart body for POST /api/ocr/extract-identity (fields: file, request).</summary>
public sealed class ExtractIdentityFormRequest
{
    public IFormFile? File { get; set; }

    /// <summary>JSON-serialized <see cref="Application.Features.Ocr.DTOs.ExtractIdentityOcrRequest"/>.</summary>
    public string? Request { get; set; }
}
