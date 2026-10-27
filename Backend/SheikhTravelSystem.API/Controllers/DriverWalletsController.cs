using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.API.Authorization;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.DriverWallets;
using SheikhTravelSystem.Application.Features.DriverWallets.DTOs;

namespace SheikhTravelSystem.API.Controllers;

[Authorize]
[RequirePermission(FinancePermissions.DriverWalletView)]
public class DriverWalletsController : BaseApiController
{
    [HttpGet("policy")]
    public async Task<IActionResult> GetPolicy()
        => Ok(await Mediator.Send(new GetDriverWalletPolicyQuery()));

    [HttpGet("preview")]
    public async Task<IActionResult> PreviewCashOut([FromQuery] decimal grossAmount)
        => Ok(await Mediator.Send(new PreviewDriverCashOutQuery(grossAmount)));

    [HttpGet]
    public async Task<IActionResult> GetWallets([FromQuery] GetDriverWalletsQuery query)
        => Ok(await Mediator.Send(query));

    [HttpGet("{driverId:int}")]
    public async Task<IActionResult> GetByDriver(int driverId)
        => Ok(await Mediator.Send(new GetDriverWalletByDriverIdQuery(driverId)));

    [HttpGet("ledger")]
    public async Task<IActionResult> GetLedger([FromQuery] GetDriverWalletLedgerQuery query)
        => Ok(await Mediator.Send(query));

    [HttpGet("cash-out-requests")]
    public async Task<IActionResult> GetCashOutRequests([FromQuery] GetDriverCashOutRequestsQuery query)
        => Ok(await Mediator.Send(query));

    [RequirePermission(FinancePermissions.DriverWalletManage)]
    [HttpPost("cash-out-requests/{id:int}/reject")]
    public async Task<IActionResult> RejectCashOut(int id, [FromBody] RejectCashOutBody body)
        => Ok(await Mediator.Send(new RejectDriverCashOutCommand(id, body.Reason)));

    [RequirePermission(FinancePermissions.DriverWalletManage)]
    [HttpPost("cash-out-requests/bulk-approve")]
    public async Task<IActionResult> BulkApprove([FromBody] BulkApproveDriverCashOutsCommand command)
        => Ok(await Mediator.Send(command));
}

public record RejectCashOutBody(string Reason);
