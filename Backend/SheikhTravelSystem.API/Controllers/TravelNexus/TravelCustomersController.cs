using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.API.Authorization;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Customers.Commands;
using SheikhTravelSystem.Application.Features.TravelNexus.Customers.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.Customers.Queries;
using SheikhTravelSystem.Application.Features.TravelNexus.Travelers.Commands;
using SheikhTravelSystem.Application.Features.TravelNexus.Travelers.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.Travelers.Queries;

namespace SheikhTravelSystem.API.Controllers.TravelNexus;

/// <summary>
/// Travel customers (individual or corporate) and their travelers. Ids are Nexus <c>CustomerId</c>s.
/// Branch-scoped users only see customers of their branches (or with no branch).
/// </summary>
[Authorize]
[RequireModule(TenantModuleCatalog.TravelAgencyModuleCode)]
[RequirePermission(TravelPermissions.CustomerView)]
[Route("api/travel/customers")]
public class TravelCustomersController : BaseApiController
{
    /// <summary>Paged list. Filters: search (name/phone/email/contact), customerKind, agentUserId, branchId.</summary>
    [HttpGet]
    public async Task<IActionResult> GetCustomers([FromQuery] GetTravelCustomersQuery query) => Ok(await Mediator.Send(query));

    [HttpGet("{customerId:int}")]
    public async Task<IActionResult> GetCustomer(int customerId)
        => Ok(await Mediator.Send(new GetTravelCustomerByIdQuery(customerId)));

    /// <summary>Creates a travel customer, or attaches a profile to an existing customer (ExistingCustomerId).</summary>
    [RequirePermission(TravelPermissions.CustomerManage)]
    [HttpPost]
    public async Task<IActionResult> CreateCustomer([FromBody] CreateTravelCustomerDto dto)
        => Created(string.Empty, await Mediator.Send(new CreateTravelCustomerCommand(dto)));

    [RequirePermission(TravelPermissions.CustomerManage)]
    [HttpPut("{customerId:int}")]
    public async Task<IActionResult> UpdateCustomer(int customerId, [FromBody] UpdateTravelCustomerDto dto)
        => Ok(await Mediator.Send(new UpdateTravelCustomerCommand(customerId, dto)));

    /// <summary>Archives the travel profile; the Nexus customer record is kept.</summary>
    [RequirePermission(TravelPermissions.CustomerManage)]
    [HttpDelete("{customerId:int}")]
    public async Task<IActionResult> DeleteCustomer(int customerId)
        => Ok(await Mediator.Send(new DeleteTravelCustomerCommand(customerId)));

    /// <summary>Travelers of the customer. Passport/CNIC masked without Travel.Traveler.ViewPII.</summary>
    [HttpGet("{customerId:int}/travelers")]
    public async Task<IActionResult> GetTravelers(int customerId)
        => Ok(await Mediator.Send(new GetTravelTravelersByCustomerQuery(customerId)));

    [RequirePermission(TravelPermissions.CustomerManage)]
    [HttpPost("{customerId:int}/travelers")]
    public async Task<IActionResult> CreateTraveler(int customerId, [FromBody] SaveTravelTravelerDto dto)
        => Created(string.Empty, await Mediator.Send(new CreateTravelTravelerCommand(customerId, dto)));
}
