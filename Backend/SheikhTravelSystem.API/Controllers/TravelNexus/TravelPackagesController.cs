using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.API.Authorization;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Hotels.Commands;
using SheikhTravelSystem.Application.Features.TravelNexus.Hotels.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.Hotels.Queries;
using SheikhTravelSystem.Application.Features.TravelNexus.Packages.Commands;
using SheikhTravelSystem.Application.Features.TravelNexus.Packages.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.Packages.Queries;

namespace SheikhTravelSystem.API.Controllers.TravelNexus;

/// <summary>Umrah/Hajj packages and the hotel catalog.</summary>
[Authorize]
[RequireModule(TenantModuleCatalog.TravelAgencyModuleCode)]
[RequirePermission(TravelPermissions.PackageView)]
[Route("api/travel")]
public class TravelPackagesController : BaseApiController
{
    // ── Hotels ──
    [HttpGet("hotels")]
    public async Task<IActionResult> GetHotels([FromQuery] string? city, [FromQuery] bool? isActive)
        => Ok(await Mediator.Send(new GetTravelHotelsQuery(city, isActive)));

    [RequirePermission(TravelPermissions.PackageManage)]
    [HttpPost("hotels")]
    public async Task<IActionResult> CreateHotel([FromBody] SaveTravelHotelDto dto) => Created(string.Empty, await Mediator.Send(new SaveTravelHotelCommand(null, dto)));

    [RequirePermission(TravelPermissions.PackageManage)]
    [HttpPut("hotels/{id:int}")]
    public async Task<IActionResult> UpdateHotel(int id, [FromBody] SaveTravelHotelDto dto) => Ok(await Mediator.Send(new SaveTravelHotelCommand(id, dto)));

    [RequirePermission(TravelPermissions.PackageManage)]
    [HttpDelete("hotels/{id:int}")]
    public async Task<IActionResult> DeleteHotel(int id) => Ok(await Mediator.Send(new DeleteTravelHotelCommand(id)));

    // ── Packages ──
    [HttpGet("packages")]
    public async Task<IActionResult> GetPackages([FromQuery] string? status, [FromQuery] string? packageType)
        => Ok(await Mediator.Send(new GetTravelPackagesQuery(status, packageType)));

    /// <summary>Package with default hotel stays and prices per room type.</summary>
    [HttpGet("packages/{id:int}")]
    public async Task<IActionResult> GetPackage(int id) => Ok(await Mediator.Send(new GetTravelPackageByIdQuery(id)));

    [RequirePermission(TravelPermissions.PackageManage)]
    [HttpPost("packages")]
    public async Task<IActionResult> CreatePackage([FromBody] SaveTravelPackageDto dto) => Created(string.Empty, await Mediator.Send(new SaveTravelPackageCommand(null, dto)));

    [RequirePermission(TravelPermissions.PackageManage)]
    [HttpPut("packages/{id:int}")]
    public async Task<IActionResult> UpdatePackage(int id, [FromBody] SaveTravelPackageDto dto) => Ok(await Mediator.Send(new SaveTravelPackageCommand(id, dto)));

    /// <summary>Publish | Archive.</summary>
    [RequirePermission(TravelPermissions.PackageManage)]
    [HttpPost("packages/{id:int}/{action:regex(^(publish|archive)$)}")]
    public async Task<IActionResult> ChangeStatus(int id, string action)
        => Ok(await Mediator.Send(new ChangeTravelPackageStatusCommand(id, action == "publish" ? "Publish" : "Archive")));

    [RequirePermission(TravelPermissions.PackageManage)]
    [HttpDelete("packages/{id:int}")]
    public async Task<IActionResult> DeletePackage(int id) => Ok(await Mediator.Send(new DeleteTravelPackageCommand(id)));
}
