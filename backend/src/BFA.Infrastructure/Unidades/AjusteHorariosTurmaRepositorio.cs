using BFA.Application.Unidades.Turmas;
using BFA.Domain.Turmas;
using BFA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BFA.Infrastructure.Unidades;

public sealed class AjusteHorariosTurmaRepositorio(BfaDbContext dbContext)
    : IAjusteHorariosTurmaRepositorio
{
    private const string MensagemConflito =
        "O professor responsavel possui horario recorrente conflitante.";

    public async Task<ProgramacaoTurmaResumo?> ObterAsync(
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
                && item.Ativo
                && vinculo.OrganizacaoId == organizacaoId
                && vinculo.UnidadeId == unidadeId
                && vinculo.Ativo
                && professor.OrganizacaoId == organizacaoId
                && professor.Ativo
            select new
            {
                item.Id,
                item.Nome,
                item.ProfessorUnidadeId,
                NomeProfessor = professor.NomeCompleto
            }).SingleOrDefaultAsync(cancellationToken);
        if (turma is null) return null;

        var horarios = await dbContext.TurmasHorarios.AsNoTracking()
            .Where(item => item.OrganizacaoId == organizacaoId
                && item.UnidadeId == unidadeId
                && item.TurmaId == turmaId
                && item.ProfessorUnidadeId == turma.ProfessorUnidadeId
                && item.Ativo
                && item.VigenciaFim == null)
            .OrderBy(item => item.DiaSemana)
            .ThenBy(item => item.HoraInicio)
            .Select(item => new TurmaHorarioResumo(
                item.Id, item.DiaSemana, item.HoraInicio, item.HoraFim,
                item.VigenciaInicio))
            .ToArrayAsync(cancellationToken);
        return new(turma.Id, turma.Nome, turma.ProfessorUnidadeId,
            turma.NomeProfessor, horarios);
    }

    public async Task<ConflitoHorarioProfessor?> ObterConflitoAsync(
        Guid organizacaoId, Guid professorUnidadeId, Guid turmaId,
        DateOnly novaVigenciaInicio, NovoHorarioTurmaSolicitacao horario,
        CancellationToken cancellationToken)
    {
        var professorId = await dbContext.ProfessoresUnidades.AsNoTracking()
            .Where(item => item.Id == professorUnidadeId
                && item.OrganizacaoId == organizacaoId)
            .Select(item => (Guid?)item.ProfessorId)
            .SingleOrDefaultAsync(cancellationToken);
        if (professorId is null) return null;

        return await (
            from existente in dbContext.TurmasHorarios.AsNoTracking()
            join vinculo in dbContext.ProfessoresUnidades.AsNoTracking()
                on existente.ProfessorUnidadeId equals vinculo.Id
            join professor in dbContext.Professores.AsNoTracking()
                on vinculo.ProfessorId equals professor.Id
            join turma in dbContext.Turmas.AsNoTracking()
                on existente.TurmaId equals turma.Id
            join unidade in dbContext.Unidades.AsNoTracking()
                on existente.UnidadeId equals unidade.Id
            where existente.OrganizacaoId == organizacaoId
                && existente.TurmaId != turmaId
                && vinculo.OrganizacaoId == organizacaoId
                && vinculo.ProfessorId == professorId.Value
                && existente.Ativo
                && turma.Ativo
                && existente.DiaSemana == horario.DiaSemana
                && existente.HoraInicio < horario.HoraFim
                && existente.HoraFim > horario.HoraInicio
                && (existente.VigenciaFim == null
                    || existente.VigenciaFim >= novaVigenciaInicio)
            orderby existente.HoraInicio, existente.Id
            select new ConflitoHorarioProfessor(
                professor.NomeCompleto, turma.Nome, unidade.Nome,
                existente.DiaSemana, existente.HoraInicio, existente.HoraFim))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<EstadoAjusteHorariosTurma> AjustarAsync(
        Guid organizacaoId, Guid unidadeId, Guid turmaId, Guid usuarioId,
        AjustarHorariosTurmaSolicitacao solicitacao, DateTime atualizadoEmUtc,
        CancellationToken cancellationToken)
    {
        await using var transacao = await dbContext.Database.BeginTransactionAsync(
            cancellationToken);
        try
        {
            var turma = await dbContext.Turmas.SingleOrDefaultAsync(item =>
                item.Id == turmaId && item.OrganizacaoId == organizacaoId
                && item.UnidadeId == unidadeId && item.Ativo, cancellationToken);
            if (turma is null) return EstadoAjusteHorariosTurma.TurmaNaoEncontrada;

            var atuais = await dbContext.TurmasHorarios
                .Where(item => item.OrganizacaoId == organizacaoId
                    && item.UnidadeId == unidadeId
                    && item.TurmaId == turmaId
                    && item.ProfessorUnidadeId == turma.ProfessorUnidadeId
                    && item.Ativo
                    && item.VigenciaFim == null)
                .ToArrayAsync(cancellationToken);

            var solicitados = solicitacao.Horarios
                .Select(item => new IdentidadeMaterialHorario(
                    item.DiaSemana, item.HoraInicio, item.HoraFim,
                    turma.ProfessorUnidadeId))
                .ToHashSet();
            var afetados = atuais
                .Where(item => !solicitados.Contains(Identidade(item)))
                .OrderBy(item => item.Id)
                .ToArray();
            var identidadesAtuais = atuais.Select(Identidade).ToHashSet();
            var novosSolicitados = solicitacao.Horarios
                .Where(item => !identidadesAtuais.Contains(new(
                    item.DiaSemana, item.HoraInicio, item.HoraFim,
                    turma.ProfessorUnidadeId)))
                .ToArray();

            if (afetados.Any(item =>
                    solicitacao.NovaVigenciaInicio <= item.VigenciaInicio))
                return EstadoAjusteHorariosTurma.VigenciaInvalida;

            foreach (var horario in solicitacao.Horarios)
            {
                if (await ObterConflitoAsync(
                        organizacaoId, turma.ProfessorUnidadeId, turmaId,
                        solicitacao.NovaVigenciaInicio, horario,
                        cancellationToken) is not null)
                    return EstadoAjusteHorariosTurma.ConflitoHorario;
            }

            var vigenciaFim = solicitacao.NovaVigenciaInicio.AddDays(-1);
            await GradeLoteLocks.BloquearTurmasHorariosAsync(
                dbContext, organizacaoId, unidadeId,
                afetados.Select(item => item.Id), cancellationToken);

            var idsAfetados = afetados.Select(item => item.Id).ToArray();
            var existeGradeAfetada = idsAfetados.Length > 0
                && await dbContext.MatriculasHorarios.AsNoTracking().AnyAsync(item =>
                    item.OrganizacaoId == organizacaoId
                    && item.UnidadeId == unidadeId
                    && idsAfetados.Contains(item.TurmaHorarioId)
                    && (item.VigenciaFim == null || item.VigenciaFim > vigenciaFim),
                    cancellationToken);
            if (existeGradeAfetada)
                return EstadoAjusteHorariosTurma.ExisteGradeAfetada;

            foreach (var atual in afetados)
                atual.Encerrar(vigenciaFim, usuarioId, atualizadoEmUtc);

            if (afetados.Length > 0)
                await dbContext.SaveChangesAsync(cancellationToken);

            var novos = novosSolicitados.Select(item => new TurmaHorario(
                Guid.NewGuid(), organizacaoId, unidadeId, turmaId,
                turma.ProfessorUnidadeId, item.DiaSemana, item.HoraInicio,
                item.HoraFim, solicitacao.NovaVigenciaInicio, null,
                usuarioId, atualizadoEmUtc));
            dbContext.TurmasHorarios.AddRange(novos);
            if (novosSolicitados.Length > 0)
                await dbContext.SaveChangesAsync(cancellationToken);
            await transacao.CommitAsync(cancellationToken);
            return EstadoAjusteHorariosTurma.Sucesso;
        }
        catch (ArgumentException)
        {
            await transacao.RollbackAsync(cancellationToken);
            return EstadoAjusteHorariosTurma.DadosInvalidos;
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException postgres
            && postgres.SqlState == PostgresErrorCodes.CheckViolation
            && postgres.MessageText.Contains(
                MensagemConflito, StringComparison.OrdinalIgnoreCase))
        {
            await transacao.RollbackAsync(cancellationToken);
            return EstadoAjusteHorariosTurma.ConflitoHorario;
        }
        catch (DbUpdateException)
        {
            await transacao.RollbackAsync(cancellationToken);
            return EstadoAjusteHorariosTurma.Falha;
        }
    }

    private static IdentidadeMaterialHorario Identidade(TurmaHorario horario) => new(
        horario.DiaSemana, horario.HoraInicio, horario.HoraFim,
        horario.ProfessorUnidadeId);

    private sealed record IdentidadeMaterialHorario(
        DiaSemana DiaSemana,
        TimeOnly HoraInicio,
        TimeOnly HoraFim,
        Guid ProfessorUnidadeId);
}
