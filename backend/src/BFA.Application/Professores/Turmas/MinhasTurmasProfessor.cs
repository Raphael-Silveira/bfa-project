using BFA.Application.Acessos;
using BFA.Application.Unidades;
using BFA.Domain.Acessos;
using BFA.Domain.Turmas;
using BFA.Domain.Aulas;
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

public sealed record AlunoTurmaProfessorResumo(
    Guid Id,
    string NomeCompleto,
    string? Apelido,
    string? Telefone);

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
    IReadOnlyList<HorarioTurmaProfessorResumo> HistoricoHorarios,
    IReadOnlyList<AlunoTurmaProfessorResumo> Alunos);

public sealed record ProfessorDashboardAlunoResumo(
    Guid AlunoId,
    string Nome,
    DateOnly DataNascimento);

public sealed record ProfessorDashboardDados(
    int QuantidadeTurmas,
    int QuantidadeAulasHoje,
    IReadOnlyList<ProfessorDashboardAlunoResumo> Alunos);

public sealed record ProfessorDashboardAniversario(
    Guid AlunoId,
    string Nome,
    DateOnly Data,
    int DiasAteAniversario);

public static class ProfessorDashboardAniversarios
{
    public static IReadOnlyList<ProfessorDashboardAniversario> Calcular(
        IReadOnlyList<ProfessorDashboardAlunoResumo> alunos,
        DateOnly hoje,
        int janelaDias = 30)
    {
        ArgumentNullException.ThrowIfNull(alunos);
        if (janelaDias < 0) throw new ArgumentOutOfRangeException(nameof(janelaDias));

        var limite = hoje.AddDays(janelaDias);
        return alunos
            .Select(aluno => CriarAniversario(aluno, hoje))
            .Where(aniversario => aniversario.Data >= hoje && aniversario.Data <= limite)
            .OrderBy(aniversario => aniversario.Data)
            .ThenBy(aniversario => aniversario.Nome)
            .ToArray();
    }

    private static ProfessorDashboardAniversario CriarAniversario(
        ProfessorDashboardAlunoResumo aluno,
        DateOnly hoje)
    {
        var data = CriarDataSegura(hoje.Year, aluno.DataNascimento.Month,
            aluno.DataNascimento.Day);
        if (data < hoje)
        {
            data = CriarDataSegura(hoje.Year + 1, aluno.DataNascimento.Month,
                aluno.DataNascimento.Day);
        }

        return new(aluno.AlunoId, aluno.Nome, data, data.DayNumber - hoje.DayNumber);
    }

    private static DateOnly CriarDataSegura(int ano, int mes, int dia)
    {
        if (mes == 2 && dia == 29 && !DateTime.IsLeapYear(ano))
            dia = 28;
        return new DateOnly(ano, mes, dia);
    }
}

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

    Task<ProfessorDashboardDados> ObterDadosDashboardAsync(
        Guid organizacaoId, Guid unidadeId, Guid professorUnidadeId,
        DateOnly dataAtual, CancellationToken cancellationToken);
}

public interface IMinhasTurmasProfessorConsulta
{
    Task<ResultadoMinhasTurmasProfessor<IReadOnlyList<TurmaProfessorResumo>>> ListarAsync(
        Guid usuarioId,
        Guid unidadeId,
        CancellationToken cancellationToken);

    Task<ResultadoMinhasTurmasProfessor<TurmaProfessorDetalhe>> ObterDetalheAsync(
        Guid usuarioId,
        Guid unidadeId,
        Guid turmaId,
        CancellationToken cancellationToken);

    Task<ResultadoMinhasTurmasProfessor<ProfessorDashboardDados>> ObterDadosDashboardAsync(
        Guid usuarioId, Guid unidadeId, CancellationToken cancellationToken);
}

public sealed class MinhasTurmasProfessorConsulta(
    IUnidadesUsuarioConsulta unidadesUsuarioConsulta,
    IAcessoUsuarioConsulta acessoUsuarioConsulta,
    IMinhasTurmasProfessorRepositorio repositorio,
    TimeProvider timeProvider,
    ILogger<MinhasTurmasProfessorConsulta> logger) : IMinhasTurmasProfessorConsulta
{
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

    public async Task<ResultadoMinhasTurmasProfessor<ProfessorDashboardDados>>
        ObterDadosDashboardAsync(Guid usuarioId, Guid unidadeId, CancellationToken cancellationToken)
    {
        var contexto = await ResolverContextoAsync(usuarioId, unidadeId, cancellationToken);
        if (contexto.Estado != EstadoMinhasTurmasProfessor.Sucesso)
            return new(contexto.Estado);

        var dados = await repositorio.ObterDadosDashboardAsync(
            contexto.OrganizacaoId, unidadeId, contexto.ProfessorUnidadeId,
            Hoje(), cancellationToken);
        logger.LogDebug(
            "ProfessorDashboard: resumo carregado para {UsuarioId} na Unidade {UnidadeId}",
            usuarioId, unidadeId);
        return new(EstadoMinhasTurmasProfessor.Sucesso, dados);
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
