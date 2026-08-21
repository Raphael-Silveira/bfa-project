using BFA.Application.Localidades;
using BFA.Web.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BFA.Web.Areas.Franqueadora.Controllers;

[Area("Franqueadora")]
[Authorize(Policy = PoliticasAcesso.AdministradorRede)]
[Route("franqueadora/localidades")]
public sealed class LocalidadesController(ILocalidadesConsulta localidadesConsulta)
    : Controller
{
    [HttpGet("municipios")]
    public async Task<IActionResult> Municipios(
        int estadoCodigoIbge,
        CancellationToken cancellationToken)
    {
        if (estadoCodigoIbge <= 0)
        {
            return BadRequest();
        }

        try
        {
            var municipios = await localidadesConsulta.ListarMunicipiosAtivosAsync(
                estadoCodigoIbge,
                cancellationToken);

            return Ok(municipios.Select(municipio => new MunicipioLocalidadeResponse(
                municipio.CodigoIbge,
                municipio.Nome)));
        }
        catch (OperationCanceledException) when (
            cancellationToken.IsCancellationRequested
            && HttpContext.RequestAborted.IsCancellationRequested)
        {
            return new EmptyResult();
        }
    }

    private sealed record MunicipioLocalidadeResponse(int CodigoIbge, string Nome);
}
