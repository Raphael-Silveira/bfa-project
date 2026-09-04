using BFA.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace BFA.Web.Api.V1.Controllers;

[ApiController]
[Route("api/v1/health/database")]
public sealed class DatabaseHealthController(
    IDatabaseConnectionProbe databaseConnectionProbe,
    ILogger<DatabaseHealthController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var canConnect = await databaseConnectionProbe.CanConnectAsync(cancellationToken);

        if (!canConnect)
        {
            logger.LogWarning("DatabaseHealth check falhou: nao foi possivel conectar ao banco de dados");
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { status = "unhealthy" });
        }

        return Ok(new { status = "healthy" });
    }
}
