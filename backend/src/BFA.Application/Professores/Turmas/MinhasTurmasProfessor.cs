using BFA.Application.Acessos;
using BFA.Application.Unidades;
using BFA.Domain.Acessos;
using BFA.Domain.Turmas;
using Microsoft.Extensions.Logging;

namespace BFA.Application.Professores.Turmas;

public sealed record HorarioTurmaProfessorResumo(
    Guid Id,
    DiaSemana DiaSemana,
    TimeOnly HoraInicio,
    TimeOnly HoraFim,
    DateOnly VigenciaInicio,
    DateOnly? VigenciaFim,
    bool Ativo);

public sealed record TurmaProfessorResumo(
    Guid Id,
    string Nome,
    int Capacidade,
    bool Ativo,
    IReadOnlyList<HorarioTurmaProfessorResumo> HorariosAtuais);

public sealed record TurmaProfessorDetalhe(
    Guid Id,
    string Nome,
    int Capacidade,
    bool Ativo,
    string NomeProfessor,
    IReadOnlyList<HorarioTurmaProfessorResumo> HorariosAtuais,
    IReadOnlyList<HorarioTurmaProfessorResumo> HistoricoHorarios);

public enum EstadoMinhasTurmasProfessor
{
    Sucesso,
    SemAcesso,
    VinculoProfissionalNaoEncontrado,
    TurmaNaoEncontrada
}

public sealed record ResultadoMinhasTurmasProfessor<T>(
    EstadoMinhasTurmasProfessor Estado,
    T? Valor = default);

public interface IMinhasTurmasProfessorRepositorio
{
    Task<Guid?> ObterProfessorUnidadeAtivoAsync(
        Guid usuarioId,
        Guid organizacaoId,
        Guid unidadeId,
        CancellationToken cancellationToken);

    Task<int> ContarAtivasAsync(
        Guid organizacaoId,
        Guid unidadeId,
        Guid professorUnidadeId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<TurmaProfessorResumo>> ListarAsync(
        Guid organizacaoId,
        Guid unidadeId,
        Guid professorUnidadeId,
        DateOnly dataAtual,
        CancellationToken cancellationToken);

    Task<TurmaProfessorDetalhe?> ObterDetalheAsync(
        Guid organizacaoId,
        Guid unidadeId,
        Guid professorUnidadeId,
        Guid turmaId,
        DateOnly dataAtual,
        CancellationToken cancellationToken);
}

public interface IMinhasTurmasProfessorConsulta
{
    Task<ResultadoMinhasTurmasProfessor<int>> ContarAtivasAsync(
        Guid usuarioId,
        Guid unidadeId,
        CancellationToken cancellationToken);

    Task<ResultadoMinhasTurmasProfessor<IReadOnlyList<TurmaProfessorResumo>>> ListarAsync(
        Guid usuarioId,
        Guid unidadeId,
        CancellationToken cancellationToken);

    Task<ResultadoMinhasTurmasProfessor<TurmaProfessorDetalhe>> ObterDetalheAsync(
        Guid usuarioId,
        Guid unidadeId,
        Guid turmaId,
        CancellationToken cancellationToken);
}

public sealed class MinhasTurmasProfessorConsulta(
    IUnidadesUsuarioConsulta unidadesUsuarioConsulta,
    IAcessoUsuarioConsulta acessoUsuarioConsulta,
    IMinhasTurmasProfessorRepositorio repositorio,
    TimeProvider timeProvider,
    ILogger<MinhasTurmasProfessorConsulta> logger) : IMinhasTurmasProfessorConsulta
{
    public async Task<ResultadoMinhasTurmasProfessor<int>> ContarAtivasAsync(
        Guid usuarioId,
        Guid unidadeId,
        CancellationToken cancellationToken)
    {
        var contexto = await ResolverContextoAsync(usuarioId, unidadeId, cancellationToken);
        if (contexto.Estado != EstadoMinhasTurmasProfessor.Sucesso)
            return new(contexto.Estado);
        return new(EstadoMinhasTurmasProfessor.Sucesso,
            await repositorio.ContarAtivasAsync(
                contexto.OrganizacaoId, unidadeId, contexto.ProfessorUnidadeId,
                cancellationToken));
    }

    public async Task<ResultadoMinhasTurmasProfessor<IReadOnlyList<TurmaProfessorResumo>>>
        ListarAsync(
            Guid usuarioId,
            Guid unidadeId,
            CancellationToken cancellationToken)
    {
        var contexto = await ResolverContextoAsync(usuarioId, unidadeId, cancellationToken);
        if (contexto.Estado != EstadoMinhasTurmasProfessor.Sucesso)
            return new(contexto.Estado);
        return new(EstadoMinhasTurmasProfessor.Sucesso,
            await repositorio.ListarAsync(
                contexto.OrganizacaoId, unidadeId, contexto.ProfessorUnidadeId,
                Hoje(), cancellationToken));
    }

    public async Task<ResultadoMinhasTurmasProfessor<TurmaProfessorDetalhe>>
        ObterDetalheAsync(
            Guid usuarioId,
            Guid unidadeId,
            Guid turmaId,
            CancellationToken cancellationToken)
    {
        var contexto = await ResolverContextoAsync(usuarioId, unidadeId, cancellationToken);
        if (contexto.Estado != EstadoMinhasTurmasProfessor.Sucesso)
            return new(contexto.Estado);
        var turma = await repositorio.ObterDetalheAsync(
            contexto.OrganizacaoId, unidadeId, contexto.ProfessorUnidadeId,
            turmaId, Hoje(), cancellationToken);
        return turma is null
            ? new(EstadoMinhasTurmasProfessor.TurmaNaoEncontrada)
            : new(EstadoMinhasTurmasProfessor.Sucesso, turma);
    }

    private async Task<ContextoProfessorTurmas> ResolverContextoAsync(
        Guid usuarioId,
        Guid unidadeId,
        CancellationToken cancellationToken)
    {
        if (usuarioId == Guid.Empty || unidadeId == Guid.Empty)
            return new(EstadoMinhasTurmasProfessor.SemAcesso);

        var unidade = await unidadesUsuarioConsulta.ObterProfessorAsync(
            usuarioId, unidadeId, cancellationToken);
        if (unidade is null)
            return new(EstadoMinhasTurmasProfessor.SemAcesso);

        var possuiAcesso = await acessoUsuarioConsulta.PossuiPerfilNaUnidadeAsync(
            usuarioId, unidade.OrganizacaoId, unidadeId,
            PerfilAcesso.Professor, cancellationToken);
        if (!possuiAcesso)
            return new(EstadoMinhasTurmasProfessor.SemAcesso);

        var professorUnidadeId = await repositorio.ObterProfessorUnidadeAtivoAsync(
            usuarioId, unidade.OrganizacaoId, unidadeId, cancellationToken);
        return professorUnidadeId is null
            ? new(EstadoMinhasTurmasProfessor.VinculoProfissionalNaoEncontrado)
            : new(EstadoMinhasTurmasProfessor.Sucesso,
                unidade.OrganizacaoId, professorUnidadeId.Value);
    }

    private DateOnly Hoje() => DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);

    private sealed record ContextoProfessorTurmas(
        EstadoMinhasTurmasProfessor Estado,
        Guid OrganizacaoId = default,
        Guid ProfessorUnidadeId = default);
}
