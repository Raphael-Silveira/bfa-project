using BFA.Application.Professores.Confirmacoes;
using BFA.Domain.Matriculas;
using BFA.Domain.Aulas;
using BFA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace BFA.Infrastructure.Professores;

public sealed class ConfirmacoesParticipacaoProfessorRepositorio(BfaDbContext dbContext)
    : IConfirmacoesParticipacaoProfessorRepositorio
{
    public async Task<AulasProfessorPagina> ListarAgendaAsync(
        Guid organizacaoId, Guid unidadeId, Guid professorUnidadeId,
        DateOnly? dataInicial, DateOnly? dataFinal, StatusAula? status,
        bool incluirTodas, int pagina, int tamanhoPagina, CancellationToken cancellationToken)
    {
        var query = from aula in dbContext.Aulas.AsNoTracking()
                    join turma in dbContext.Turmas.AsNoTracking() on aula.TurmaId equals turma.Id
                    where aula.OrganizacaoId == organizacaoId && aula.UnidadeId == unidadeId
                        && turma.OrganizacaoId == organizacaoId && turma.UnidadeId == unidadeId
                        && turma.ProfessorUnidadeId == professorUnidadeId
                    select new { aula, turma };
        if (dataInicial is { } inicio) query = query.Where(item => item.aula.Data >= inicio);
        if (dataFinal is { } fim) query = query.Where(item => item.aula.Data <= fim);
        if (!incluirTodas && status is { } filtroStatus) query = query.Where(item => item.aula.Status == filtroStatus);

        var totalItens = await query.CountAsync(cancellationToken);
        var totalPaginas = Math.Max(1, (int)Math.Ceiling(totalItens / (double)tamanhoPagina));
        var paginaSegura = Math.Min(Math.Max(1, pagina), totalPaginas);
        var aulas = await query
            .OrderBy(item => item.aula.Data).ThenBy(item => item.aula.HoraInicio)
            .Skip((paginaSegura - 1) * tamanhoPagina).Take(tamanhoPagina)
            .Select(item => new AulaConfirmacoesProfessorResumo(
                item.aula.Id, item.turma.Id, item.turma.Nome, item.aula.Data, item.aula.HoraInicio,
                item.aula.HoraFim, item.aula.Status, item.aula.MotivoCancelamento,
                item.aula.CanceladaEmUtc, item.aula.CanceladaPorUsuarioId))
            .ToArrayAsync(cancellationToken);
        return new(aulas, paginaSegura, totalPaginas, totalItens);
    }

    public async Task<AulaConfirmacoesProfessorDetalhe?> ObterAsync(
        Guid organizacaoId, Guid unidadeId, Guid professorUnidadeId, Guid turmaId,
        Guid aulaId, CancellationToken cancellationToken)
    {
        var aula = await (from item in dbContext.Aulas.AsNoTracking()
                          join turma in dbContext.Turmas.AsNoTracking() on item.TurmaId equals turma.Id
                          where item.Id == aulaId && item.TurmaId == turmaId
                              && item.OrganizacaoId == organizacaoId && item.UnidadeId == unidadeId
                              && turma.OrganizacaoId == organizacaoId && turma.UnidadeId == unidadeId
                              && turma.ProfessorUnidadeId == professorUnidadeId
                          select new AulaConfirmacoesProfessorResumo(
                               item.Id, turma.Id, turma.Nome, item.Data, item.HoraInicio, item.HoraFim, item.Status,
                               item.MotivoCancelamento, item.CanceladaEmUtc, item.CanceladaPorUsuarioId))
            .SingleOrDefaultAsync(cancellationToken);
        if (aula is null) return null;

        var alunoIds = await (from horario in dbContext.MatriculasHorarios.AsNoTracking()
                              join matricula in dbContext.Matriculas.AsNoTracking()
                                  on horario.MatriculaId equals matricula.Id
                              where horario.OrganizacaoId == organizacaoId && horario.UnidadeId == unidadeId
                                  && horario.TurmaHorarioId == (from item in dbContext.Aulas
                                      where item.Id == aulaId select item.TurmaHorarioId).First()
                                  && horario.VigenciaFim == null
                                  && matricula.OrganizacaoId == organizacaoId && matricula.UnidadeId == unidadeId
                                  && matricula.Status == StatusMatricula.Ativa
                                  && matricula.DataInicio <= aula.Data
                                  && matricula.DataFimPrevista >= aula.Data
                              select matricula.AlunoId).Distinct().ToArrayAsync(cancellationToken);

        var alunos = await dbContext.Alunos.AsNoTracking()
            .Where(item => alunoIds.Contains(item.Id) && item.OrganizacaoId == organizacaoId)
            .Select(item => new { item.Id, item.NomeCompleto })
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        var confirmados = await dbContext.ConfirmacoesAulaAluno.AsNoTracking()
            .Where(item => item.OrganizacaoId == organizacaoId && item.UnidadeId == unidadeId
                && item.AulaId == aulaId && item.Ativa)
            .Select(item => item.AlunoId).ToHashSetAsync(cancellationToken);
        var presencas = await dbContext.Presencas.AsNoTracking()
            .Where(item => item.OrganizacaoId == organizacaoId
                && item.UnidadeId == unidadeId && item.AulaId == aulaId)
            .ToDictionaryAsync(item => item.AlunoId, cancellationToken);

        var itens = alunoIds.Where(alunos.ContainsKey)
            .Select(id => new AlunoConfirmacaoProfessorResumo(id, alunos[id].NomeCompleto,
                confirmados.Contains(id), presencas.TryGetValue(id, out var presenca)
                    ? presenca.Status : null))
            .OrderBy(item => item.NomeCompleto)
            .ToArray();
        return new(aula, itens.Length, itens.Count(item => item.Confirmou),
            itens.Count(item => item.Presenca == BFA.Domain.Aulas.StatusPresenca.Presente),
            itens.Count(item => item.Presenca == BFA.Domain.Aulas.StatusPresenca.Ausente),
            itens.Count(item => item.Presenca is null), itens);
    }

    public async Task<EstadoConfirmacoesProfessor> RegistrarChamadaAsync(
        Guid organizacaoId, Guid unidadeId, Guid professorUnidadeId, Guid turmaId,
        Guid aulaId, IReadOnlyList<RegistroChamadaProfessor> registros,
        Guid usuarioId, DateTime agoraUtc, CancellationToken cancellationToken)
    {
        await using var transacao = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        var aula = await (from item in dbContext.Aulas
                          join turma in dbContext.Turmas on item.TurmaId equals turma.Id
                          where item.Id == aulaId && item.TurmaId == turmaId
                              && item.OrganizacaoId == organizacaoId && item.UnidadeId == unidadeId
                              && turma.OrganizacaoId == organizacaoId && turma.UnidadeId == unidadeId
                              && turma.ProfessorUnidadeId == professorUnidadeId
                          select item).SingleOrDefaultAsync(cancellationToken);
        if (aula is null) return EstadoConfirmacoesProfessor.AulaNaoEncontrada;
        if (aula.Status == BFA.Domain.Aulas.StatusAula.Cancelada)
            return EstadoConfirmacoesProfessor.AulaCancelada;

        var elegiveis = await ObterMatriculasElegiveisAsync(
            organizacaoId, unidadeId, aula, cancellationToken);
        var permitidos = elegiveis.ToDictionary(item => item.AlunoId, item => item.MatriculaId);
        foreach (var registro in registros.DistinctBy(item => item.AlunoId))
        {
            if (!permitidos.TryGetValue(registro.AlunoId, out var matriculaId)) continue;
            var existente = await dbContext.Presencas.FirstOrDefaultAsync(item =>
                item.OrganizacaoId == organizacaoId && item.AulaId == aulaId
                && item.AlunoId == registro.AlunoId, cancellationToken);
            if (existente is not null)
                existente.Registrar(registro.Status, null, agoraUtc);
            else
                dbContext.Presencas.Add(new BFA.Domain.Aulas.Presenca(
                    Guid.NewGuid(), organizacaoId, unidadeId, aulaId, registro.AlunoId,
                    matriculaId, registro.Status, usuarioId, agoraUtc));
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        await transacao.CommitAsync(cancellationToken);
        return EstadoConfirmacoesProfessor.Sucesso;
    }

    public async Task<EstadoConfirmacoesProfessor> CancelarAsync(
        Guid organizacaoId, Guid unidadeId, Guid professorUnidadeId, Guid turmaId,
        Guid aulaId, string motivo, Guid usuarioId, DateTime agoraUtc,
        CancellationToken cancellationToken)
    {
        await using var transacao = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        var aula = await (from item in dbContext.Aulas
                          join turma in dbContext.Turmas on item.TurmaId equals turma.Id
                          where item.Id == aulaId && item.TurmaId == turmaId
                              && item.OrganizacaoId == organizacaoId && item.UnidadeId == unidadeId
                              && turma.OrganizacaoId == organizacaoId && turma.UnidadeId == unidadeId
                              && turma.ProfessorUnidadeId == professorUnidadeId
                          select item).SingleOrDefaultAsync(cancellationToken);
        if (aula is null) return EstadoConfirmacoesProfessor.AulaNaoEncontrada;
        if (aula.Status == BFA.Domain.Aulas.StatusAula.Cancelada)
            return EstadoConfirmacoesProfessor.AulaCancelada;
        if (aula.Status != BFA.Domain.Aulas.StatusAula.Programada)
            return EstadoConfirmacoesProfessor.DadosInvalidos;
        if (await dbContext.Presencas.AnyAsync(item =>
                item.OrganizacaoId == organizacaoId && item.UnidadeId == unidadeId
                && item.AulaId == aulaId, cancellationToken))
            return EstadoConfirmacoesProfessor.ChamadaExistente;

        aula.Cancelar(usuarioId, agoraUtc, motivo);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transacao.CommitAsync(cancellationToken);
        return EstadoConfirmacoesProfessor.Sucesso;
    }

    private async Task<IReadOnlyList<(Guid AlunoId, Guid MatriculaId)>> ObterMatriculasElegiveisAsync(
        Guid organizacaoId, Guid unidadeId, BFA.Domain.Aulas.Aula aula,
        CancellationToken cancellationToken)
    {
        return await (from horario in dbContext.MatriculasHorarios.AsNoTracking()
                      join matricula in dbContext.Matriculas.AsNoTracking()
                          on horario.MatriculaId equals matricula.Id
                      where horario.OrganizacaoId == organizacaoId && horario.UnidadeId == unidadeId
                          && horario.TurmaHorarioId == aula.TurmaHorarioId
                          && horario.VigenciaFim == null
                          && matricula.OrganizacaoId == organizacaoId
                          && matricula.UnidadeId == unidadeId
                          && matricula.Status == StatusMatricula.Ativa
                          && matricula.DataInicio <= aula.Data
                          && matricula.DataFimPrevista >= aula.Data
                      select new ValueTuple<Guid, Guid>(matricula.AlunoId, matricula.Id))
            .Distinct().ToArrayAsync(cancellationToken);
    }
}
