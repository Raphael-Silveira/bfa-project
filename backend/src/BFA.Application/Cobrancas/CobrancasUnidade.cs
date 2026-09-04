using BFA.Domain.Cobrancas;

namespace BFA.Application.Cobrancas;

public enum EstadoCobrancas
{
    Sucesso,
    SemAcesso,
    UnidadeNaoEncontrada,
    CobrancaNaoEncontrada,
    AlunoNaoEncontrado,
    MatriculaNaoEncontrada,
    CobrancaNaoPendente,
    ValorExcedeSaldo,
    DadosInvalidos,
    Falha
}

public sealed record CobrancaListaItem(
    Guid CobrancaId,
    Guid AlunoId,
    string AlunoNome,
    string Descricao,
    TipoCobranca Tipo,
    decimal Valor,
    decimal ValorPago,
    DateOnly DataVencimento,
    StatusCobranca Status);

public sealed record CobrancaDetalhe(
    Guid CobrancaId,
    Guid AlunoId,
    string AlunoNome,
    string? AlunoCpf,
    string Descricao,
    TipoCobranca Tipo,
    decimal Valor,
    decimal ValorPago,
    decimal SaldoDevedor,
    DateOnly DataEmissao,
    DateOnly DataVencimento,
    DateOnly? DataPagamento,
    StatusCobranca Status,
    string? Observacoes,
    IReadOnlyList<PagamentoResumo> Pagamentos);

public sealed record PagamentoResumo(
    Guid PagamentoId,
    decimal Valor,
    DateOnly DataPagamento,
    FormaPagamento FormaPagamento,
    string? Observacoes);

public sealed record CriarCobrancaSolicitacao(
    Guid AlunoId,
    Guid MatriculaId,
    TipoCobranca Tipo,
    string Descricao,
    decimal Valor,
    DateOnly DataVencimento,
    string? Observacoes);

public sealed record RegistrarPagamentoSolicitacao(
    decimal Valor,
    DateOnly DataPagamento,
    FormaPagamento FormaPagamento,
    string? Observacoes);

public sealed record FiltroCobrancas(
    Guid? AlunoId,
    StatusCobranca? Status,
    TipoCobranca? Tipo,
    DateOnly? DataVencimentoInicio,
    DateOnly? DataVencimentoFim);

public sealed record ResumoFinanceiro(
    decimal TotalReceita,
    decimal TotalPendente,
    decimal TotalAtrasado,
    int CobrancasPendentes,
    int CobrancasAtrasadas,
    int TotalAlunosComDebito);

public sealed record AlunoParaSelecao(
    Guid AlunoId,
    string NomeCompleto,
    string? Cpf,
    Guid MatriculaId);

public interface ICobrancasServico
{
    Task<(EstadoCobrancas Estado, IReadOnlyList<CobrancaListaItem> Itens)> ListarAsync(
        Guid usuarioId, Guid unidadeId, FiltroCobrancas filtro);

    Task<(EstadoCobrancas Estado, CobrancaDetalhe? Detalhe)> ObterAsync(
        Guid usuarioId, Guid unidadeId, Guid cobrancaId);

    Task<(EstadoCobrancas Estado, CobrancaListaItem? Item)> CriarAsync(
        Guid usuarioId, Guid unidadeId, CriarCobrancaSolicitacao solicitacao);

    Task<EstadoCobrancas> CancelarAsync(
        Guid usuarioId, Guid unidadeId, Guid cobrancaId);

    Task<(EstadoCobrancas Estado, PagamentoResumo? Pagamento)> RegistrarPagamentoAsync(
        Guid usuarioId, Guid unidadeId, Guid cobrancaId, RegistrarPagamentoSolicitacao solicitacao);

    Task<(EstadoCobrancas Estado, IReadOnlyList<AlunoParaSelecao> Alunos)> ListarAlunosAsync(
        Guid usuarioId, Guid unidadeId);

    Task<(EstadoCobrancas Estado, ResumoFinanceiro? Resumo)> ObterResumoFinanceiroAsync(
        Guid usuarioId, Guid unidadeId);
}
