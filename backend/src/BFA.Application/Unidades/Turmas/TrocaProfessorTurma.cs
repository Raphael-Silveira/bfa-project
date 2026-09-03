using BFA.Application.Unidades;

namespace BFA.Application.Unidades.Turmas;

public sealed record ProfessorTrocaOpcao(Guid ProfessorUnidadeId, string NomeCompleto);

public sealed record TrocaProfessorTurmaResumo(
    Guid TurmaId,
    string NomeTurma,
    Guid ProfessorUnidadeAtualId,
    string NomeProfessorAtual,
    IReadOnlyList<TurmaHorarioResumo> HorariosAtuais,
    IReadOnlyList<ProfessorTrocaOpcao> ProfessoresDisponiveis);

public sealed record TrocarProfessorTurmaSolicitacao(
    Guid NovoProfessorUnidadeId, DateOnly DataTroca);

public enum EstadoTrocaProfessorTurma
{
    Sucesso,
    SemAcesso,
    TurmaNaoEncontrada,
    ProfessorNaoEncontrado,
    MesmoProfessor,
    VigenciaInvalida,
    ConflitoHorario,
    MigracaoGradeInvalida,
    Falha
}

public sealed record ResultadoTrocaProfessorTurma<T>(
    EstadoTrocaProfessorTurma Estado,
    T? Valor = default,
    ConflitoHorarioProfessor? Conflito = null,
    DateOnly? MenorDataTroca = null,
    int HorariosMigrados = 0,
    int GradesMigradas = 0);

public sealed record ResultadoPersistenciaTrocaProfessor(
    EstadoTrocaProfessorTurma Estado,
    int HorariosMigrados = 0,
    int GradesMigradas = 0);

public interface ITrocaProfessorTurmaRepositorio
{
    Task<TrocaProfessorTurmaResumo?> ObterAsync(
        Guid organizacaoId, Guid unidadeId, Guid turmaId,
        CancellationToken cancellationToken);

    Task<ResultadoPersistenciaTrocaProfessor> TrocarAsync(
        Guid organizacaoId, Guid unidadeId, Guid turmaId,
        Guid novoProfessorUnidadeId, DateOnly dataTroca,
        Guid usuarioId, DateTime atualizadoEmUtc,
        CancellationToken cancellationToken);
}

public interface ITrocaProfessorTurmaServico
{
    Task<ResultadoTrocaProfessorTurma<TrocaProfessorTurmaResumo>> ObterAsync(
        Guid usuarioId, Guid unidadeId, Guid turmaId,
        CancellationToken cancellationToken);

    Task<ResultadoTrocaProfessorTurma<Guid>> TrocarAsync(
        Guid usuarioId, Guid unidadeId, Guid turmaId,
        TrocarProfessorTurmaSolicitacao solicitacao,
        CancellationToken cancellationToken);
}

public sealed class TrocaProfessorTurmaServico(
    IUnidadeContextoConsulta unidadeContextoConsulta,
    IGovernancaOperacionalUnidade governancaOperacional,
    ITrocaProfessorTurmaRepositorio repositorio,
    TimeProvider timeProvider) : ITrocaProfessorTurmaServico
{
    public async Task<ResultadoTrocaProfessorTurma<TrocaProfessorTurmaResumo>> ObterAsync(
        Guid usuarioId, Guid unidadeId, Guid turmaId,
        CancellationToken cancellationToken)
    {
        var organizacaoId = await AutorizarAsync(usuarioId, unidadeId, cancellationToken);
        if (organizacaoId is null) return new(EstadoTrocaProfessorTurma.SemAcesso);
        var turma = await repositorio.ObterAsync(
            organizacaoId.Value, unidadeId, turmaId, cancellationToken);
        return turma is null
            ? new(EstadoTrocaProfessorTurma.TurmaNaoEncontrada)
            : new(EstadoTrocaProfessorTurma.Sucesso, turma,
                MenorDataTroca: MenorData(turma));
    }

    public async Task<ResultadoTrocaProfessorTurma<Guid>> TrocarAsync(
        Guid usuarioId, Guid unidadeId, Guid turmaId,
        TrocarProfessorTurmaSolicitacao solicitacao,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(solicitacao);
        var atual = await ObterAsync(usuarioId, unidadeId, turmaId, cancellationToken);
        if (atual.Estado != EstadoTrocaProfessorTurma.Sucesso || atual.Valor is null)
            return new(atual.Estado);
        if (solicitacao.NovoProfessorUnidadeId == atual.Valor.ProfessorUnidadeAtualId)
            return new(EstadoTrocaProfessorTurma.MesmoProfessor);
        if (!atual.Valor.ProfessoresDisponiveis.Any(item =>
                item.ProfessorUnidadeId == solicitacao.NovoProfessorUnidadeId))
            return new(EstadoTrocaProfessorTurma.ProfessorNaoEncontrado);
        if (atual.MenorDataTroca.HasValue
            && solicitacao.DataTroca < atual.MenorDataTroca.Value)
            return new(EstadoTrocaProfessorTurma.VigenciaInvalida,
                MenorDataTroca: atual.MenorDataTroca);

        var organizacaoId = await AutorizarAsync(usuarioId, unidadeId, cancellationToken);
        if (organizacaoId is null) return new(EstadoTrocaProfessorTurma.SemAcesso);
        var persistencia = await repositorio.TrocarAsync(
            organizacaoId.Value, unidadeId, turmaId,
            solicitacao.NovoProfessorUnidadeId, solicitacao.DataTroca,
            usuarioId, timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
        return persistencia.Estado == EstadoTrocaProfessorTurma.Sucesso
            ? new(persistencia.Estado, turmaId,
                HorariosMigrados: persistencia.HorariosMigrados,
                GradesMigradas: persistencia.GradesMigradas)
            : new(persistencia.Estado);
    }

    private async Task<Guid?> AutorizarAsync(
        Guid usuarioId, Guid unidadeId, CancellationToken cancellationToken)
    {
        var contexto = await unidadeContextoConsulta.ObterAtivaAsync(
            unidadeId, cancellationToken);
        if (contexto is null) return null;
        var governanca = await governancaOperacional.ObterAsync(
            usuarioId, contexto.OrganizacaoId, unidadeId, cancellationToken);
        return governanca.PodeGerenciarTurmas ? contexto.OrganizacaoId : null;
    }

    private static DateOnly? MenorData(TrocaProfessorTurmaResumo turma) =>
        turma.HorariosAtuais.Count == 0
            ? null
            : turma.HorariosAtuais.Max(item => item.VigenciaInicio).AddDays(1);
}
