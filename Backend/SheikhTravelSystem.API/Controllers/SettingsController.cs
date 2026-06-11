using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.API.Authorization;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.Settings;

namespace SheikhTravelSystem.API.Controllers;

[RequirePermission(PlatformPermissions.SettingsView)]
[ApiController]
[Route("api/settings")]
public class SettingsController : BaseApiController
{
    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories()
        => Ok(await Mediator.Send(new GetSettingsCategoriesQuery()));

    [HttpGet("{category}")]
    public async Task<IActionResult> GetByCategory(string category)
        => Ok(await Mediator.Send(new GetSettingsByCategoryQuery(category)));

    [RequirePermission(PlatformPermissions.SettingsManage)]
    [HttpPut("{category}")]
    public async Task<IActionResult> Update(string category, [FromBody] Dictionary<string, string?> values)
        => Ok(await Mediator.Send(new UpdateSettingsCommand(category, values)));
}
