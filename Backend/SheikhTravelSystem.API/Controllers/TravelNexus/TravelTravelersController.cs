using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.API.Authorization;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Travelers.Commands;
using SheikhTravelSystem.Application.Features.TravelNexus.Travelers.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.Travelers.Queries;

namespace SheikhTravelSystem.API.Controllers.TravelNexus;

/// <summary>Single-traveler operations. Create via <c>POST api/travel/customers/{customerId}/travelers</c>.</summary>
[Authorize]
[RequireModule(TenantModuleCatalog.TravelAgencyModuleCode)]
[RequirePermission(TravelPermissions.CustomerView)]
[Route("api/travel/travelers")]
public class TravelTravelersController : BaseApiController
{
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetTraveler(int id) => Ok(await Mediator.Send(new GetTravelTravelerByIdQuery(id)));

    /// <summary>PassportNumber/Cnic: null or masked value keeps the stored value; empty string clears it.</summary>
    [RequirePermission(TravelPermissions.CustomerManage)]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateTraveler(int id, [FromBody] SaveTravelTravelerDto dto)
        => Ok(await Mediator.Send(new UpdateTravelTravelerCommand(id, dto)));

    [RequirePermission(TravelPermissions.CustomerManage)]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteTraveler(int id) => Ok(await Mediator.Send(new DeleteTravelTravelerCommand(id)));
}
