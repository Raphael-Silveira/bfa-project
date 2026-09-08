using BFA.Application.Acessos;
using BFA.Application.Aulas;
using BFA.Application.Cobrancas;
using BFA.Application.Relatorios;
using BFA.Application.Unidades;
using BFA.Domain.Aulas;
using BFA.Domain.Cobrancas;
using BFA.Web.Authorization;
using BFA.Web.ViewModels.Unidade;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace BFA.Web.Areas.Unidade.Controllers;

[Area("Unidade")]
[Authorize]
[ServiceFilter(typeof(GovernancaOperacionalUnidadeResultFilter))]
[Route("unidade/{unidadeId:guid}/relatorios")]
public sealed class RelatoriosController(
    IUsuarioAtual usuarioAtual,
    IRelatoriosServico relatoriosServico,
    IAulasServico aulasServico,
    ICobrancasServico cobrancasServico,
    IUnidadesUsuarioConsulta unidadesUsuarioConsulta,
    ILogger<RelatoriosController> logger) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(
        Guid unidadeId,
        CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId) return Forbid();

        var contexto = await ObterContextoAsync(usuarioId, unidadeId, cancellationToken);
        if (contexto is null) return Forbid();

        var (estado, resumo) = await relatoriosServico.ObterResumoGeralAsync(usuarioId, unidadeId);

        if (estado == EstadoRelatorios.UnidadeNaoEncontrada)
            return NotFound();
        if (estado != EstadoRelatorios.Sucesso || resumo is null)
            return Forbid();

        return View(RelatorioViewModelMapper.MapearIndex(contexto, resumo));
    }

    [HttpGet("financeiro")]
    public async Task<IActionResult> Financeiro(
        Guid unidadeId,
        DateOnly? dataInicio,
        DateOnly? dataFim,
        CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId) return Forbid();

        var contexto = await ObterContextoAsync(usuarioId, unidadeId, cancellationToken);
        if (contexto is null) return Forbid();

        var filtro = new FiltroRelatorio(dataInicio, dataFim);
        var (estado, relatorio) = await relatoriosServico.ObterFinanceiroDetalhadoAsync(
            usuarioId, unidadeId, filtro);

        if (estado == EstadoRelatorios.UnidadeNaoEncontrada)
            return NotFound();
        if (estado != EstadoRelatorios.Sucesso || relatorio is null)
            return Forbid();

        return View(RelatorioViewModelMapper.MapearFinanceiro(contexto, relatorio, dataInicio, dataFim));
    }

    [HttpGet("resumo-financeiro")]
    public async Task<IActionResult> ResumoFinanceiro(
        Guid unidadeId,
        CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId) return Forbid();

        var contexto = await ObterContextoAsync(usuarioId, unidadeId, cancellationToken);
        if (contexto is null) return Forbid();

        var (estado, resumo) = await cobrancasServico.ObterResumoFinanceiroAsync(usuarioId, unidadeId);

        if (estado == EstadoCobrancas.UnidadeNaoEncontrada)
            return NotFound();
        if (estado != EstadoCobrancas.Sucesso || resumo is null)
            return Forbid();

        return View(CobrancaViewModelMapper.MapearResumoFinanceiro(contexto, resumo));
    }

    [HttpGet("frequencia")]
    public async Task<IActionResult> Frequencia(
        Guid unidadeId,
        Guid? turmaId,
        DateOnly? dataInicio,
        DateOnly? dataFim,
        CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId) return Forbid();

        var contexto = await ObterContextoAsync(usuarioId, unidadeId, cancellationToken);
        if (contexto is null) return Forbid();

        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var inicio = dataInicio ?? hoje.AddDays(-30);
        var fim = dataFim ?? hoje;

        var resultado = await aulasServico.ObterFrequenciaAsync(
            usuarioId, unidadeId, turmaId, inicio, fim, cancellationToken);

        if (resultado.Estado == EstadoAulasUnidade.UnidadeNaoEncontrada)
            return NotFound();
        if (resultado.Estado != EstadoAulasUnidade.Sucesso
            || resultado.Valor is null
            || resultado.Contexto is null)
            return Forbid();

        return View(AulasViewModelMapper.MapearFrequencia(
            resultado.Contexto,
            resultado.Valor,
            turmaId,
            inicio,
            fim));
    }

    [HttpGet("inadimplencia")]
    public async Task<IActionResult> Inadimplencia(
        Guid unidadeId,
        CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId) return Forbid();

        var contexto = await ObterContextoAsync(usuarioId, unidadeId, cancellationToken);
        if (contexto is null) return Forbid();

        var (estado, relatorio) = await relatoriosServico.ObterInadimplenciaAsync(usuarioId, unidadeId);

        if (estado == EstadoRelatorios.UnidadeNaoEncontrada)
            return NotFound();
        if (estado != EstadoRelatorios.Sucesso || relatorio is null)
            return Forbid();

        return View(RelatorioViewModelMapper.MapearInadimplencia(contexto, relatorio));
    }

    [HttpGet("inadimplencia/{alunoId:guid}")]
    public async Task<IActionResult> InadimplenciaDetalhe(
        Guid unidadeId,
        Guid alunoId,
        CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId) return Forbid();

        var contexto = await ObterContextoAsync(usuarioId, unidadeId, cancellationToken);
        if (contexto is null) return Forbid();

        var (estado, detalhe) = await relatoriosServico.ObterInadimplenciaAlunoAsync(
            usuarioId, unidadeId, alunoId);

        if (estado == EstadoRelatorios.UnidadeNaoEncontrada)
            return NotFound();
        if (estado != EstadoRelatorios.Sucesso || detalhe is null)
            return RedirectToAction(nameof(Inadimplencia), new { unidadeId });

        return View(RelatorioViewModelMapper.MapearInadimplenciaDetalhe(contexto, detalhe));
    }

    private async Task<UnidadeAcessoResumo?> ObterContextoAsync(
        Guid usuarioId, Guid unidadeId, CancellationToken cancellationToken)
    {
        return await unidadesUsuarioConsulta.ObterAdministradaAsync(
            usuarioId, unidadeId, cancellationToken);
    }
}
