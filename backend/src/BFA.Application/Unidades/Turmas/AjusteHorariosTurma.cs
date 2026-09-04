using BFA.Application.Acessos;
using BFA.Application.Professores.Turmas;
using BFA.Application.Unidades;
using BFA.Domain.Acessos;
using BFA.Domain.Turmas;
using Microsoft.Extensions.Logging;

namespace BFA.Application.Unidades.Turmas;

public sealed record NovoHorarioTurmaSolicitacao(
    DiaSemana DiaSemana, TimeOnly HoraInicio, TimeOnly HoraFim);

public sealed record AjustarHorariosTurmaSolicitacao(
    DateOnly NovaVigenciaInicio,
    IReadOnlyList<NovoHorarioTurmaSolicitacao> Horarios);

public sealed record ProgramacaoTurmaResumo(
    Guid TurmaId,
    string NomeTurma,
    Guid ProfessorUnidadeId,
    string NomeProfessor,
    IReadOnlyList<TurmaHorarioResumo> HorariosAtuais);

public enum EstadoAjusteHorariosTurma
{
    Sucesso,
    SemAcesso,
    TurmaNaoEncontrada,
    SemHorarios,
    DadosInvalidos,
    VigenciaInvalida,
    ExisteGradeAfetada,
    ConflitoHorario,
    Falha
}

public sealed record ResultadoAjusteHorariosTurma<T>(
    EstadoAjusteHorariosTurma Estado,
    T? Valor = default,
    ConflitoHorarioProfessor? Conflito = null,
    DateOnly? MenorVigenciaPermitida = null);

public interface IAjusteHorariosTurmaRepositorio
{
    Task<ProgramacaoTurmaResumo?> ObterAsync(
        Guid organizacaoId, Guid unidadeId, Guid turmaId,
        CancellationToken cancellationToken);

    Task<ConflitoHorarioProfessor?> ObterConflitoAsync(
        Guid organizacaoId, Guid professorUnidadeId, Guid turmaId,
        DateOnly novaVigenciaInicio, NovoHorarioTurmaSolicitacao horario,
        CancellationToken cancellationToken);

    Task<EstadoAjusteHorariosTurma> AjustarAsync(
        Guid organizacaoId, Guid unidadeId, Guid turmaId, Guid usuarioId,
        AjustarHorariosTurmaSolicitacao solicitacao, DateTime atualizadoEmUtc,
        CancellationToken cancellationToken);
}

public interface IAjusteHorariosTurmaServico
{
    Task<ResultadoAjusteHorariosTurma<ProgramacaoTurmaResumo>> ObterAdministracaoAsync(
        Guid usuarioId, Guid unidadeId, Guid turmaId,
        CancellationToken cancellationToken);

    Task<ResultadoAjusteHorariosTurma<ProgramacaoTurmaResumo>> ObterProfessorAsync(
        Guid usuarioId, Guid unidadeId, Guid turmaId,
        CancellationToken cancellationToken);

    Task<ResultadoAjusteHorariosTurma<Guid>> AjustarAdministracaoAsync(
        Guid usuarioId, Guid unidadeId, Guid turmaId,
        AjustarHorariosTurmaSolicitacao solicitacao,
        CancellationToken cancellationToken);

    Task<ResultadoAjusteHorariosTurma<Guid>> AjustarProfessorAsync(
        Guid usuarioId, Guid unidadeId, Guid turmaId,
        AjustarHorariosTurmaSolicitacao solicitacao,
        CancellationToken cancellationToken);
}

public sealed class AjusteHorariosTurmaServico(
    IUnidadeContextoConsulta unidadeContextoConsulta,
    IUnidadesUsuarioConsulta unidadesUsuarioConsulta,
    IAcessoUsuarioConsulta acessoUsuarioConsulta,
    IGovernancaOperacionalUnidade governancaOperacional,
    IMinhasTurmasProfessorRepositorio minhasTurmasRepositorio,
    IAjusteHorariosTurmaRepositorio repositorio,
    TimeProvider timeProvider,
    ILogger<AjusteHorariosTurmaServico> logger) : IAjusteHorariosTurmaServico
{
    public Task<ResultadoAjusteHorariosTurma<ProgramacaoTurmaResumo>>
        ObterAdministracaoAsync(Guid usuarioId, Guid unidadeId, Guid turmaId,
            CancellationToken cancellationToken) =>
        ObterAsync(usuarioId, unidadeId, turmaId, professor: false, cancellationToken);

    public Task<ResultadoAjusteHorariosTurma<ProgramacaoTurmaResumo>>
        ObterProfessorAsync(Guid usuarioId, Guid unidadeId, Guid turmaId,
            CancellationToken cancellationToken) =>
        ObterAsync(usuarioId, unidadeId, turmaId, professor: true, cancellationToken);

    public Task<ResultadoAjusteHorariosTurma<Guid>> AjustarAdministracaoAsync(
        Guid usuarioId, Guid unidadeId, Guid turmaId,
        AjustarHorariosTurmaSolicitacao solicitacao,
        CancellationToken cancellationToken) =>
        AjustarAsync(usuarioId, unidadeId, turmaId, solicitacao,
            professor: false, cancellationToken);

    public Task<ResultadoAjusteHorariosTurma<Guid>> AjustarProfessorAsync(
        Guid usuarioId, Guid unidadeId, Guid turmaId,
        AjustarHorariosTurmaSolicitacao solicitacao,
        CancellationToken cancellationToken) =>
        AjustarAsync(usuarioId, unidadeId, turmaId, solicitacao,
            professor: true, cancellationToken);

    private async Task<ResultadoAjusteHorariosTurma<ProgramacaoTurmaResumo>> ObterAsync(
        Guid usuarioId, Guid unidadeId, Guid turmaId, bool professor,
        CancellationToken cancellationToken)
    {
        var contexto = await ResolverContextoAsync(
            usuarioId, unidadeId, professor, cancellationToken);
        if (contexto.Estado != EstadoAjusteHorariosTurma.Sucesso)
            return new(contexto.Estado);
        var turma = await repositorio.ObterAsync(
            contexto.OrganizacaoId, unidadeId, turmaId, cancellationToken);
        if (turma is null || professorUnidadeInvalido(professor, contexto, turma))
            return new(EstadoAjusteHorariosTurma.TurmaNaoEncontrada);
        return new(EstadoAjusteHorariosTurma.Sucesso, turma,
            MenorVigenciaPermitida: MenorVigencia(turma));
    }

    private async Task<ResultadoAjusteHorariosTurma<Guid>> AjustarAsync(
        Guid usuarioId, Guid unidadeId, Guid turmaId,
        AjustarHorariosTurmaSolicitacao solicitacao, bool professor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(solicitacao);
        var contexto = await ResolverContextoAsync(
            usuarioId, unidadeId, professor, cancellationToken);
        if (contexto.Estado != EstadoAjusteHorariosTurma.Sucesso)
            return new(contexto.Estado);
        var turma = await repositorio.ObterAsync(
            contexto.OrganizacaoId, unidadeId, turmaId, cancellationToken);
        if (turma is null || professorUnidadeInvalido(professor, contexto, turma))
            return new(EstadoAjusteHorariosTurma.TurmaNaoEncontrada);
        var menorVigencia = MenorVigencia(turma);
        if (solicitacao.Horarios is null || solicitacao.Horarios.Count == 0)
            return new(EstadoAjusteHorariosTurma.SemHorarios);
        if (menorVigencia.HasValue
            && solicitacao.NovaVigenciaInicio < menorVigencia.Value)
            return new(EstadoAjusteHorariosTurma.VigenciaInvalida,
                MenorVigenciaPermitida: menorVigencia);

        for (var indice = 0; indice < solicitacao.Horarios.Count; indice++)
        {
            var horario = solicitacao.Horarios[indice];
            if (!Enum.IsDefined(horario.DiaSemana)
                || horario.HoraInicio >= horario.HoraFim)
                return new(EstadoAjusteHorariosTurma.DadosInvalidos);
            if (solicitacao.Horarios.Where((_, outro) => outro != indice).Any(item =>
                    item.DiaSemana == horario.DiaSemana
                    && item.HoraInicio < horario.HoraFim
                    && item.HoraFim > horario.HoraInicio))
                return new(EstadoAjusteHorariosTurma.ConflitoHorario);
            var conflito = await repositorio.ObterConflitoAsync(
                contexto.OrganizacaoId, turma.ProfessorUnidadeId, turmaId,
                solicitacao.NovaVigenciaInicio, horario, cancellationToken);
            if (conflito is not null)
                return new(EstadoAjusteHorariosTurma.ConflitoHorario,
                    Conflito: conflito);
        }

        var estado = await repositorio.AjustarAsync(
            contexto.OrganizacaoId, unidadeId, turmaId, usuarioId, solicitacao,
            timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
        if (estado == EstadoAjusteHorariosTurma.Sucesso)
        {
            logger.LogInformation("AjustarHorários concluído para turma {TurmaId}", turmaId);
        }
        return estado == EstadoAjusteHorariosTurma.Sucesso
            ? new(estado, turmaId)
            : new(estado);
    }

    private async Task<ContextoAjuste> ResolverContextoAsync(
        Guid usuarioId, Guid unidadeId, bool professor,
        CancellationToken cancellationToken)
    {
        if (professor)
        {
            var unidade = await unidadesUsuarioConsulta.ObterProfessorAsync(
                usuarioId, unidadeId, cancellationToken);
            if (unidade is null || !await acessoUsuarioConsulta.PossuiPerfilNaUnidadeAsync(
                    usuarioId, unidade.OrganizacaoId, unidadeId,
                    PerfilAcesso.Professor, cancellationToken))
                return new(EstadoAjusteHorariosTurma.SemAcesso);
            var vinculoId = await minhasTurmasRepositorio.ObterProfessorUnidadeAtivoAsync(
                usuarioId, unidade.OrganizacaoId, unidadeId, cancellationToken);
            return vinculoId is null
                ? new(EstadoAjusteHorariosTurma.SemAcesso)
                : new(EstadoAjusteHorariosTurma.Sucesso,
                    unidade.OrganizacaoId, vinculoId.Value);
        }

        var contexto = await unidadeContextoConsulta.ObterAtivaAsync(
            unidadeId, cancellationToken);
        if (contexto is null) return new(EstadoAjusteHorariosTurma.TurmaNaoEncontrada);
        var governanca = await governancaOperacional.ObterAsync(
            usuarioId, contexto.OrganizacaoId, unidadeId, cancellationToken);
        return governanca.PodeGerenciarTurmas
            ? new(EstadoAjusteHorariosTurma.Sucesso, contexto.OrganizacaoId)
            : new(EstadoAjusteHorariosTurma.SemAcesso);
    }

    private static bool professorUnidadeInvalido(
        bool professor, ContextoAjuste contexto, ProgramacaoTurmaResumo turma) =>
        professor && turma.ProfessorUnidadeId != contexto.ProfessorUnidadeId;

    private static DateOnly? MenorVigencia(ProgramacaoTurmaResumo turma) =>
        turma.HorariosAtuais.Count == 0
            ? null
            : turma.HorariosAtuais.Max(item => item.VigenciaInicio).AddDays(1);

    private sealed record ContextoAjuste(
        EstadoAjusteHorariosTurma Estado,
        Guid OrganizacaoId = default,
        Guid ProfessorUnidadeId = default);
}
