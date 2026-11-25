using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.API.Authorization;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Documents;
using SheikhTravelSystem.Application.Features.TravelNexus.Documents.Commands;
using SheikhTravelSystem.Application.Features.TravelNexus.Documents.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.Documents.Queries;

namespace SheikhTravelSystem.API.Controllers.TravelNexus;

/// <summary>
/// Applicant documents of visa cases. Files are stored privately and only ever streamed by
/// <c>GET files/{fileId}</c> (Travel.Document.Download, audited fail-closed) — no URLs are returned.
/// </summary>
[Authorize]
[RequireModule(TenantModuleCatalog.TravelAgencyModuleCode)]
[RequirePermission(TravelPermissions.VisaView)]
[Route("api/travel/documents")]
public class TravelDocumentsController : BaseApiController
{
    private const int UploadLimit = TravelDocumentFileValidator.MaxBytes + 512 * 1024; // file + multipart overhead

    /// <summary>Requirements of a case grouped by applicant, with file metadata and checklist progress.</summary>
    [HttpGet("visa-cases/{caseId:int}")]
    public async Task<IActionResult> GetCaseDocuments(int caseId) => Ok(await Mediator.Send(new GetTravelCaseDocumentsQuery(caseId)));

    /// <summary>Open cases with outstanding mandatory documents.</summary>
    [HttpGet("missing")]
    public async Task<IActionResult> GetMissing([FromQuery] GetTravelMissingDocumentsQuery query) => Ok(await Mediator.Send(query));

    /// <summary>Upload a PDF/JPG/PNG (max 10 MB) for a requirement. Content is checked by its bytes, not its name.</summary>
    [RequirePermission(TravelPermissions.DocumentManage)]
    [HttpPost("requirements/{requirementId:int}/files")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(UploadLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = UploadLimit)]
    public async Task<IActionResult> Upload(int requirementId, IFormFile? file, CancellationToken cancellationToken)
    {
        var bytes = await TravelUploadReader.ReadAsync(file, cancellationToken);
        if (bytes is null)
            return BadRequest(ApiResponse<int>.FailResponse("Choose a PDF, JPG or PNG file up to 10 MB."));
        return Ok(await Mediator.Send(new UploadTravelDocumentCommand(requirementId, file!.FileName, bytes), cancellationToken));
    }

    /// <summary>Ask one applicant for an additional document.</summary>
    [RequirePermission(TravelPermissions.DocumentManage)]
    [HttpPost("visa-cases/{caseId:int}/requests")]
    public async Task<IActionResult> Request(int caseId, [FromBody] RequestTravelDocumentDto dto)
        => Ok(await Mediator.Send(new RequestTravelDocumentCommand(caseId, dto)));

    /// <summary>Remove an ad-hoc requirement (checklist requirements can only be waived).</summary>
    [RequirePermission(TravelPermissions.DocumentManage)]
    [HttpDelete("requirements/{requirementId:int}")]
    public async Task<IActionResult> RemoveRequirement(int requirementId)
        => Ok(await Mediator.Send(new RemoveTravelDocumentRequirementCommand(requirementId)));

    [RequirePermission(TravelPermissions.DocumentVerify)]
    [HttpPost("requirements/{requirementId:int}/verify")]
    public async Task<IActionResult> Verify(int requirementId, [FromBody] VerifyTravelDocumentDto dto)
        => Ok(await Mediator.Send(new VerifyTravelDocumentCommand(requirementId, dto)));

    [RequirePermission(TravelPermissions.DocumentVerify)]
    [HttpPost("requirements/{requirementId:int}/reject")]
    public async Task<IActionResult> Reject(int requirementId, [FromBody] ReviewReasonDto dto)
        => Ok(await Mediator.Send(new RejectTravelDocumentCommand(requirementId, dto)));

    [RequirePermission(TravelPermissions.DocumentVerify)]
    [HttpPost("requirements/{requirementId:int}/waive")]
    public async Task<IActionResult> Waive(int requirementId, [FromBody] ReviewReasonDto dto)
        => Ok(await Mediator.Send(new WaiveTravelDocumentCommand(requirementId, dto)));

    /// <summary>Streams a document file inline. Not cacheable; every access is audited.</summary>
    [RequirePermission(TravelPermissions.DocumentDownload)]
    [HttpGet("files/{fileId:int}")]
    public async Task<IActionResult> Download(int fileId, CancellationToken cancellationToken)
    {
        var file = await Mediator.Send(new DownloadTravelDocumentFileQuery(fileId), cancellationToken);
        Response.Headers.CacheControl = "no-store, private";
        Response.Headers.Pragma = "no-cache";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers.ContentDisposition = new System.Net.Mime.ContentDisposition
        {
            Inline = true,
            FileName = file.FileName
        }.ToString();
        return File(file.Content, file.ContentType);
    }
}
