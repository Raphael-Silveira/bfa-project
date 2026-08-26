using BFA.Application.Professores.Turmas;
using BFA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BFA.Infrastructure.Professores;

public sealed class MinhasTurmasProfessorRepositorio(BfaDbContext dbContext)
    : IMinhasTurmasProfessorRepositorio
{
    public Task<Guid?> ObterProfessorUnidadeAtivoAsync(
        Guid usuarioId,
        Guid organizacaoId,
        Guid unidadeId,
        CancellationToken cancellationToken) =>
        (from professor in dbContext.Professores.AsNoTracking()
         join vinculo in dbContext.ProfessoresUnidades.AsNoTracking()
             on professor.Id equals vinculo.ProfessorId
         where professor.UsuarioId == usuarioId
             && professor.OrganizacaoId == organizacaoId
             && professor.Ativo
             && vinculo.OrganizacaoId == organizacaoId
             && vinculo.UnidadeId == unidadeId
             && vinculo.Ativo
         select (Guid?)vinculo.Id)
        .SingleOrDefaultAsync(cancellationToken);

    public Task<int> ContarAtivasAsync(
        Guid organizacaoId,
        Guid unidadeId,
        Guid professorUnidadeId,
        CancellationToken cancellationToken) =>
        dbContext.Turmas.AsNoTracking().CountAsync(turma =>
            turma.OrganizacaoId == organizacaoId
            && turma.UnidadeId == unidadeId
            && turma.ProfessorUnidadeId == professorUnidadeId
            && turma.Ativo,
            cancellationToken);

    public async Task<IReadOnlyList<TurmaProfessorResumo>> ListarAsync(
        Guid organizacaoId,
        Guid unidadeId,
        Guid professorUnidadeId,
        DateOnly dataAtual,
        CancellationToken cancellationToken)
    {
        var turmas = await dbContext.Turmas.AsNoTracking()
            .Where(turma => turma.OrganizacaoId == organizacaoId
                && turma.UnidadeId == unidadeId
                && turma.ProfessorUnidadeId == professorUnidadeId)
            .OrderBy(turma => turma.Nome)
            .ThenBy(turma => turma.Id)
            .Select(turma => new
            {
                turma.Id,
                turma.Nome,
                turma.Capacidade,
                turma.Ativo
            })
            .ToArrayAsync(cancellationToken);

        var turmaIds = turmas.Select(turma => turma.Id).ToArray();
        var horarios = await ConsultarHorariosAsync(
            organizacaoId, unidadeId, professorUnidadeId, turmaIds,
            cancellationToken);

        return turmas.Select(turma => new TurmaProfessorResumo(
            turma.Id,
            turma.Nome,
            turma.Capacidade,
            turma.Ativo,
            horarios.Where(item => item.TurmaId == turma.Id && EhAtual(item.Horario))
                .Select(item => item.Horario)
                .ToArray())).ToArray();
    }

    public async Task<TurmaProfessorDetalhe?> ObterDetalheAsync(
        Guid organizacaoId,
        Guid unidadeId,
        Guid professorUnidadeId,
        Guid turmaId,
        DateOnly dataAtual,
        CancellationToken cancellationToken)
    {
        var turma = await (
            from item in dbContext.Turmas.AsNoTracking()
            join vinculo in dbContext.ProfessoresUnidades.AsNoTracking()
                on item.ProfessorUnidadeId equals vinculo.Id
            join professor in dbContext.Professores.AsNoTracking()
                on vinculo.ProfessorId equals professor.Id
            where item.Id == turmaId
                && item.OrganizacaoId == organizacaoId
                && item.UnidadeId == unidadeId
                && item.ProfessorUnidadeId == professorUnidadeId
                && vinculo.OrganizacaoId == organizacaoId
                && vinculo.UnidadeId == unidadeId
                && professor.OrganizacaoId == organizacaoId
            select new
            {
                item.Id,
                item.Nome,
                item.Capacidade,
                item.Ativo,
                NomeProfessor = professor.NomeCompleto
            }).SingleOrDefaultAsync(cancellationToken);
        if (turma is null) return null;

        var horarios = await ConsultarHorariosAsync(
            organizacaoId, unidadeId, professorUnidadeId, [turmaId],
            cancellationToken);
        var atuais = horarios.Where(item => EhAtual(item.Horario))
            .Select(item => item.Horario).ToArray();
        var historico = horarios.Where(item => !EhAtual(item.Horario))
            .Select(item => item.Horario).ToArray();
        return new TurmaProfessorDetalhe(
            turma.Id, turma.Nome, turma.Capacidade, turma.Ativo,
            turma.NomeProfessor, atuais, historico);
    }

    private async Task<IReadOnlyList<HorarioComTurma>> ConsultarHorariosAsync(
        Guid organizacaoId,
        Guid unidadeId,
        Guid professorUnidadeId,
        IReadOnlyCollection<Guid> turmaIds,
        CancellationToken cancellationToken) =>
        await dbContext.TurmasHorarios.AsNoTracking()
            .Where(horario => horario.OrganizacaoId == organizacaoId
                && horario.UnidadeId == unidadeId
                && horario.ProfessorUnidadeId == professorUnidadeId
                && turmaIds.Contains(horario.TurmaId))
            .OrderBy(horario => horario.DiaSemana)
            .ThenBy(horario => horario.HoraInicio)
            .ThenByDescending(horario => horario.VigenciaInicio)
            .Select(horario => new HorarioComTurma(
                horario.TurmaId,
                new HorarioTurmaProfessorResumo(
                    horario.Id,
                    horario.DiaSemana,
                    horario.HoraInicio,
                    horario.HoraFim,
                    horario.VigenciaInicio,
                    horario.VigenciaFim,
                    horario.Ativo)))
            .ToArrayAsync(cancellationToken);

    private static bool EhAtual(HorarioTurmaProfessorResumo horario) =>
        horario.Ativo && horario.VigenciaFim is null;

    private sealed record HorarioComTurma(
        Guid TurmaId,
        HorarioTurmaProfessorResumo Horario);
}
