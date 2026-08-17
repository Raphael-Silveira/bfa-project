using BFA.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;

namespace BFA.Web.Api.V1.Controllers;

[ApiController]
[Route("api/v1/health/database")]
public sealed class DatabaseHealthController(IDatabaseConnectionProbe databaseConnectionProbe)
    : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var canConnect = await databaseConnectionProbe.CanConnectAsync(cancellationToken);

        if (!canConnect)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { status = "unhealthy" });
        }

        return Ok(new { status = "healthy" });
    }
}
