using BFA.Application.Acessos;
using BFA.Application.Franqueadora.AcessosUnidade;
using BFA.Web.Authorization;
using BFA.Web.ViewModels.Franqueadora;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace BFA.Web.Areas.Franqueadora.Controllers;

[Area("Franqueadora")]
[Authorize(Policy = PoliticasAcesso.AdministradorRede)]
[Route("franqueadora/unidades/{unidadeId:guid}/acessos")]
public sealed class AcessosUnidadeController(
    IUsuarioAtual usuarioAtual,
    IAcessosUnidadeConsulta consulta,
    IAcessosUnidadeServico servico,
    ILogger<AcessosUnidadeController> logger) : Controller
{
    private const string MensagemUsuarioNaoEncontrado =
        "Não encontramos um usuário cadastrado com este email.";
    private const string MensagemVinculoJaAtivo =
        "Este usuário já administra esta unidade.";

    [HttpGet("")]
    public async Task<IActionResult> Index(
        Guid unidadeId,
        CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId)
        {
            return Forbid();
        }

        return await ExibirAsync(
            usuarioId,
            unidadeId,
            email: string.Empty,
            cancellationToken);
    }

    [HttpPost("adicionar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Adicionar(
        Guid unidadeId,
        [Bind(nameof(AcessosUnidadeViewModel.Email))] AcessosUnidadeViewModel model,
        CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId)
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            return await ExibirAsync(
                usuarioId,
                unidadeId,
                model.Email,
                cancellationToken);
        }

        var resultado = await servico.AdicionarAsync(
            usuarioId,
            unidadeId,
            new AdicionarAdministradorUnidadeSolicitacao(model.Email),
            cancellationToken);

        if (resultado.Estado == EstadoGerenciamentoAcessoUnidade.UsuarioNaoEncontrado)
        {
            ModelState.AddModelError(nameof(model.Email), MensagemUsuarioNaoEncontrado);
            return await ExibirAsync(
                usuarioId,
                unidadeId,
                model.Email,
                cancellationToken);
        }

        if (resultado.Estado == EstadoGerenciamentoAcessoUnidade.VinculoJaAtivo)
        {
            ModelState.AddModelError(nameof(model.Email), MensagemVinculoJaAtivo);
            return await ExibirAsync(
                usuarioId,
                unidadeId,
                model.Email,
                cancellationToken);
        }

        return MapearOperacao(resultado, unidadeId, "Adicionar", usuarioId);
    }

    [HttpPost("{vinculoId:guid}/ativar")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Ativar(
        Guid unidadeId,
        Guid vinculoId,
        CancellationToken cancellationToken)
    {
        return AlterarEstadoAsync(
            unidadeId,
            vinculoId,
            ativar: true,
            cancellationToken);
    }

    [HttpPost("{vinculoId:guid}/desativar")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Desativar(
        Guid unidadeId,
        Guid vinculoId,
        CancellationToken cancellationToken)
    {
        return AlterarEstadoAsync(
            unidadeId,
            vinculoId,
            ativar: false,
            cancellationToken);
    }

    private async Task<IActionResult> AlterarEstadoAsync(
        Guid unidadeId,
        Guid vinculoId,
        bool ativar,
        CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId)
        {
            return Forbid();
        }

        var resultado = ativar
            ? await servico.AtivarAsync(
                usuarioId,
                unidadeId,
                vinculoId,
                cancellationToken)
            : await servico.DesativarAsync(
                usuarioId,
                unidadeId,
                vinculoId,
                cancellationToken);

        return MapearOperacao(resultado, unidadeId, ativar ? "Ativar" : "Desativar", usuarioId);
    }

    private async Task<IActionResult> ExibirAsync(
        Guid usuarioId,
        Guid unidadeId,
        string email,
        CancellationToken cancellationToken)
    {
        var resultado = await consulta.ObterAsync(
            usuarioId,
            unidadeId,
            cancellationToken);

        if (resultado.Estado == EstadoGerenciamentoAcessoUnidade.UnidadeNaoEncontrada)
        {
            return NotFound();
        }

        if (resultado.Estado != EstadoGerenciamentoAcessoUnidade.Sucesso
            || resultado.Valor is not { } acessos)
        {
            return Forbid();
        }

        return View("Index", new AcessosUnidadeViewModel
        {
            UnidadeId = acessos.Unidade.Id,
            UnidadeNome = acessos.Unidade.Nome,
            UnidadeAtiva = acessos.Unidade.Ativa,
            Email = email,
            Administradores = acessos.Administradores
                .Select(administrador => new AdministradorUnidadeItemViewModel(
                    acessos.Unidade.Id,
                    administrador.VinculoId,
                    administrador.UsuarioId,
                    administrador.Email,
                    administrador.Ativo,
                    administrador.CriadoEmUtc))
                .ToArray()
        });
    }

    private IActionResult MapearOperacao(
        ResultadoOperacaoAcessoUnidade resultado,
        Guid unidadeId,
        string acao,
        Guid usuarioId)
    {
        if (resultado.Estado is EstadoGerenciamentoAcessoUnidade.UnidadeNaoEncontrada
            or EstadoGerenciamentoAcessoUnidade.VinculoNaoEncontrado)
        {
            return NotFound();
        }

        if (resultado.Estado == EstadoGerenciamentoAcessoUnidade.Sucesso)
        {
            logger.LogInformation(
                "{Controller} {Action} concluído: {EntityId}",
                "AcessosUnidade", acao, unidadeId);
            return Redirect($"/franqueadora/unidades/{unidadeId}/acessos");
        }

        logger.LogWarning(
            "{Controller} {Action} negado para {UsuarioId}",
            "AcessosUnidade", acao, usuarioId);
        return Forbid();
    }
}
