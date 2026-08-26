using BFA.Application.Acessos;
using BFA.Application.Unidades;
using BFA.Application.Usuarios;
using BFA.Application.Professores.Turmas;
using BFA.Domain.Acessos;
using BFA.Web.Authorization;
using BFA.Web.ViewModels.Professor;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BFA.Web.Areas.Professor.Controllers;

[Area("Professor")]
[Authorize]
[Route("professor")]
public sealed class InicioController(
    IUsuarioAtual usuarioAtual,
    IUnidadesUsuarioConsulta unidadesUsuarioConsulta,
    IUsuarioApresentacaoConsulta usuarioApresentacaoConsulta,
    IMinhasTurmasProfessorConsulta minhasTurmasConsulta,
    IAuthorizationService authorizationService) : Controller
{
    [HttpGet("selecionar-unidade")]
    public async Task<IActionResult> SelecionarUnidade(CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId) return Forbid();
        var unidades = await unidadesUsuarioConsulta.ListarProfessorAsync(
            usuarioId, cancellationToken);
        if (unidades.Count == 1)
        {
            return Redirect($"/professor/unidade/{unidades[0].UnidadeId:D}");
        }

        var nome = await usuarioApresentacaoConsulta.ObterNomeCompletoAsync(
            usuarioId, cancellationToken);
        return View(new ProfessorSelecaoUnidadeViewModel(
            PrimeiroNome(nome),
            unidades.Select(item => new BFA.Web.ViewModels.Unidade.UnidadeSelecaoItemViewModel(
                item.UnidadeId, item.Nome)).ToArray()));
    }

    [HttpPost("selecionar-unidade")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SelecionarUnidade(
        Guid unidadeId,
        CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId) return Forbid();
        var unidade = await unidadesUsuarioConsulta.ObterProfessorAsync(
            usuarioId, unidadeId, cancellationToken);
        return unidade is null
            ? Forbid()
            : Redirect($"/professor/unidade/{unidadeId:D}");
    }

    [HttpGet("unidade/{unidadeId:guid}")]
    public async Task<IActionResult> Unidade(
        Guid unidadeId,
        CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId) return Forbid();
        var unidade = await unidadesUsuarioConsulta.ObterProfessorAsync(
            usuarioId, unidadeId, cancellationToken);
        if (unidade is null) return Forbid();
        var autorizacao = await authorizationService.AuthorizeAsync(
            User,
            new ContextoUnidade(unidade.OrganizacaoId, unidadeId),
            new AcessoUnidadePorPerfilRequirement(PerfilAcesso.Professor));
        if (!autorizacao.Succeeded) return Forbid();
        var todas = await unidadesUsuarioConsulta.ListarProfessorAsync(
            usuarioId, cancellationToken);
        var nome = await usuarioApresentacaoConsulta.ObterNomeCompletoAsync(
            usuarioId, cancellationToken);
        var turmas = await minhasTurmasConsulta.ContarAtivasAsync(
            usuarioId, unidadeId, cancellationToken);
        if (turmas.Estado is EstadoMinhasTurmasProfessor.SemAcesso
            or EstadoMinhasTurmasProfessor.VinculoProfissionalNaoEncontrado)
        {
            return Forbid();
        }
        return View(new ProfessorInicioViewModel(
            unidadeId, unidade.Nome, todas.Count > 1, PrimeiroNome(nome),
            turmas.Valor));
    }

    private static string? PrimeiroNome(string? nome) => string.IsNullOrWhiteSpace(nome)
        ? null
        : nome.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
}
