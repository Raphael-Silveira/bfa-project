using BFA.Application.Acessos;
using BFA.Application.Professores.Turmas;
using BFA.Application.Professores.Confirmacoes;
using BFA.Application.Unidades;
using BFA.Domain.Acessos;
using BFA.Web.Authorization;
using BFA.Web.ViewModels.Professor;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BFA.Web.Areas.Professor.Controllers;

[Area("Professor")]
[Authorize]
[Route("professor/unidade/{unidadeId:guid}/turmas")]
public sealed class TurmasController(
    IUsuarioAtual usuarioAtual,
    IUnidadesUsuarioConsulta unidadesUsuarioConsulta,
    IMinhasTurmasProfessorConsulta consulta,
    IConfirmacoesParticipacaoProfessorConsulta confirmacoesConsulta,
    IAuthorizationService authorizationService) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(
        Guid unidadeId,
        CancellationToken cancellationToken)
    {
        var acesso = await ValidarAcessoAsync(unidadeId, cancellationToken);
        if (acesso.Resultado is not null) return acesso.Resultado;
        var resultado = await consulta.ListarAsync(
            usuarioAtual.UsuarioId!.Value, unidadeId, cancellationToken);
        if (resultado.Estado is EstadoMinhasTurmasProfessor.SemAcesso
            or EstadoMinhasTurmasProfessor.VinculoProfissionalNaoEncontrado)
        {
            return Forbid();
        }

        return View(new MinhasTurmasProfessorViewModel(
            unidadeId, acesso.Unidade!.Nome, acesso.PodeTrocar,
            resultado.Valor ?? []));
    }

    [HttpGet("{turmaId:guid}")]
    public async Task<IActionResult> Detalhe(
        Guid unidadeId,
        Guid turmaId,
        CancellationToken cancellationToken)
    {
        var acesso = await ValidarAcessoAsync(unidadeId, cancellationToken);
        if (acesso.Resultado is not null) return acesso.Resultado;
        var resultado = await consulta.ObterDetalheAsync(
            usuarioAtual.UsuarioId!.Value, unidadeId, turmaId, cancellationToken);
        if (resultado.Estado is EstadoMinhasTurmasProfessor.SemAcesso
            or EstadoMinhasTurmasProfessor.VinculoProfissionalNaoEncontrado)
        {
            return Forbid();
        }
        if (resultado.Estado == EstadoMinhasTurmasProfessor.TurmaNaoEncontrada
            || resultado.Valor is null)
        {
            return NotFound();
        }

        return View(new TurmaProfessorDetalheViewModel(
            unidadeId, acesso.Unidade!.Nome, acesso.PodeTrocar, resultado.Valor));
    }

    [HttpGet("{turmaId:guid}/aulas/{aulaId:guid}/confirmacoes")]
    public async Task<IActionResult> Confirmacoes(Guid unidadeId, Guid turmaId, Guid aulaId,
        CancellationToken cancellationToken)
    {
        var acesso = await ValidarAcessoAsync(unidadeId, cancellationToken);
        if (acesso.Resultado is not null) return acesso.Resultado;
        var resultado = await confirmacoesConsulta.ObterAsync(
            usuarioAtual.UsuarioId!.Value, unidadeId, turmaId, aulaId, cancellationToken);
        if (resultado.Estado == EstadoConfirmacoesProfessor.SemAcesso) return Forbid();
        return resultado.Valor is null ? NotFound() : View(new ProfessorConfirmacoesViewModel(
            unidadeId, turmaId, acesso.Unidade!.Nome, acesso.PodeTrocar, resultado.Valor));
    }

    [HttpPost("{turmaId:guid}/aulas/{aulaId:guid}/confirmacoes/chamada")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SalvarChamada(Guid unidadeId, Guid turmaId, Guid aulaId,
        ProfessorChamadaFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return RedirectToAction(nameof(Confirmacoes), new { unidadeId, turmaId, aulaId });

        var registros = model.Registros
            .Where(item => item.Status is not null)
            .Select(item => new RegistroChamadaProfessor(item.AlunoId, item.Status!.Value))
            .ToArray();
        var resultado = await confirmacoesConsulta.RegistrarChamadaAsync(
            usuarioAtual.UsuarioId!.Value, unidadeId, turmaId, aulaId, registros,
            cancellationToken);
        if (resultado.Estado == EstadoConfirmacoesProfessor.SemAcesso) return Forbid();
        if (resultado.Estado == EstadoConfirmacoesProfessor.AulaNaoEncontrada) return NotFound();
        if (resultado.Estado != EstadoConfirmacoesProfessor.Sucesso)
            return RedirectToAction(nameof(Confirmacoes), new { unidadeId, turmaId, aulaId });
        return RedirectToAction(nameof(Confirmacoes), new { unidadeId, turmaId, aulaId });
    }

    [HttpPost("{turmaId:guid}/aulas/{aulaId:guid}/confirmacoes/cancelar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelarAula(Guid unidadeId, Guid turmaId, Guid aulaId,
        ProfessorChamadaFormViewModel model, CancellationToken cancellationToken)
    {
        var resultado = await confirmacoesConsulta.CancelarAsync(
            usuarioAtual.UsuarioId!.Value, unidadeId, turmaId, aulaId,
            model.MotivoCancelamento, cancellationToken);
        if (resultado.Estado == EstadoConfirmacoesProfessor.SemAcesso) return Forbid();
        if (resultado.Estado == EstadoConfirmacoesProfessor.AulaNaoEncontrada) return NotFound();
        return RedirectToAction(nameof(Confirmacoes), new { unidadeId, turmaId, aulaId });
    }

    private async Task<(UnidadeAcessoResumo? Unidade, bool PodeTrocar,
        IActionResult? Resultado)> ValidarAcessoAsync(
        Guid unidadeId,
        CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId)
            return (null, false, Forbid());
        var unidade = await unidadesUsuarioConsulta.ObterProfessorAsync(
            usuarioId, unidadeId, cancellationToken);
        if (unidade is null) return (null, false, Forbid());
        var autorizacao = await authorizationService.AuthorizeAsync(
            User,
            new ContextoUnidade(unidade.OrganizacaoId, unidadeId),
            new AcessoUnidadePorPerfilRequirement(PerfilAcesso.Professor));
        if (!autorizacao.Succeeded) return (null, false, Forbid());
        var unidades = await unidadesUsuarioConsulta.ListarProfessorAsync(
            usuarioId, cancellationToken);
        return (unidade, unidades.Count > 1, null);
    }

}
