using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace SheikhTravelSystem.API.Controllers;

[ApiController]
[Route("api/[controller]")]
/// <summary>
/// Base API controller exposing lazy MediatR access.
/// </summary>
public abstract class BaseApiController : ControllerBase
{
    private ISender? _mediator;
    protected ISender Mediator => _mediator ??= HttpContext.RequestServices.GetRequiredService<ISender>();
}
