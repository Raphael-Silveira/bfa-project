using BFA.Application.Unidades;
using BFA.Domain.Cobrancas;
using Microsoft.Extensions.Logging;

namespace BFA.Application.Cobrancas;

public sealed class CobrancasServico(
    ICobrancasRepositorio repositorio,
    IGovernancaOperacionalUnidade governancaOperacional,
    IUnidadeContextoConsulta unidadeContextoConsulta,
    TimeProvider timeProvider,
    ILogger<CobrancasServico> logger) : ICobrancasServico
{
    public async Task<(EstadoCobrancas Estado, IReadOnlyList<CobrancaListaItem> Itens)> ListarAsync(
        Guid usuarioId, Guid unidadeId, FiltroCobrancas filtro)
    {
        var contexto = await ObterContextoAsync(usuarioId, unidadeId);
        if (contexto.Estado != EstadoCobrancas.Sucesso)
            return (contexto.Estado, []);

        var itens = await repositorio.ListarAsync(
            contexto.Valor!.OrganizacaoId, unidadeId, filtro, CancellationToken.None);

        return (EstadoCobrancas.Sucesso, itens);
    }

    public async Task<(EstadoCobrancas Estado, CobrancaDetalhe? Detalhe)> ObterAsync(
        Guid usuarioId, Guid unidadeId, Guid cobrancaId)
    {
        var contexto = await ObterContextoAsync(usuarioId, unidadeId);
        if (contexto.Estado != EstadoCobrancas.Sucesso)
            return (contexto.Estado, null);

        if (cobrancaId == Guid.Empty)
            return (EstadoCobrancas.CobrancaNaoEncontrada, null);

        var detalhe = await repositorio.ObterAsync(
            contexto.Valor!.OrganizacaoId, unidadeId, cobrancaId, CancellationToken.None);

        if (detalhe is null)
            return (EstadoCobrancas.CobrancaNaoEncontrada, null);

        return (EstadoCobrancas.Sucesso, detalhe);
    }

    public async Task<(EstadoCobrancas Estado, IReadOnlyList<CobrancaListaItem> Itens)> ListarPorAlunoAsync(
        Guid usuarioId, Guid unidadeId, Guid alunoId)
    {
        var contexto = await ObterContextoAsync(usuarioId, unidadeId);
        if (contexto.Estado != EstadoCobrancas.Sucesso)
            return (contexto.Estado, []);

        if (alunoId == Guid.Empty)
            return (EstadoCobrancas.CobrancaNaoEncontrada, []);

        var itens = await repositorio.ListarPorAlunoAsync(
            contexto.Valor!.OrganizacaoId, unidadeId, alunoId, CancellationToken.None);

        return (EstadoCobrancas.Sucesso, itens);
    }

    public async Task<(EstadoCobrancas Estado, CobrancaListaItem? Item)> CriarAsync(
        Guid usuarioId, Guid unidadeId, CriarCobrancaSolicitacao solicitacao)
    {
        var contexto = await ObterContextoAsync(usuarioId, unidadeId, exigirGerenciamento: true);
        if (contexto.Estado != EstadoCobrancas.Sucesso)
            return (contexto.Estado, null);

        if (solicitacao.AlunoId == Guid.Empty
            || solicitacao.MatriculaId == Guid.Empty
            || string.IsNullOrWhiteSpace(solicitacao.Descricao)
            || solicitacao.Valor <= 0)
        {
            return (EstadoCobrancas.DadosInvalidos, null);
        }

        var agora = timeProvider.GetUtcNow().UtcDateTime;
        var hoje = DateOnly.FromDateTime(agora);

        if (solicitacao.DataVencimento < hoje)
            return (EstadoCobrancas.DadosInvalidos, null);

        var cobranca = new Cobranca(
            Guid.NewGuid(),
            contexto.Valor!.OrganizacaoId,
            unidadeId,
            solicitacao.AlunoId,
            solicitacao.MatriculaId,
            solicitacao.Tipo,
            solicitacao.Descricao,
            solicitacao.Valor,
            hoje,
            solicitacao.DataVencimento,
            usuarioId,
            agora);

        cobranca.AtualizarObservacoes(solicitacao.Observacoes, usuarioId, agora);

        await repositorio.CriarAsync(cobranca, CancellationToken.None);

        var aluno = await repositorio.ListarAlunosAsync(
            contexto.Valor.OrganizacaoId, unidadeId, CancellationToken.None);

        var alunoSelecionado = aluno.FirstOrDefault(a => a.AlunoId == solicitacao.AlunoId);

        var item = new CobrancaListaItem(
            cobranca.Id,
            cobranca.AlunoId,
            alunoSelecionado?.NomeCompleto ?? "Aluno",
            cobranca.Descricao,
            cobranca.Tipo,
            cobranca.Valor,
            cobranca.ValorPago,
            cobranca.DataVencimento,
            cobranca.Status);

        logger.LogInformation(
            "Cobranca criada: {CobrancaId} para aluno {AlunoId} na unidade {UnidadeId}",
            cobranca.Id, solicitacao.AlunoId, unidadeId);

        return (EstadoCobrancas.Sucesso, item);
    }

    public async Task<EstadoCobrancas> CancelarAsync(
        Guid usuarioId, Guid unidadeId, Guid cobrancaId)
    {
        var contexto = await ObterContextoAsync(usuarioId, unidadeId, exigirGerenciamento: true);
        if (contexto.Estado != EstadoCobrancas.Sucesso)
            return contexto.Estado;

        var cobranca = await repositorio.ObterPorIdAsync(
            contexto.Valor!.OrganizacaoId, unidadeId, cobrancaId, CancellationToken.None);

        if (cobranca is null)
            return EstadoCobrancas.CobrancaNaoEncontrada;

        if (cobranca.Status != StatusCobranca.Pendente)
            return EstadoCobrancas.CobrancaNaoPendente;

        var agora = timeProvider.GetUtcNow().UtcDateTime;
        cobranca.Cancelar(usuarioId, agora);

        await repositorio.CancelarAsync(cobranca, CancellationToken.None);

        logger.LogInformation(
            "Cobranca cancelada: {CobrancaId} na unidade {UnidadeId}",
            cobrancaId, unidadeId);

        return EstadoCobrancas.Sucesso;
    }

    public async Task<(EstadoCobrancas Estado, IReadOnlyList<AlunoParaSelecao> Alunos)> ListarAlunosAsync(
        Guid usuarioId, Guid unidadeId)
    {
        var contexto = await ObterContextoAsync(usuarioId, unidadeId);
        if (contexto.Estado != EstadoCobrancas.Sucesso)
            return (contexto.Estado, []);

        var alunos = await repositorio.ListarAlunosAsync(
            contexto.Valor!.OrganizacaoId, unidadeId, CancellationToken.None);

        return (EstadoCobrancas.Sucesso, alunos);
    }

    public async Task<(EstadoCobrancas Estado, ResumoFinanceiro? Resumo)> ObterResumoFinanceiroAsync(
        Guid usuarioId, Guid unidadeId)
    {
        var contexto = await ObterContextoAsync(usuarioId, unidadeId);
        if (contexto.Estado != EstadoCobrancas.Sucesso)
            return (contexto.Estado, null);

        var resumo = await repositorio.ObterResumoAsync(
            contexto.Valor!.OrganizacaoId, unidadeId, CancellationToken.None);

        return (EstadoCobrancas.Sucesso, resumo);
    }

    public async Task<(EstadoCobrancas Estado, IReadOnlyList<PagamentoResumo> Pagamentos)> RegistrarPagamentoConsolidadoAsync(
        Guid usuarioId, Guid unidadeId, RegistrarPagamentoConsolidadoSolicitacao solicitacao)
    {
        var contexto = await ObterContextoAsync(usuarioId, unidadeId, exigirGerenciamento: true);
        if (contexto.Estado != EstadoCobrancas.Sucesso)
            return (contexto.Estado, []);

        if (solicitacao.CobrancaIds.Count == 0)
            return (EstadoCobrancas.DadosInvalidos, []);

        var pagamentos = await repositorio.RegistrarPagamentoConsolidadoAsync(
            contexto.Valor!.OrganizacaoId,
            unidadeId,
            solicitacao.CobrancaIds,
            solicitacao.DataPagamento,
            solicitacao.FormaPagamento,
            solicitacao.Observacoes,
            usuarioId,
            timeProvider.GetUtcNow().UtcDateTime,
            CancellationToken.None);

        if (pagamentos.Count == 0)
            return (EstadoCobrancas.Falha, []);

        logger.LogInformation(
            "Pagamento consolidado registrado: {Count} pagamentos para {CobrancaIds} na unidade {UnidadeId}",
            pagamentos.Count, string.Join(",", solicitacao.CobrancaIds), unidadeId);

        return (EstadoCobrancas.Sucesso, pagamentos);
    }

    private async Task<(EstadoCobrancas Estado, UnidadeContextoResumo? Valor)> ObterContextoAsync(
        Guid usuarioId, Guid unidadeId, bool exigirGerenciamento = false)
    {
        var unidade = await unidadeContextoConsulta.ObterAtivaAsync(
            unidadeId, CancellationToken.None);

        if (unidade is null)
            return (EstadoCobrancas.UnidadeNaoEncontrada, null);

        var governanca = await governancaOperacional.ObterAsync(
            usuarioId, unidade.OrganizacaoId, unidadeId, CancellationToken.None);

        if (!governanca.PodeAcessar)
        {
            logger.LogWarning(
                "Cobrancas acesso negado: usuario {UsuarioId} na unidade {UnidadeId}",
                usuarioId, unidadeId);
            return (EstadoCobrancas.SemAcesso, null);
        }

        if (exigirGerenciamento && !governanca.PodeGerenciarMatriculas)
        {
            logger.LogWarning(
                "Cobrancas gerenciamento negado: usuario {UsuarioId} na unidade {UnidadeId}",
                usuarioId, unidadeId);
            return (EstadoCobrancas.SemAcesso, null);
        }

        return (EstadoCobrancas.Sucesso, unidade);
    }
}
