using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.API.Authorization;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Suppliers.Commands;
using SheikhTravelSystem.Application.Features.TravelNexus.Suppliers.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.Suppliers.Queries;

namespace SheikhTravelSystem.API.Controllers.TravelNexus;

/// <summary>Travel suppliers (airlines, hotels, bed banks, consolidators, transport) and the airport lookup.</summary>
[Authorize]
[RequireModule(TenantModuleCatalog.TravelAgencyModuleCode)]
[RequirePermission(TravelPermissions.View)]
[Route("api/travel")]
public class TravelSuppliersController : BaseApiController
{
    [RequirePermission(TravelPermissions.SupplierView)]
    [HttpGet("suppliers")]
    public async Task<IActionResult> GetSuppliers([FromQuery] string? supplierType, [FromQuery] bool? isActive)
        => Ok(await Mediator.Send(new GetTravelSuppliersQuery(supplierType, isActive)));

    [RequirePermission(TravelPermissions.SupplierManage)]
    [HttpPost("suppliers")]
    public async Task<IActionResult> Create([FromBody] SaveTravelSupplierDto dto) => Created(string.Empty, await Mediator.Send(new SaveTravelSupplierCommand(null, dto)));

    [RequirePermission(TravelPermissions.SupplierManage)]
    [HttpPut("suppliers/{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] SaveTravelSupplierDto dto) => Ok(await Mediator.Send(new SaveTravelSupplierCommand(id, dto)));

    [RequirePermission(TravelPermissions.SupplierManage)]
    [HttpDelete("suppliers/{id:int}")]
    public async Task<IActionResult> Delete(int id) => Ok(await Mediator.Send(new DeleteTravelSupplierCommand(id)));

    /// <summary>Airports (IATA reference data) by code, city or name.</summary>
    [HttpGet("airports")]
    public async Task<IActionResult> GetAirports([FromQuery] string? search) => Ok(await Mediator.Send(new GetTravelAirportsQuery(search)));
}
