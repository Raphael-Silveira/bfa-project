using BFA.Application.Acessos;
using BFA.Application.Unidades;
using BFA.Domain.Acessos;
using BFA.Web.Authorization;
using BFA.Web.ViewModels.Unidade;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BFA.Web.Areas.Unidade.Controllers;

[Area("Unidade")]
[Authorize]
[Route("unidade/{unidadeId:guid}")]
public sealed class InicioController(
    IUsuarioAtual usuarioAtual,
    IUnidadeContextoConsulta unidadeContextoConsulta,
    IUnidadesUsuarioConsulta unidadesUsuarioConsulta,
    IAuthorizationService authorizationService) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(
        Guid unidadeId,
        CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId)
        {
            return Forbid();
        }

        var unidade = await unidadeContextoConsulta.ObterAtivaAsync(
            unidadeId,
            cancellationToken);

        if (unidade is null)
        {
            return NotFound();
        }

        var autorizacao = await authorizationService.AuthorizeAsync(
            User,
            new ContextoUnidade(unidade.OrganizacaoId, unidade.UnidadeId),
            new AcessoUnidadePorPerfilRequirement(PerfilAcesso.AdministradorUnidade));

        if (!autorizacao.Succeeded)
        {
            return Forbid();
        }

        var unidadesAdministradas = await unidadesUsuarioConsulta.ListarAdministradasAsync(
            usuarioId,
            cancellationToken);

        return View(new PainelUnidadeViewModel
        {
            OrganizacaoId = unidade.OrganizacaoId,
            UnidadeId = unidade.UnidadeId,
            NomeUnidade = unidade.Nome,
            PodeTrocarUnidade = unidadesAdministradas.Count > 1
        });
    }
}
