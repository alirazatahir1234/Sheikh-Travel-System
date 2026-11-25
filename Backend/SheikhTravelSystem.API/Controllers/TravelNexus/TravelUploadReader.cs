using Microsoft.AspNetCore.Http;
using SheikhTravelSystem.Application.Features.TravelNexus.Documents;

namespace SheikhTravelSystem.API.Controllers.TravelNexus;

/// <summary>Reads an uploaded form file into memory with a hard size cap (never trusts the declared length).</summary>
internal static class TravelUploadReader
{
    public static async Task<byte[]?> ReadAsync(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0 || file.Length > TravelDocumentFileValidator.MaxBytes) return null;
        await using var input = file.OpenReadStream();
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await input.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + read > TravelDocumentFileValidator.MaxBytes) return null;
            buffer.Write(chunk, 0, read);
        }
        return buffer.ToArray();
    }
}
