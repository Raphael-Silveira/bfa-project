using BFA.Application.Unidades;
using BFA.Domain.Cobrancas;
using Microsoft.Extensions.Logging;

namespace BFA.Application.Relatorios;

public sealed class RelatoriosServico(
    IRelatoriosRepositorio repositorio,
    IGovernancaOperacionalUnidade governancaOperacional,
    IUnidadeContextoConsulta unidadeContextoConsulta,
    ILogger<RelatoriosServico> logger) : IRelatoriosServico
{
    public async Task<(EstadoRelatorios Estado, ResumoGeralRelatorios? Resumo)> ObterResumoGeralAsync(
        Guid usuarioId, Guid unidadeId)
    {
        var contexto = await ObterContextoAsync(usuarioId, unidadeId);
        if (contexto.Estado != EstadoRelatorios.Sucesso)
            return (contexto.Estado, null);

        var orgId = contexto.Valor!.OrganizacaoId;
        var hoje = DateOnly.FromDateTime(DateTime.Today);

        var totalAlunosAtivos = await repositorio.ContarAlunosAtivosAsync(
            orgId, unidadeId, CancellationToken.None);

        var totalAulasConcluidas = await repositorio.ContarAulasConcluidasAsync(
            orgId, unidadeId, hoje, CancellationToken.None);

        var cobrancas = await repositorio.ListarCobrancasAsync(
            orgId, unidadeId, null, null, CancellationToken.None);

        var totalPendente = cobrancas
            .Where(c => c.Status == StatusCobranca.Pendente)
            .Sum(c => c.Valor - c.ValorPago);

        var totalAtrasado = cobrancas
            .Where(c => c.Status == StatusCobranca.Atrasada)
            .Sum(c => c.Valor - c.ValorPago);

        var totalReceita = cobrancas
            .Where(c => c.Status == StatusCobranca.Paga)
            .Sum(c => c.ValorPago);

        var resumo = new ResumoGeralRelatorios(
            totalAlunosAtivos,
            totalAlunosAtivos,
            totalAulasConcluidas,
            cobrancas.Count(c => c.Status == StatusCobranca.Pendente),
            cobrancas.Count(c => c.Status == StatusCobranca.Atrasada),
            totalReceita,
            totalPendente,
            totalAtrasado);

        return (EstadoRelatorios.Sucesso, resumo);
    }

    public async Task<(EstadoRelatorios Estado, FinanceiroDetalheRelatorio? Relatorio)> ObterFinanceiroDetalhadoAsync(
        Guid usuarioId, Guid unidadeId, FiltroRelatorio filtro)
    {
        var contexto = await ObterContextoAsync(usuarioId, unidadeId);
        if (contexto.Estado != EstadoRelatorios.Sucesso)
            return (contexto.Estado, null);

        var orgId = contexto.Valor!.OrganizacaoId;

        var cobrancas = await repositorio.ListarCobrancasAsync(
            orgId, unidadeId, filtro.DataInicio, filtro.DataFim, CancellationToken.None);

        var totalReceita = cobrancas
            .Where(c => c.Status == StatusCobranca.Paga)
            .Sum(c => c.ValorPago);

        var totalPendente = cobrancas
            .Where(c => c.Status == StatusCobranca.Pendente)
            .Sum(c => c.Valor - c.ValorPago);

        var totalAtrasado = cobrancas
            .Where(c => c.Status == StatusCobranca.Atrasada)
            .Sum(c => c.Valor - c.ValorPago);

        var porTipo = cobrancas
            .GroupBy(c => c.Tipo)
            .Select(g => new FinanceiroPorTipo(
                g.Key,
                g.Sum(c => c.Valor),
                g.Count()))
            .OrderBy(x => x.Tipo)
            .ToList();

        var porStatus = cobrancas
            .GroupBy(c => c.Status)
            .Select(g => new FinanceiroPorStatus(
                g.Key,
                g.Sum(c => c.Status == StatusCobranca.Paga ? c.ValorPago : c.Valor - c.ValorPago),
                g.Count()))
            .OrderBy(x => x.Status)
            .ToList();

        var porPeriodo = cobrancas
            .GroupBy(c => new { c.DataEmissao.Year, c.DataEmissao.Month })
            .Select(g => new FinanceiroPorPeriodo(
                g.Key.Year,
                g.Key.Month,
                g.Where(c => c.Status == StatusCobranca.Paga).Sum(c => c.ValorPago),
                g.Where(c => c.Status is StatusCobranca.Pendente or StatusCobranca.Atrasada)
                    .Sum(c => c.Valor - c.ValorPago)))
            .OrderBy(x => x.Ano)
            .ThenBy(x => x.Mes)
            .ToList();

        var relatorio = new FinanceiroDetalheRelatorio(
            totalReceita,
            totalPendente,
            totalAtrasado,
            porTipo,
            porStatus,
            porPeriodo);

        return (EstadoRelatorios.Sucesso, relatorio);
    }

    public async Task<(EstadoRelatorios Estado, InadimplenciaRelatorio? Relatorio)> ObterInadimplenciaAsync(
        Guid usuarioId, Guid unidadeId)
    {
        var contexto = await ObterContextoAsync(usuarioId, unidadeId);
        if (contexto.Estado != EstadoRelatorios.Sucesso)
            return (contexto.Estado, null);

        var orgId = contexto.Valor!.OrganizacaoId;

        var cobrancas = await repositorio.ListarCobrancasAsync(
            orgId, unidadeId, null, null, CancellationToken.None);

        var cobrancasAtrasadas = cobrancas
            .Where(c => c.Status == StatusCobranca.Atrasada)
            .ToList();

        if (cobrancasAtrasadas.Count == 0)
        {
            return (EstadoRelatorios.Sucesso, new InadimplenciaRelatorio(0, 0, []));
        }

        var totalAtrasado = cobrancasAtrasadas.Sum(c => c.Valor - c.ValorPago);

        var alunosInadimplentes = cobrancasAtrasadas
            .GroupBy(c => c.AlunoId)
            .Select(g => new InadimplenciaAluno(
                g.Key,
                "Aluno",
                null,
                g.Count(),
                g.Sum(c => c.Valor - c.ValorPago),
                g.Min(c => c.DataVencimento),
                g.Max(c => c.DataVencimento)))
            .OrderByDescending(x => x.ValorTotalAtrasado)
            .ToList();

        var relatorio = new InadimplenciaRelatorio(
            totalAtrasado,
            alunosInadimplentes.Count,
            alunosInadimplentes);

        return (EstadoRelatorios.Sucesso, relatorio);
    }

    private async Task<(EstadoRelatorios Estado, UnidadeContextoResumo? Valor)> ObterContextoAsync(
        Guid usuarioId, Guid unidadeId)
    {
        var unidade = await unidadeContextoConsulta.ObterAtivaAsync(
            unidadeId, CancellationToken.None);

        if (unidade is null)
            return (EstadoRelatorios.UnidadeNaoEncontrada, null);

        var governanca = await governancaOperacional.ObterAsync(
            usuarioId, unidade.OrganizacaoId, unidadeId, CancellationToken.None);

        if (!governanca.PodeAcessar)
        {
            logger.LogWarning(
                "Relatorios acesso negado: usuario {UsuarioId} na unidade {UnidadeId}",
                usuarioId, unidadeId);
            return (EstadoRelatorios.SemAcesso, null);
        }

        return (EstadoRelatorios.Sucesso, unidade);
    }
}
