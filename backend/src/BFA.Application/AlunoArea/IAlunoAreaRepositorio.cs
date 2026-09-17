using BFA.Domain.Alunos;
using BFA.Domain.Cobrancas;
using BFA.Domain.Matriculas;

namespace BFA.Application.AlunoArea;

public sealed record AlunoComUnidade(
    Aluno Aluno,
    Guid OrganizacaoId,
    Guid UnidadeId);

public interface IAlunoAreaRepositorio
{
    Task<AlunoComUnidade?> ObterAlunoPorUsuarioAsync(
        Guid usuarioId,
        Guid unidadeId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<MatriculaAlunoConsulta>> ListarMatriculasAsync(
        Guid organizacaoId,
        Guid unidadeId,
        Guid alunoId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<(Guid AulaId, string TurmaNome, DateOnly Data, string HoraInicio, string HoraFim, string Status, bool ConfirmacaoAtiva)>> ListarAulasAsync(
        Guid organizacaoId,
        Guid unidadeId,
        Guid alunoId,
        DateOnly dataInicio,
        DateOnly dataFim,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<(DateOnly Data, string TurmaNome, string HoraInicio, string HoraFim, string Status, string? Observacoes)>> ListarPresencasAsync(
        Guid organizacaoId,
        Guid unidadeId,
        Guid alunoId,
        DateOnly dataInicio,
        DateOnly dataFim,
        CancellationToken cancellationToken);

    Task<int> ContarAulasAsync(
        Guid organizacaoId,
        Guid unidadeId,
        Guid alunoId,
        DateOnly dataInicio,
        DateOnly dataFim,
        CancellationToken cancellationToken);

    Task<int> ContarPresencasAsync(
        Guid organizacaoId,
        Guid unidadeId,
        Guid alunoId,
        DateOnly dataInicio,
        DateOnly dataFim,
        CancellationToken cancellationToken);

    Task<int> ContarAusenciasAsync(
        Guid organizacaoId,
        Guid unidadeId,
        Guid alunoId,
        DateOnly dataInicio,
        DateOnly dataFim,
        CancellationToken cancellationToken);

    Task<int> ContarJustificativasAsync(
        Guid organizacaoId,
        Guid unidadeId,
        Guid alunoId,
        DateOnly dataInicio,
        DateOnly dataFim,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Cobranca>> ListarCobrancasAsync(
        Guid organizacaoId,
        Guid unidadeId,
        Guid alunoId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Pagamento>> ListarPagamentosAsync(
        Guid organizacaoId,
        Guid unidadeId,
        Guid alunoId,
        CancellationToken cancellationToken);

    Task<string?> ObterNomeUnidadeAsync(
        Guid organizacaoId,
        Guid unidadeId,
        CancellationToken cancellationToken);

    Task<AulaConfirmacaoConsulta?> ObterAulaParaConfirmacaoAsync(
        Guid organizacaoId,
        Guid unidadeId,
        Guid alunoId,
        Guid aulaId,
        CancellationToken cancellationToken);

    Task<bool> ConfirmarAulaAsync(
        Guid organizacaoId,
        Guid unidadeId,
        Guid aulaId,
        Guid alunoId,
        DateTime agoraUtc,
        CancellationToken cancellationToken);

    Task<bool> CancelarConfirmacaoAulaAsync(
        Guid organizacaoId,
        Guid unidadeId,
        Guid aulaId,
        Guid alunoId,
        DateTime agoraUtc,
        CancellationToken cancellationToken);
}
