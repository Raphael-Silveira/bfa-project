using BFA.Application.Acessos;
using BFA.Application.Franqueadora;
using BFA.Web.Authorization;
using BFA.Web.ViewModels.Franqueadora;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace BFA.Web.Areas.Franqueadora.Controllers;

[Area("Franqueadora")]
[Authorize(Policy = PoliticasAcesso.AdministradorRede)]
[Route("franqueadora")]
public sealed class InicioController(
    IUsuarioAtual usuarioAtual,
    IPainelFranqueadoraConsulta painelConsulta,
    IFranqueadoraDashboardConsulta dashboardConsulta,
    ILogger<InicioController> logger) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId)
        {
            return Forbid();
        }

        var resultado = await dashboardConsulta.ObterAsync(usuarioId, cancellationToken);

        if (resultado.Estado == EstadoFranqueadoraDashboard.SemAcesso)
        {
            return Forbid();
        }

        if (resultado.Estado == EstadoFranqueadoraDashboard.SelecaoOrganizacaoNecessaria)
        {
            return View(new FranqueadoraDashboardViewModel
            {
                SelecaoOrganizacaoNecessaria = true
            });
        }

        if (resultado.Resumo is not { } resumo)
        {
            return StatusCode(StatusCodes.Status500InternalServerError);
        }

        return View(FranqueadoraDashboardMapper.Mapear(resumo));
    }
}
