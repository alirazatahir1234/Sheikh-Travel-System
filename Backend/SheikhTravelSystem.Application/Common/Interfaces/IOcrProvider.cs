using SheikhTravelSystem.Application.Features.Ocr.DTOs;

namespace SheikhTravelSystem.Application.Common.Interfaces;

public interface IOcrProvider
{
    string ProviderName { get; }
    Task<IdentityOcrResult> ExtractAsync(Stream fileStream, string fileName, CancellationToken cancellationToken = default);
}
