using BFA.Application.Acessos;
using BFA.Application.Franqueadora;
using BFA.Web.Authorization;
using BFA.Web.ViewModels.Franqueadora;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BFA.Web.Areas.Franqueadora.Controllers;

[Area("Franqueadora")]
[Authorize(Policy = PoliticasAcesso.AdministradorRede)]
[Route("franqueadora")]
public sealed class InicioController(
    IUsuarioAtual usuarioAtual,
    IPainelFranqueadoraConsulta painelConsulta) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId)
        {
            return Forbid();
        }

        var resultado = await painelConsulta.ObterAsync(usuarioId, cancellationToken);

        if (resultado.Estado == EstadoPainelFranqueadora.SemAcesso)
        {
            return Forbid();
        }

        if (resultado.Estado == EstadoPainelFranqueadora.SelecaoOrganizacaoNecessaria)
        {
            return View(new PainelFranqueadoraViewModel
            {
                SelecaoOrganizacaoNecessaria = true
            });
        }

        if (resultado.Resumo is not { } resumo)
        {
            return StatusCode(StatusCodes.Status500InternalServerError);
        }

        return View(new PainelFranqueadoraViewModel
        {
            NomeOrganizacao = resumo.NomeOrganizacao,
            TotalUnidades = resumo.TotalUnidades,
            UnidadesAtivas = resumo.UnidadesAtivas,
            AdministradoresRedeAtivos = resumo.AdministradoresRedeAtivos,
            AdministradoresUnidadeAtivos = resumo.AdministradoresUnidadeAtivos
        });
    }
}
