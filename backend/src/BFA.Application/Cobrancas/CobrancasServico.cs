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

    public async Task<(EstadoCobrancas Estado, PagamentoResumo? Pagamento)> RegistrarPagamentoAsync(
        Guid usuarioId, Guid unidadeId, Guid cobrancaId, RegistrarPagamentoSolicitacao solicitacao)
    {
        var contexto = await ObterContextoAsync(usuarioId, unidadeId, exigirGerenciamento: true);
        if (contexto.Estado != EstadoCobrancas.Sucesso)
            return (contexto.Estado, null);

        if (solicitacao.Valor <= 0)
            return (EstadoCobrancas.DadosInvalidos, null);

        var cobranca = await repositorio.ObterPorIdAsync(
            contexto.Valor!.OrganizacaoId, unidadeId, cobrancaId, CancellationToken.None);

        if (cobranca is null)
            return (EstadoCobrancas.CobrancaNaoEncontrada, null);

        if (cobranca.Status is StatusCobranca.Paga or StatusCobranca.Cancelada)
            return (EstadoCobrancas.CobrancaNaoPendente, null);

        var saldoDevedor = cobranca.Valor - cobranca.ValorPago;
        if (solicitacao.Valor > saldoDevedor)
            return (EstadoCobrancas.ValorExcedeSaldo, null);

        var agora = timeProvider.GetUtcNow().UtcDateTime;

        var pagamento = new Pagamento(
            Guid.NewGuid(),
            contexto.Valor.OrganizacaoId,
            unidadeId,
            cobrancaId,
            solicitacao.Valor,
            solicitacao.DataPagamento,
            solicitacao.FormaPagamento,
            usuarioId,
            agora);

        await repositorio.RegistrarPagamentoAsync(pagamento, CancellationToken.None);

        logger.LogInformation(
            "Pagamento registrado: {PagamentoId} para cobranca {CobrancaId} na unidade {UnidadeId}",
            pagamento.Id, cobrancaId, unidadeId);

        var resumo = new PagamentoResumo(
            pagamento.Id,
            pagamento.Valor,
            pagamento.DataPagamento,
            pagamento.FormaPagamento,
            pagamento.Observacoes);

        return (EstadoCobrancas.Sucesso, resumo);
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
