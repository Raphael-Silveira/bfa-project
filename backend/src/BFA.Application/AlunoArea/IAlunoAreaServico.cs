using BFA.Domain.Acessos;

namespace BFA.Application.AlunoArea;

public sealed record PerfilAlunoDto(
    Guid AlunoId,
    string NomeCompleto,
    string? Cpf,
    string? Telefone,
    string? Email,
    DateOnly DataNascimento,
    bool Ativo);

public sealed record MatriculaAlunoDto(
    Guid MatriculaId,
    string PlanoNome,
    string Status,
    DateOnly DataInicio,
    DateOnly DataFimPrevista,
    DateOnly? DataFimReal,
    decimal ValorMensal,
    IReadOnlyList<HorarioMatriculaDto> Horarios);

public sealed record HorarioMatriculaDto(
    string DiaSemana,
    string HoraInicio,
    string HoraFim,
    string TurmaNome);

public sealed record AulaAlunoDto(
    Guid AulaId,
    DateOnly Data,
    string HoraInicio,
    string HoraFim,
    string TurmaNome,
    string Status);

public sealed record PresencaAlunoDto(
    DateOnly Data,
    string TurmaNome,
    string HoraInicio,
    string HoraFim,
    string Status,
    string? Observacoes);

public sealed record FrequenciaResumoDto(
    int TotalAulas,
    int Presentes,
    int Ausentes,
    int Justificados,
    decimal PercentualFrequencia);

public sealed record CobrancaAlunoDto(
    Guid CobrancaId,
    string Descricao,
    string Tipo,
    string Valor,
    string ValorPago,
    string SaldoDevedor,
    DateOnly DataVencimento,
    string Status,
    int DiasAtraso);

public sealed record PagamentoAlunoDto(
    DateOnly DataPagamento,
    string Valor,
    string FormaPagamento);

public sealed record FinanceiroResumoDto(
    string TotalPendente,
    string TotalPago,
    IReadOnlyList<CobrancaAlunoDto> Cobrancas,
    IReadOnlyList<PagamentoAlunoDto> Pagamentos);

public sealed record DashboardAlunoDto(
    PerfilAlunoDto Perfil,
    string NomeUnidade,
    string? ProximaAula,
    string PercentualFrequencia,
    string TotalPendente,
    int TotalAulas);

public interface IAlunoAreaServico
{
    Task<DashboardAlunoDto?> ObterDashboardAsync(
        Guid usuarioId,
        Guid unidadeId,
        CancellationToken cancellationToken);

    Task<PerfilAlunoDto?> ObterPerfilAsync(
        Guid usuarioId,
        Guid unidadeId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<MatriculaAlunoDto>> ObterMatriculasAsync(
        Guid usuarioId,
        Guid unidadeId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<AulaAlunoDto>> ObterAgendaAsync(
        Guid usuarioId,
        Guid unidadeId,
        DateOnly dataInicio,
        DateOnly dataFim,
        CancellationToken cancellationToken);

    Task<FrequenciaResumoDto?> ObterFrequenciaAsync(
        Guid usuarioId,
        Guid unidadeId,
        DateOnly dataInicio,
        DateOnly dataFim,
        CancellationToken cancellationToken);

    Task<FinanceiroResumoDto?> ObterFinanceiroAsync(
        Guid usuarioId,
        Guid unidadeId,
        CancellationToken cancellationToken);
}
