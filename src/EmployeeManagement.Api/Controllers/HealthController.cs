using EmployeeManagement.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;

namespace EmployeeManagement.Api.Controllers;

[ApiController]
[Route("health")]
public class HealthController(ILogger<HealthController> logger) : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new { status = "ok" });

    [HttpGet("db")]
    public async Task<IActionResult> GetDb([FromServices] AppDbContext db, CancellationToken ct)
    {
        try
        {
            if (await db.Database.CanConnectAsync(ct))
            {
                return Ok(new { status = "ok" });
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Database health check failed");
        }

        return StatusCode(StatusCodes.Status503ServiceUnavailable, new { status = "unavailable" });
    }
}