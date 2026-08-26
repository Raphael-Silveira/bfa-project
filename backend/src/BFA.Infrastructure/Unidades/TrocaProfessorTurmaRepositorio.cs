using BFA.Application.Unidades.Turmas;
using BFA.Domain.Turmas;
using BFA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BFA.Infrastructure.Unidades;

public sealed class TrocaProfessorTurmaRepositorio(
    BfaDbContext dbContext,
    IAjusteHorariosTurmaRepositorio ajusteHorariosRepositorio)
    : ITrocaProfessorTurmaRepositorio
{
    private const string MensagemConflito =
        "O professor responsavel possui horario recorrente conflitante.";

    public async Task<TrocaProfessorTurmaResumo?> ObterAsync(
        Guid organizacaoId, Guid unidadeId, Guid turmaId,
        CancellationToken cancellationToken)
    {
        var turma = await (
            from item in dbContext.Turmas.AsNoTracking()
            join vinculo in dbContext.ProfessoresUnidades.AsNoTracking()
                on item.ProfessorUnidadeId equals vinculo.Id
            join professor in dbContext.Professores.AsNoTracking()
                on vinculo.ProfessorId equals professor.Id
            where item.Id == turmaId && item.OrganizacaoId == organizacaoId
                && item.UnidadeId == unidadeId && item.Ativo
                && vinculo.OrganizacaoId == organizacaoId
                && vinculo.UnidadeId == unidadeId
                && professor.OrganizacaoId == organizacaoId
            select new
            {
                item.Id, item.Nome,
                ProfessorUnidadeAtualId = vinculo.Id,
                NomeProfessorAtual = professor.NomeCompleto
            }).SingleOrDefaultAsync(cancellationToken);
        if (turma is null) return null;

        var horarios = await dbContext.TurmasHorarios.AsNoTracking()
            .Where(item => item.OrganizacaoId == organizacaoId
                && item.UnidadeId == unidadeId && item.TurmaId == turmaId
                && item.ProfessorUnidadeId == turma.ProfessorUnidadeAtualId
                && item.Ativo && item.VigenciaFim == null)
            .OrderBy(item => item.DiaSemana).ThenBy(item => item.HoraInicio)
            .Select(item => new TurmaHorarioResumo(
                item.Id, item.DiaSemana, item.HoraInicio, item.HoraFim,
                item.VigenciaInicio))
            .ToArrayAsync(cancellationToken);
        var professores = await (
            from vinculo in dbContext.ProfessoresUnidades.AsNoTracking()
            join professor in dbContext.Professores.AsNoTracking()
                on vinculo.ProfessorId equals professor.Id
            where vinculo.OrganizacaoId == organizacaoId
                && vinculo.UnidadeId == unidadeId && vinculo.Ativo
                && vinculo.Id != turma.ProfessorUnidadeAtualId
                && professor.OrganizacaoId == organizacaoId && professor.Ativo
            orderby professor.NomeCompleto, vinculo.Id
            select new ProfessorTrocaOpcao(vinculo.Id, professor.NomeCompleto))
            .ToArrayAsync(cancellationToken);
        return new(turma.Id, turma.Nome, turma.ProfessorUnidadeAtualId,
            turma.NomeProfessorAtual, horarios, professores);
    }

    public async Task<EstadoTrocaProfessorTurma> TrocarAsync(
        Guid organizacaoId, Guid unidadeId, Guid turmaId,
        Guid novoProfessorUnidadeId, DateOnly dataTroca,
        Guid usuarioId, DateTime atualizadoEmUtc,
        CancellationToken cancellationToken)
    {
        await using var transacao = await dbContext.Database.BeginTransactionAsync(
            cancellationToken);
        try
        {
            var turma = await dbContext.Turmas.SingleOrDefaultAsync(item =>
                item.Id == turmaId && item.OrganizacaoId == organizacaoId
                && item.UnidadeId == unidadeId && item.Ativo, cancellationToken);
            if (turma is null) return EstadoTrocaProfessorTurma.TurmaNaoEncontrada;
            if (turma.ProfessorUnidadeId == novoProfessorUnidadeId)
                return EstadoTrocaProfessorTurma.MesmoProfessor;
            var novoProfessor = await dbContext.ProfessoresUnidades.AsNoTracking()
                .Where(item => item.Id == novoProfessorUnidadeId
                    && item.OrganizacaoId == organizacaoId
                    && item.UnidadeId == unidadeId && item.Ativo)
                .Join(dbContext.Professores.AsNoTracking().Where(item =>
                        item.OrganizacaoId == organizacaoId && item.Ativo),
                    vinculo => vinculo.ProfessorId, professor => professor.Id,
                    (vinculo, professor) => vinculo.Id)
                .SingleOrDefaultAsync(cancellationToken);
            if (novoProfessor == Guid.Empty)
                return EstadoTrocaProfessorTurma.ProfessorNaoEncontrado;

            var atuais = await dbContext.TurmasHorarios
                .Where(item => item.OrganizacaoId == organizacaoId
                    && item.UnidadeId == unidadeId && item.TurmaId == turmaId
                    && item.ProfessorUnidadeId == turma.ProfessorUnidadeId
                    && item.Ativo && item.VigenciaFim == null)
                .OrderBy(item => item.DiaSemana).ThenBy(item => item.HoraInicio)
                .ToArrayAsync(cancellationToken);
            if (atuais.Any(item => dataTroca <= item.VigenciaInicio))
                return EstadoTrocaProfessorTurma.VigenciaInvalida;
            foreach (var horario in atuais)
            {
                var conflito = await ajusteHorariosRepositorio.ObterConflitoAsync(
                    organizacaoId, novoProfessorUnidadeId, turmaId, dataTroca,
                    new(horario.DiaSemana, horario.HoraInicio, horario.HoraFim),
                    cancellationToken);
                if (conflito is not null) return EstadoTrocaProfessorTurma.ConflitoHorario;
            }

            if (atuais.Length > 0)
            {
                var fim = dataTroca.AddDays(-1);
                foreach (var horario in atuais)
                    horario.Encerrar(fim, usuarioId, atualizadoEmUtc);
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            turma.Atualizar(turma.Nome, turma.Capacidade, novoProfessorUnidadeId,
                usuarioId, atualizadoEmUtc);
            await dbContext.SaveChangesAsync(cancellationToken);

            if (atuais.Length > 0)
            {
                var novos = atuais.Select(item => new TurmaHorario(
                    Guid.NewGuid(), organizacaoId, unidadeId, turmaId,
                    novoProfessorUnidadeId, item.DiaSemana, item.HoraInicio,
                    item.HoraFim, dataTroca, null, usuarioId, atualizadoEmUtc));
                dbContext.TurmasHorarios.AddRange(novos);
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            await transacao.CommitAsync(cancellationToken);
            return EstadoTrocaProfessorTurma.Sucesso;
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException postgres
            && postgres.SqlState == PostgresErrorCodes.CheckViolation
            && postgres.MessageText.Contains(MensagemConflito,
                StringComparison.OrdinalIgnoreCase))
        {
            await transacao.RollbackAsync(cancellationToken);
            return EstadoTrocaProfessorTurma.ConflitoHorario;
        }
        catch (DbUpdateException)
        {
            await transacao.RollbackAsync(cancellationToken);
            return EstadoTrocaProfessorTurma.Falha;
        }
    }
}
