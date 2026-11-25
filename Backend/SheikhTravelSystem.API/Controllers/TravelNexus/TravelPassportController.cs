using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.API.Authorization;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Documents;
using SheikhTravelSystem.Application.Features.TravelNexus.Passport.Commands;

namespace SheikhTravelSystem.API.Controllers.TravelNexus;

/// <summary>Passport photo-page scan (OCR + MRZ) to pre-fill a traveler. Nothing is stored.</summary>
[Authorize]
[RequireModule(TenantModuleCatalog.TravelAgencyModuleCode)]
[RequirePermission(TravelPermissions.CustomerManage)]
[Route("api/travel/passport")]
public class TravelPassportController : BaseApiController
{
    private const int UploadLimit = TravelDocumentFileValidator.MaxBytes + 512 * 1024;

    [HttpPost("scan")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(UploadLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = UploadLimit)]
    public async Task<IActionResult> Scan(IFormFile? file, CancellationToken cancellationToken)
    {
        var bytes = await TravelUploadReader.ReadAsync(file, cancellationToken);
        if (bytes is null)
            return BadRequest(ApiResponse<TravelPassportScanResultDto>.FailResponse("Upload a JPG or PNG photo up to 10 MB."));
        return Ok(await Mediator.Send(new ScanTravelPassportCommand(file!.FileName, bytes), cancellationToken));
    }
}
