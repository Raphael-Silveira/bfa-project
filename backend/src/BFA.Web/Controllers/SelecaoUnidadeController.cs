using BFA.Application.Acessos;
using BFA.Application.Unidades;
using BFA.Application.Usuarios;
using BFA.Web.ViewModels.Unidade;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace BFA.Web.Controllers;

[Authorize]
public sealed class SelecaoUnidadeController(
    IUsuarioAtual usuarioAtual,
    IUnidadesUsuarioConsulta unidadesUsuarioConsulta,
    IUsuarioApresentacaoConsulta usuarioApresentacaoConsulta,
    ILogger<SelecaoUnidadeController> logger) : Controller
{
    [HttpGet("selecionar-unidade")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId)
        {
            return Forbid();
        }

        var unidades = await unidadesUsuarioConsulta.ListarAdministradasAsync(
            usuarioId,
            cancellationToken);

        if (unidades.Count == 1)
        {
            return Redirect(ObterUrlUnidade(unidades[0].UnidadeId));
        }

        var nomeCompleto = await usuarioApresentacaoConsulta.ObterNomeCompletoAsync(
            usuarioId,
            cancellationToken);

        return View(new SelecaoUnidadeViewModel
        {
            PrimeiroNomeUsuario = ObterPrimeiroNome(nomeCompleto),
            Unidades = unidades
                .Select(unidade => new UnidadeSelecaoItemViewModel(
                    unidade.UnidadeId,
                    unidade.Nome))
                .ToArray()
        });
    }

    [HttpPost("selecionar-unidade")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Selecionar(
        Guid unidadeId,
        CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId || unidadeId == Guid.Empty)
        {
            logger.LogWarning("SelecaoUnidade {Action} negado: usuario ou unidade invalidos", "Selecionar");
            return Forbid();
        }

        var unidade = await unidadesUsuarioConsulta.ObterAdministradaAsync(
            usuarioId,
            unidadeId,
            cancellationToken);

        if (unidade is null)
        {
            logger.LogWarning("SelecaoUnidade {Action} negado: unidade {UnidadeId} nao autorizada", "Selecionar", unidadeId);
            return Forbid();
        }

        logger.LogInformation("SelecaoUnidade {Action} concluido: {UnidadeId}", "Selecionar", unidadeId);
        return Redirect(ObterUrlUnidade(unidade.UnidadeId));
    }

    private static string ObterUrlUnidade(Guid unidadeId)
    {
        return $"/unidade/{unidadeId:D}";
    }

    private static string? ObterPrimeiroNome(string? nomeCompleto)
    {
        if (string.IsNullOrWhiteSpace(nomeCompleto))
        {
            return null;
        }

        return nomeCompleto
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();
    }
}
