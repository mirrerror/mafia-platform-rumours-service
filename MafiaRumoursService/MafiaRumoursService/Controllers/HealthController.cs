using Microsoft.AspNetCore.Mvc;

namespace MafiaRumoursService.Controllers;

[ApiController]
[Route("actuator")]
public class HealthController : ControllerBase
{
    [HttpGet("health")]
    public IActionResult GetHealth()
    {
        var response = new
        {
            status = "UP",
            timestamp = DateTime.UtcNow
        };
        return Ok(response);
    }
}