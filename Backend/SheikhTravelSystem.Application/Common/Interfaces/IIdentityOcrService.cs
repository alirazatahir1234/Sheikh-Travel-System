using SheikhTravelSystem.Application.Features.Ocr.DTOs;

namespace SheikhTravelSystem.Application.Common.Interfaces;

public interface IIdentityOcrService
{
    Task<IdentityOcrResult> ExtractAsync(
        Stream fileStream,
        string fileName,
        ExtractIdentityOcrRequest request,
        CancellationToken cancellationToken = default);
}
