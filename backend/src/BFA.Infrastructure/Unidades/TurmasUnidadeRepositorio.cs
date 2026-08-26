using BFA.Application.Unidades.Turmas;
using BFA.Domain.Turmas;
using BFA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BFA.Infrastructure.Unidades;

public sealed class TurmasUnidadeRepositorio(BfaDbContext dbContext)
    : ITurmasUnidadeRepositorio
{
    private const string MensagemConflito =
        "O professor responsavel possui horario recorrente conflitante.";

    public async Task<IReadOnlyList<TurmaResumo>> ListarAsync(
        Guid organizacaoId, Guid unidadeId, CancellationToken cancellationToken)
    {
        var turmas = await (
            from turma in dbContext.Turmas.AsNoTracking()
            join vinculo in dbContext.ProfessoresUnidades.AsNoTracking()
                on turma.ProfessorUnidadeId equals vinculo.Id
            join professor in dbContext.Professores.AsNoTracking()
                on vinculo.ProfessorId equals professor.Id
            where turma.OrganizacaoId == organizacaoId
                && turma.UnidadeId == unidadeId
                && vinculo.OrganizacaoId == organizacaoId
                && professor.OrganizacaoId == organizacaoId
            orderby turma.Nome, turma.Id
            select new
            {
                turma.Id,
                turma.Nome,
                NomeProfessor = professor.NomeCompleto,
                turma.Capacidade,
                turma.Ativo
            }).ToArrayAsync(cancellationToken);

        var ids = turmas.Select(item => item.Id).ToArray();
        var horarios = await dbContext.TurmasHorarios.AsNoTracking()
            .Where(item => item.OrganizacaoId == organizacaoId
                && item.UnidadeId == unidadeId
                && ids.Contains(item.TurmaId)
                && item.Ativo
                && item.VigenciaFim == null)
            .OrderBy(item => item.DiaSemana)
            .ThenBy(item => item.HoraInicio)
            .Select(item => new
            {
                item.TurmaId,
                Resumo = new TurmaHorarioResumo(
                    item.Id, item.DiaSemana, item.HoraInicio, item.HoraFim,
                    item.VigenciaInicio)
            })
            .ToArrayAsync(cancellationToken);

        return turmas.Select(turma => new TurmaResumo(
            turma.Id, turma.Nome, turma.NomeProfessor, turma.Capacidade, turma.Ativo,
            horarios.Where(item => item.TurmaId == turma.Id)
                .Select(item => item.Resumo).ToArray())).ToArray();
    }

    public async Task<IReadOnlyList<ProfessorTurmaOpcao>> ListarProfessoresAtivosAsync(
        Guid organizacaoId, Guid unidadeId, CancellationToken cancellationToken) =>
        await (
            from vinculo in dbContext.ProfessoresUnidades.AsNoTracking()
            join professor in dbContext.Professores.AsNoTracking()
                on vinculo.ProfessorId equals professor.Id
            where vinculo.OrganizacaoId == organizacaoId
                && vinculo.UnidadeId == unidadeId
                && vinculo.Ativo
                && professor.OrganizacaoId == organizacaoId
                && professor.Ativo
            orderby professor.NomeCompleto, vinculo.Id
            select new ProfessorTurmaOpcao(vinculo.Id, professor.NomeCompleto))
            .ToArrayAsync(cancellationToken);

    public async Task<ProfessorTurmaOpcao?> ObterProfessorAtivoAsync(
        Guid organizacaoId, Guid unidadeId, Guid professorUnidadeId,
        CancellationToken cancellationToken) =>
        await (
            from vinculo in dbContext.ProfessoresUnidades.AsNoTracking()
            join professor in dbContext.Professores.AsNoTracking()
                on vinculo.ProfessorId equals professor.Id
            where vinculo.Id == professorUnidadeId
                && vinculo.OrganizacaoId == organizacaoId
                && vinculo.UnidadeId == unidadeId
                && vinculo.Ativo
                && professor.OrganizacaoId == organizacaoId
                && professor.Ativo
            select new ProfessorTurmaOpcao(vinculo.Id, professor.NomeCompleto))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<TurmaEdicaoResumo?> ObterEdicaoAsync(
        Guid organizacaoId, Guid unidadeId, Guid turmaId,
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
                && vinculo.OrganizacaoId == organizacaoId
                && professor.OrganizacaoId == organizacaoId
            select new
            {
                item.Id,
                item.Nome,
                item.Capacidade,
                item.ProfessorUnidadeId,
                NomeProfessor = professor.NomeCompleto
            }).SingleOrDefaultAsync(cancellationToken);
        if (turma is null) return null;

        var horarios = await dbContext.TurmasHorarios.AsNoTracking()
            .Where(item => item.OrganizacaoId == organizacaoId
                && item.UnidadeId == unidadeId
                && item.TurmaId == turmaId
                && item.Ativo
                && item.VigenciaFim == null)
            .OrderBy(item => item.DiaSemana)
            .ThenBy(item => item.HoraInicio)
            .Select(item => new TurmaHorarioResumo(
                item.Id, item.DiaSemana, item.HoraInicio, item.HoraFim,
                item.VigenciaInicio))
            .ToArrayAsync(cancellationToken);
        return new(turma.Id, turma.Nome, turma.Capacidade,
            turma.ProfessorUnidadeId, turma.NomeProfessor, horarios);
    }

    public async Task<ConflitoHorarioProfessor?> ObterConflitoAsync(
        Guid organizacaoId, Guid professorUnidadeId, TurmaHorarioSolicitacao horario,
        CancellationToken cancellationToken)
    {
        var professor = await dbContext.ProfessoresUnidades.AsNoTracking()
            .Where(item => item.Id == professorUnidadeId
                && item.OrganizacaoId == organizacaoId)
            .Select(item => new { item.ProfessorId })
            .SingleOrDefaultAsync(cancellationToken);
        if (professor is null) return null;

        return await (
            from existente in dbContext.TurmasHorarios.AsNoTracking()
            join vinculo in dbContext.ProfessoresUnidades.AsNoTracking()
                on existente.ProfessorUnidadeId equals vinculo.Id
            join docente in dbContext.Professores.AsNoTracking()
                on vinculo.ProfessorId equals docente.Id
            join turma in dbContext.Turmas.AsNoTracking()
                on existente.TurmaId equals turma.Id
            join unidade in dbContext.Unidades.AsNoTracking()
                on existente.UnidadeId equals unidade.Id
            where existente.OrganizacaoId == organizacaoId
                && vinculo.OrganizacaoId == organizacaoId
                && vinculo.ProfessorId == professor.ProfessorId
                && existente.Ativo
                && turma.Ativo
                && existente.DiaSemana == horario.DiaSemana
                && existente.HoraInicio < horario.HoraFim
                && existente.HoraFim > horario.HoraInicio
                && (existente.VigenciaFim == null
                    || existente.VigenciaFim >= horario.VigenciaInicio)
            orderby existente.HoraInicio, existente.Id
            select new ConflitoHorarioProfessor(
                docente.NomeCompleto, turma.Nome, unidade.Nome,
                existente.DiaSemana, existente.HoraInicio, existente.HoraFim))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<EstadoPersistenciaTurma> CriarAsync(
        Turma turma, IReadOnlyList<TurmaHorario> horarios,
        CancellationToken cancellationToken)
    {
        await using var transacao = await dbContext.Database.BeginTransactionAsync(
            cancellationToken);
        try
        {
            var professor = await ObterProfessorAtivoAsync(
                turma.OrganizacaoId, turma.UnidadeId, turma.ProfessorUnidadeId,
                cancellationToken);
            if (professor is null)
                return EstadoPersistenciaTurma.ProfessorNaoEncontrado;

            foreach (var horario in horarios)
            {
                var conflito = await ObterConflitoAsync(
                    turma.OrganizacaoId, turma.ProfessorUnidadeId,
                    new(horario.DiaSemana, horario.HoraInicio,
                        horario.HoraFim, horario.VigenciaInicio),
                    cancellationToken);
                if (conflito is not null)
                    return EstadoPersistenciaTurma.ConflitoHorario;
            }

            dbContext.Turmas.Add(turma);
            dbContext.TurmasHorarios.AddRange(horarios);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transacao.CommitAsync(cancellationToken);
            return EstadoPersistenciaTurma.Sucesso;
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException postgres
            && postgres.MessageText.Contains(MensagemConflito,
                StringComparison.OrdinalIgnoreCase))
        {
            await transacao.RollbackAsync(cancellationToken);
            return EstadoPersistenciaTurma.ConflitoHorario;
        }
        catch (DbUpdateException)
        {
            await transacao.RollbackAsync(cancellationToken);
            return EstadoPersistenciaTurma.Falha;
        }
    }

    public async Task<EstadoPersistenciaTurma> AtualizarAsync(
        Guid organizacaoId, Guid unidadeId, Guid turmaId,
        string nome, int capacidade, Guid usuarioId, DateTime atualizadoEmUtc,
        CancellationToken cancellationToken)
    {
        var turma = await dbContext.Turmas.SingleOrDefaultAsync(item =>
            item.Id == turmaId && item.OrganizacaoId == organizacaoId
            && item.UnidadeId == unidadeId, cancellationToken);
        if (turma is null) return EstadoPersistenciaTurma.TurmaNaoEncontrada;
        try
        {
            turma.Atualizar(nome, capacidade, turma.ProfessorUnidadeId,
                usuarioId, atualizadoEmUtc);
            await dbContext.SaveChangesAsync(cancellationToken);
            return EstadoPersistenciaTurma.Sucesso;
        }
        catch (ArgumentException)
        {
            return EstadoPersistenciaTurma.DadosInvalidos;
        }
    }
}
