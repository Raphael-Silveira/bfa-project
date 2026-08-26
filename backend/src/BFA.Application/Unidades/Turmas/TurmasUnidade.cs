using BFA.Application.Acessos;
using BFA.Domain.Acessos;
using BFA.Domain.Turmas;

namespace BFA.Application.Unidades.Turmas;

public sealed record TurmaHorarioSolicitacao(
    DiaSemana DiaSemana,
    TimeOnly HoraInicio,
    TimeOnly HoraFim,
    DateOnly VigenciaInicio);

public sealed record CriarTurmaSolicitacao(
    string Nome,
    int Capacidade,
    Guid ProfessorUnidadeId,
    IReadOnlyList<TurmaHorarioSolicitacao> Horarios);

public sealed record AtualizarTurmaSolicitacao(string Nome, int Capacidade);

public sealed record ProfessorTurmaOpcao(Guid ProfessorUnidadeId, string NomeCompleto);

public sealed record TurmaHorarioResumo(
    Guid Id,
    DiaSemana DiaSemana,
    TimeOnly HoraInicio,
    TimeOnly HoraFim,
    DateOnly VigenciaInicio);

public sealed record TurmaResumo(
    Guid Id,
    string Nome,
    string NomeProfessor,
    int Capacidade,
    bool Ativo,
    IReadOnlyList<TurmaHorarioResumo> Horarios);

public sealed record TurmaEdicaoResumo(
    Guid Id,
    string Nome,
    int Capacidade,
    Guid ProfessorUnidadeId,
    string NomeProfessor,
    IReadOnlyList<TurmaHorarioResumo> Horarios);

public sealed record ConflitoHorarioProfessor(
    string NomeProfessor,
    string NomeTurma,
    string NomeUnidade,
    DiaSemana DiaSemana,
    TimeOnly HoraInicio,
    TimeOnly HoraFim);

public enum EstadoTurmasUnidade
{
    Sucesso,
    SemAcesso,
    UnidadeNaoEncontrada,
    TurmaNaoEncontrada,
    ProfessorNaoEncontrado,
    ProfessorInativo,
    SemHorarios,
    DadosInvalidos,
    ConflitoHorario,
    Falha
}

public sealed record ResultadoTurmasUnidade<T>(
    EstadoTurmasUnidade Estado,
    T? Valor = default,
    ConflitoHorarioProfessor? Conflito = null);

public enum EstadoPersistenciaTurma
{
    Sucesso,
    TurmaNaoEncontrada,
    ProfessorNaoEncontrado,
    ProfessorInativo,
    ConflitoHorario,
    DadosInvalidos,
    Falha
}

public interface ITurmasUnidadeRepositorio
{
    Task<IReadOnlyList<TurmaResumo>> ListarAsync(
        Guid organizacaoId, Guid unidadeId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ProfessorTurmaOpcao>> ListarProfessoresAtivosAsync(
        Guid organizacaoId, Guid unidadeId, CancellationToken cancellationToken);

    Task<ProfessorTurmaOpcao?> ObterProfessorAtivoAsync(
        Guid organizacaoId, Guid unidadeId, Guid professorUnidadeId,
        CancellationToken cancellationToken);

    Task<TurmaEdicaoResumo?> ObterEdicaoAsync(
        Guid organizacaoId, Guid unidadeId, Guid turmaId,
        CancellationToken cancellationToken);

    Task<ConflitoHorarioProfessor?> ObterConflitoAsync(
        Guid organizacaoId, Guid professorUnidadeId, TurmaHorarioSolicitacao horario,
        CancellationToken cancellationToken);

    Task<EstadoPersistenciaTurma> CriarAsync(
        Turma turma, IReadOnlyList<TurmaHorario> horarios,
        CancellationToken cancellationToken);

    Task<EstadoPersistenciaTurma> AtualizarAsync(
        Guid organizacaoId, Guid unidadeId, Guid turmaId,
        string nome, int capacidade, Guid usuarioId, DateTime atualizadoEmUtc,
        CancellationToken cancellationToken);
}

public interface ITurmasUnidadeConsulta
{
    Task<ResultadoTurmasUnidade<IReadOnlyList<TurmaResumo>>> ListarAsync(
        Guid usuarioId, Guid unidadeId, CancellationToken cancellationToken);

    Task<ResultadoTurmasUnidade<IReadOnlyList<ProfessorTurmaOpcao>>>
        ListarProfessoresAtivosAsync(
            Guid usuarioId, Guid unidadeId, CancellationToken cancellationToken);

    Task<ResultadoTurmasUnidade<TurmaEdicaoResumo>> ObterEdicaoAsync(
        Guid usuarioId, Guid unidadeId, Guid turmaId,
        CancellationToken cancellationToken);
}

public interface ITurmasUnidadeServico
{
    Task<ResultadoTurmasUnidade<Guid>> CriarAsync(
        Guid usuarioId, Guid unidadeId, CriarTurmaSolicitacao solicitacao,
        CancellationToken cancellationToken);

    Task<ResultadoTurmasUnidade<Guid>> AtualizarAsync(
        Guid usuarioId, Guid unidadeId, Guid turmaId,
        AtualizarTurmaSolicitacao solicitacao, CancellationToken cancellationToken);
}

public sealed class TurmasUnidadeServico(
    IUnidadeContextoConsulta unidadeContextoConsulta,
    IAcessoUsuarioConsulta acessoUsuarioConsulta,
    ITurmasUnidadeRepositorio repositorio,
    TimeProvider timeProvider) : ITurmasUnidadeConsulta, ITurmasUnidadeServico
{
    public async Task<ResultadoTurmasUnidade<IReadOnlyList<TurmaResumo>>> ListarAsync(
        Guid usuarioId, Guid unidadeId, CancellationToken cancellationToken)
    {
        var contexto = await ObterContextoAsync(usuarioId, unidadeId, cancellationToken);
        if (contexto.Estado != EstadoTurmasUnidade.Sucesso) return new(contexto.Estado);
        return new(EstadoTurmasUnidade.Sucesso,
            await repositorio.ListarAsync(
                contexto.Valor!.OrganizacaoId, unidadeId, cancellationToken));
    }

    public async Task<ResultadoTurmasUnidade<IReadOnlyList<ProfessorTurmaOpcao>>>
        ListarProfessoresAtivosAsync(
            Guid usuarioId, Guid unidadeId, CancellationToken cancellationToken)
    {
        var contexto = await ObterContextoAsync(usuarioId, unidadeId, cancellationToken);
        if (contexto.Estado != EstadoTurmasUnidade.Sucesso) return new(contexto.Estado);
        return new(EstadoTurmasUnidade.Sucesso,
            await repositorio.ListarProfessoresAtivosAsync(
                contexto.Valor!.OrganizacaoId, unidadeId, cancellationToken));
    }

    public async Task<ResultadoTurmasUnidade<TurmaEdicaoResumo>> ObterEdicaoAsync(
        Guid usuarioId, Guid unidadeId, Guid turmaId,
        CancellationToken cancellationToken)
    {
        var contexto = await ObterContextoAsync(usuarioId, unidadeId, cancellationToken);
        if (contexto.Estado != EstadoTurmasUnidade.Sucesso) return new(contexto.Estado);
        var turma = await repositorio.ObterEdicaoAsync(
            contexto.Valor!.OrganizacaoId, unidadeId, turmaId, cancellationToken);
        return turma is null
            ? new(EstadoTurmasUnidade.TurmaNaoEncontrada)
            : new(EstadoTurmasUnidade.Sucesso, turma);
    }

    public async Task<ResultadoTurmasUnidade<Guid>> CriarAsync(
        Guid usuarioId, Guid unidadeId, CriarTurmaSolicitacao solicitacao,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(solicitacao);
        var contexto = await ObterContextoAsync(usuarioId, unidadeId, cancellationToken);
        if (contexto.Estado != EstadoTurmasUnidade.Sucesso) return new(contexto.Estado);
        if (solicitacao.Horarios is null || solicitacao.Horarios.Count == 0)
            return new(EstadoTurmasUnidade.SemHorarios);

        var professor = await repositorio.ObterProfessorAtivoAsync(
            contexto.Valor!.OrganizacaoId, unidadeId,
            solicitacao.ProfessorUnidadeId, cancellationToken);
        if (professor is null) return new(EstadoTurmasUnidade.ProfessorNaoEncontrado);

        try
        {
            for (var indice = 0; indice < solicitacao.Horarios.Count; indice++)
            {
                var horario = solicitacao.Horarios[indice];
                ValidarHorario(horario);
                var conflitoInterno = solicitacao.Horarios.Where((_, outroIndice) =>
                        outroIndice != indice).Any(outro =>
                    outro.DiaSemana == horario.DiaSemana
                    && outro.HoraInicio < horario.HoraFim
                    && outro.HoraFim > horario.HoraInicio);
                if (conflitoInterno)
                {
                    return new(EstadoTurmasUnidade.ConflitoHorario, Conflito: new(
                        professor.NomeCompleto, solicitacao.Nome, contexto.Valor.Nome,
                        horario.DiaSemana, horario.HoraInicio, horario.HoraFim));
                }

                var conflito = await repositorio.ObterConflitoAsync(
                    contexto.Valor.OrganizacaoId, solicitacao.ProfessorUnidadeId,
                    horario, cancellationToken);
                if (conflito is not null)
                    return new(EstadoTurmasUnidade.ConflitoHorario, Conflito: conflito);
            }

            var agora = timeProvider.GetUtcNow().UtcDateTime;
            var turmaId = Guid.NewGuid();
            var turma = new Turma(
                turmaId, contexto.Valor.OrganizacaoId, unidadeId,
                solicitacao.ProfessorUnidadeId, solicitacao.Nome,
                solicitacao.Capacidade, usuarioId, agora);
            var horarios = solicitacao.Horarios.Select(item => new TurmaHorario(
                Guid.NewGuid(), contexto.Valor.OrganizacaoId, unidadeId, turmaId,
                solicitacao.ProfessorUnidadeId, item.DiaSemana, item.HoraInicio,
                item.HoraFim, item.VigenciaInicio, null, usuarioId, agora)).ToArray();

            var estado = await repositorio.CriarAsync(turma, horarios, cancellationToken);
            return estado switch
            {
                EstadoPersistenciaTurma.Sucesso =>
                    new(EstadoTurmasUnidade.Sucesso, turmaId),
                EstadoPersistenciaTurma.ConflitoHorario =>
                    new(EstadoTurmasUnidade.ConflitoHorario),
                EstadoPersistenciaTurma.ProfessorInativo or
                    EstadoPersistenciaTurma.ProfessorNaoEncontrado =>
                    new(EstadoTurmasUnidade.ProfessorNaoEncontrado),
                EstadoPersistenciaTurma.DadosInvalidos =>
                    new(EstadoTurmasUnidade.DadosInvalidos),
                _ => new(EstadoTurmasUnidade.Falha)
            };
        }
        catch (ArgumentException)
        {
            return new(EstadoTurmasUnidade.DadosInvalidos);
        }
    }

    public async Task<ResultadoTurmasUnidade<Guid>> AtualizarAsync(
        Guid usuarioId, Guid unidadeId, Guid turmaId,
        AtualizarTurmaSolicitacao solicitacao, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(solicitacao);
        var contexto = await ObterContextoAsync(usuarioId, unidadeId, cancellationToken);
        if (contexto.Estado != EstadoTurmasUnidade.Sucesso) return new(contexto.Estado);
        var estado = await repositorio.AtualizarAsync(
            contexto.Valor!.OrganizacaoId, unidadeId, turmaId,
            solicitacao.Nome, solicitacao.Capacidade, usuarioId,
            timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
        return estado switch
        {
            EstadoPersistenciaTurma.Sucesso => new(EstadoTurmasUnidade.Sucesso, turmaId),
            EstadoPersistenciaTurma.TurmaNaoEncontrada =>
                new(EstadoTurmasUnidade.TurmaNaoEncontrada),
            EstadoPersistenciaTurma.DadosInvalidos =>
                new(EstadoTurmasUnidade.DadosInvalidos),
            _ => new(EstadoTurmasUnidade.Falha)
        };
    }

    private async Task<ResultadoTurmasUnidade<UnidadeContextoResumo>> ObterContextoAsync(
        Guid usuarioId, Guid unidadeId, CancellationToken cancellationToken)
    {
        var contexto = await unidadeContextoConsulta.ObterAtivaAsync(unidadeId, cancellationToken);
        if (contexto is null) return new(EstadoTurmasUnidade.UnidadeNaoEncontrada);
        var administradorUnidade = await acessoUsuarioConsulta.PossuiPerfilNaUnidadeAsync(
            usuarioId, contexto.OrganizacaoId, unidadeId,
            PerfilAcesso.AdministradorUnidade, cancellationToken);
        var autorizado = administradorUnidade ||
            await acessoUsuarioConsulta.EhAdministradorRedeNaOrganizacaoAsync(
                usuarioId, contexto.OrganizacaoId, cancellationToken);
        return autorizado
            ? new(EstadoTurmasUnidade.Sucesso, contexto)
            : new(EstadoTurmasUnidade.SemAcesso);
    }

    private static void ValidarHorario(TurmaHorarioSolicitacao horario)
    {
        if (!Enum.IsDefined(horario.DiaSemana) || horario.VigenciaInicio == default
            || horario.HoraInicio >= horario.HoraFim)
            throw new ArgumentException("O horário recorrente é inválido.", nameof(horario));
    }
}
