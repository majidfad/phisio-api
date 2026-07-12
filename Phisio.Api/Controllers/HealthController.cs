using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Phisio.Api.Controllers;

[ApiController]
[Route("api")]
public class HealthController : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("health")]
    public IActionResult Get()
    {
        return Ok(new { status = "healthy" });
    }
}
