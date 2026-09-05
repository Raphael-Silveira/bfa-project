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
[Route("franqueadora/alunos")]
public sealed class AlunosController(
    IUsuarioAtual usuarioAtual,
    IFranqueadoraAlunosConsulta alunosConsulta,
    ILogger<AlunosController> logger) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(
        Guid? unidadeId,
        string? busca,
        int pagina = 1,
        CancellationToken cancellationToken = default)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId)
        {
            return Forbid();
        }

        logger.LogInformation(
            "Alunos da Rede iniciado por {UsuarioId}, unidade {UnidadeId}, busca {Busca}, pagina {Pagina}",
            usuarioId, unidadeId, busca, pagina);

        var resultado = await alunosConsulta.ListarAsync(
            usuarioId, unidadeId, busca, cancellationToken);

        if (resultado.Estado == EstadoFranqueadoraAlunos.SemAcesso)
        {
            return Forbid();
        }

        if (resultado.Estado == EstadoFranqueadoraAlunos.SelecaoOrganizacaoNecessaria)
        {
            return Forbid();
        }

        if (resultado.Resumo is not { } resumo)
        {
            return StatusCode(StatusCodes.Status500InternalServerError);
        }

        var viewModel = FranqueadoraAlunosMapper.Mapear(resumo, pagina, busca, unidadeId);

        return View(viewModel);
    }
}
