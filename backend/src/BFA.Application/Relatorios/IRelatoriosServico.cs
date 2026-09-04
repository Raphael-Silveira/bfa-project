using BFA.Domain.Cobrancas;

namespace BFA.Application.Relatorios;

public enum EstadoRelatorios
{
    Sucesso,
    SemAcesso,
    UnidadeNaoEncontrada,
    DadosInvalidos,
    Falha
}

public sealed record ResumoGeralRelatorios(
    int TotalAlunosAtivos,
    int TotalMatriculasAtivas,
    int TotalAulasConcluidas,
    int TotalCobrancasPendentes,
    int TotalCobrancasAtrasadas,
    decimal TotalReceita,
    decimal TotalPendente,
    decimal TotalAtrasado);

public sealed record FiltroRelatorio(
    DateOnly? DataInicio,
    DateOnly? DataFim);

public sealed record FinanceiroDetalheRelatorio(
    decimal TotalReceita,
    decimal TotalPendente,
    decimal TotalAtrasado,
    IReadOnlyList<FinanceiroPorTipo> PorTipo,
    IReadOnlyList<FinanceiroPorStatus> PorStatus,
    IReadOnlyList<FinanceiroPorPeriodo> PorPeriodo);

public sealed record FinanceiroPorTipo(
    TipoCobranca Tipo,
    decimal Valor,
    int Quantidade);

public sealed record FinanceiroPorStatus(
    StatusCobranca Status,
    decimal Valor,
    int Quantidade);

public sealed record FinanceiroPorPeriodo(
    int Ano,
    int Mes,
    decimal Receita,
    decimal Pendente);

public sealed record InadimplenciaRelatorio(
    decimal TotalAtrasado,
    int TotalAlunos,
    IReadOnlyList<InadimplenciaAluno> Alunos,
    IReadOnlyList<FaixaAtraso> PorFaixa);

public sealed record InadimplenciaAluno(
    Guid AlunoId,
    string NomeCompleto,
    string? Cpf,
    int CobrancasAtrasadas,
    decimal ValorTotalAtrasado,
    DateOnly? PrimeiraDataVencimento,
    DateOnly? UltimaDataVencimento,
    int DiasEmAtraso,
    string FaixaAtraso);

public sealed record FaixaAtraso(
    string Faixa,
    int QuantidadeAlunos,
    decimal ValorTotal);

public sealed record CobrancaAtrasadaDetalhe(
    Guid CobrancaId,
    string Descricao,
    string Tipo,
    decimal Valor,
    decimal ValorPago,
    decimal SaldoDevedor,
    DateOnly DataVencimento,
    int DiasAtraso);

public sealed record InadimplenciaAlunoDetalhe(
    Guid AlunoId,
    string NomeCompleto,
    string? Cpf,
    int DiasEmAtraso,
    decimal ValorTotalAtrasado,
    IReadOnlyList<CobrancaAtrasadaDetalhe> Cobrancas);

public interface IRelatoriosServico
{
    Task<(EstadoRelatorios Estado, ResumoGeralRelatorios? Resumo)> ObterResumoGeralAsync(
        Guid usuarioId, Guid unidadeId);

    Task<(EstadoRelatorios Estado, FinanceiroDetalheRelatorio? Relatorio)> ObterFinanceiroDetalhadoAsync(
        Guid usuarioId, Guid unidadeId, FiltroRelatorio filtro);

    Task<(EstadoRelatorios Estado, InadimplenciaRelatorio? Relatorio)> ObterInadimplenciaAsync(
        Guid usuarioId, Guid unidadeId);

    Task<(EstadoRelatorios Estado, InadimplenciaAlunoDetalhe? Detalhe)> ObterInadimplenciaAlunoAsync(
        Guid usuarioId, Guid unidadeId, Guid alunoId);
}
