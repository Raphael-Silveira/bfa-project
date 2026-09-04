using BFA.Application.Aulas;
using BFA.Domain.Aulas;
using BFA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BFA.Infrastructure.Aulas;

public sealed class AulasRepositorio(BfaDbContext dbContext, ILogger<AulasRepositorio> logger) : IAulasRepositorio
{
    public async Task<IReadOnlyList<AulaResumo>> ListarAsync(
        Guid organizacaoId, Guid unidadeId,
        DateOnly dataInicio, DateOnly dataFim,
        CancellationToken cancellationToken)
    {
        var aulas = await dbContext.Aulas.AsNoTracking()
            .Where(a => a.OrganizacaoId == organizacaoId
                     && a.UnidadeId == unidadeId
                     && a.Data >= dataInicio
                     && a.Data <= dataFim)
            .OrderBy(a => a.Data)
            .ThenBy(a => a.HoraInicio)
            .ToListAsync(cancellationToken);

        if (aulas.Count == 0)
            return [];

        var turmaIds = aulas.Select(a => a.TurmaId).Distinct().ToList();

        var turmas = await dbContext.Turmas.AsNoTracking()
            .Where(t => turmaIds.Contains(t.Id) && t.OrganizacaoId == organizacaoId)
            .ToDictionaryAsync(t => t.Id, cancellationToken);

        var professorUnidadeIds = turmas.Values
            .Select(t => t.ProfessorUnidadeId).Distinct().ToList();

        var professoresVinculos = await dbContext.ProfessoresUnidades.AsNoTracking()
            .Where(pu => professorUnidadeIds.Contains(pu.Id)
                      && pu.OrganizacaoId == organizacaoId)
            .ToDictionaryAsync(pu => pu.Id, cancellationToken);

        var professorIds = professoresVinculos.Values
            .Select(pv => pv.ProfessorId).Distinct().ToList();

        var professores = await dbContext.Professores.AsNoTracking()
            .Where(p => professorIds.Contains(p.Id) && p.OrganizacaoId == organizacaoId)
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        var matriculasHorarios = await dbContext.MatriculasHorarios.AsNoTracking()
            .Where(mh => mh.OrganizacaoId == organizacaoId
                      && mh.UnidadeId == unidadeId
                      && mh.VigenciaFim == null)
            .ToListAsync(cancellationToken);

        var turmaHorarioIds = aulas.Select(a => a.TurmaHorarioId).Distinct().ToList();
        var inscritosPorHorario = matriculasHorarios
            .Where(mh => turmaHorarioIds.Contains(mh.TurmaHorarioId))
            .GroupBy(mh => mh.TurmaHorarioId)
            .ToDictionary(g => g.Key, g => g.Count());

        var resultado = new List<AulaResumo>(aulas.Count);

        foreach (var aula in aulas)
        {
            turmas.TryGetValue(aula.TurmaId, out var turma);
            var turmaNome = turma?.Nome ?? string.Empty;

            string professorNome = string.Empty;
            if (turma is not null
                && professoresVinculos.TryGetValue(turma.ProfessorUnidadeId, out var vinculo)
                && professores.TryGetValue(vinculo.ProfessorId, out var professor))
            {
                professorNome = professor.NomeCompleto;
            }

            inscritosPorHorario.TryGetValue(aula.TurmaHorarioId, out var inscritos);

            resultado.Add(new AulaResumo(
                aula.Id,
                turmaNome,
                professorNome,
                aula.Data,
                aula.HoraInicio,
                aula.HoraFim,
                aula.Status,
                aula.Capacidade,
                inscritos));
        }

        return resultado;
    }

    public async Task<AulaDetalhe?> ObterAsync(
        Guid organizacaoId, Guid unidadeId, Guid aulaId,
        CancellationToken cancellationToken)
    {
        var aula = await dbContext.Aulas.AsNoTracking()
            .FirstOrDefaultAsync(
                a => a.Id == aulaId
                  && a.OrganizacaoId == organizacaoId
                  && a.UnidadeId == unidadeId,
                cancellationToken);

        if (aula is null)
            return null;

        var turma = await dbContext.Turmas.AsNoTracking()
            .FirstOrDefaultAsync(
                t => t.Id == aula.TurmaId
                  && t.OrganizacaoId == organizacaoId,
                cancellationToken);

        string professorNome = string.Empty;
        if (turma is not null)
        {
            var vinculo = await dbContext.ProfessoresUnidades.AsNoTracking()
                .FirstOrDefaultAsync(
                    pu => pu.Id == turma.ProfessorUnidadeId
                       && pu.OrganizacaoId == organizacaoId,
                    cancellationToken);

            if (vinculo is not null)
            {
                var professor = await dbContext.Professores.AsNoTracking()
                    .FirstOrDefaultAsync(
                        p => p.Id == vinculo.ProfessorId
                          && p.OrganizacaoId == organizacaoId,
                        cancellationToken);

                professorNome = professor?.NomeCompleto ?? string.Empty;
            }
        }

        var presencas = await dbContext.Presencas.AsNoTracking()
            .Where(p => p.OrganizacaoId == organizacaoId
                     && p.AulaId == aulaId)
            .ToListAsync(cancellationToken);

        var alunoIdsPresencas = presencas.Select(p => p.AlunoId).Distinct().ToList();

        var alunosComPresenca = await dbContext.Alunos.AsNoTracking()
            .Where(a => alunoIdsPresencas.Contains(a.Id)
                     && a.OrganizacaoId == organizacaoId)
            .ToDictionaryAsync(a => a.Id, cancellationToken);

        var alunos = presencas.Select(p =>
        {
            alunosComPresenca.TryGetValue(p.AlunoId, out var aluno);
            return new AlunoPresencaResumo(
                p.AlunoId,
                aluno?.NomeCompleto ?? string.Empty,
                p.Status,
                p.ChegouAs,
                p.SaiuAs);
        }).ToList();

        return new AulaDetalhe(
            aula.Id,
            aula.TurmaId,
            turma?.Nome ?? string.Empty,
            professorNome,
            aula.Data,
            aula.HoraInicio,
            aula.HoraFim,
            aula.Status,
            aula.Capacidade,
            aula.Observacoes,
            alunos);
    }

    public async Task<bool> ExisteAulaNoHorarioAsync(
        Guid organizacaoId, Guid turmaId,
        DateOnly data, TimeOnly horaInicio,
        CancellationToken cancellationToken)
    {
        return await dbContext.Aulas.AsNoTracking()
            .AnyAsync(a =>
                a.OrganizacaoId == organizacaoId
                && (turmaId == Guid.Empty || a.TurmaId == turmaId)
                && a.Data == data
                && a.HoraInicio == horaInicio,
                cancellationToken);
    }

    public async Task<bool> CriarAsync(
        Aula aula, CancellationToken cancellationToken)
    {
        try
        {
            dbContext.Aulas.Add(aula);
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Erro ao criar aula {AulaId}", aula.Id);
            return false;
        }
    }

    public async Task<bool> AtualizarAsync(
        Aula aula, CancellationToken cancellationToken)
    {
        try
        {
            var existente = await dbContext.Aulas
                .FirstOrDefaultAsync(
                    a => a.Id == aula.Id
                      && a.OrganizacaoId == aula.OrganizacaoId,
                    cancellationToken);

            if (existente is null)
                return false;

            dbContext.Entry(existente).CurrentValues.SetValues(aula);
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Erro ao atualizar aula {AulaId}", aula.Id);
            return false;
        }
    }

    public async Task<IReadOnlyList<AlunoPresencaResumo>> ListarAlunosParaChamadaAsync(
        Guid organizacaoId, Guid unidadeId, Guid aulaId,
        CancellationToken cancellationToken)
    {
        var aula = await dbContext.Aulas.AsNoTracking()
            .FirstOrDefaultAsync(
                a => a.Id == aulaId
                  && a.OrganizacaoId == organizacaoId
                  && a.UnidadeId == unidadeId,
                cancellationToken);

        if (aula is null)
            return [];

        var matriculaHorarios = await dbContext.MatriculasHorarios.AsNoTracking()
            .Where(mh => mh.OrganizacaoId == organizacaoId
                      && mh.UnidadeId == unidadeId
                      && mh.TurmaHorarioId == aula.TurmaHorarioId
                      && mh.VigenciaFim == null)
            .ToListAsync(cancellationToken);

        var matriculasIds = matriculaHorarios.Select(mh => mh.MatriculaId).Distinct().ToList();

        var matriculas = await dbContext.Matriculas.AsNoTracking()
            .Where(m => matriculasIds.Contains(m.Id)
                     && m.OrganizacaoId == organizacaoId
                     && m.UnidadeId == unidadeId)
            .ToListAsync(cancellationToken);

        var alunoIds = matriculas.Select(m => m.AlunoId).Distinct().ToList();

        var alunos = await dbContext.Alunos.AsNoTracking()
            .Where(a => alunoIds.Contains(a.Id) && a.OrganizacaoId == organizacaoId)
            .ToDictionaryAsync(a => a.Id, cancellationToken);

        var presencasExistentes = await dbContext.Presencas.AsNoTracking()
            .Where(p => p.OrganizacaoId == organizacaoId && p.AulaId == aulaId)
            .ToDictionaryAsync(p => p.AlunoId, cancellationToken);

        var resultado = new List<AlunoPresencaResumo>(alunoIds.Count);

        foreach (var alunoId in alunoIds)
        {
            alunos.TryGetValue(alunoId, out var aluno);
            presencasExistentes.TryGetValue(alunoId, out var presencaExistente);

            resultado.Add(new AlunoPresencaResumo(
                alunoId,
                aluno?.NomeCompleto ?? string.Empty,
                presencaExistente?.Status,
                presencaExistente?.ChegouAs,
                presencaExistente?.SaiuAs));
        }

        return resultado.OrderBy(a => a.NomeCompleto).ToList();
    }

    public async Task<Presenca?> ObterPresencaAsync(
        Guid organizacaoId, Guid aulaId, Guid alunoId,
        CancellationToken cancellationToken)
    {
        return await dbContext.Presencas.AsNoTracking()
            .FirstOrDefaultAsync(
                p => p.OrganizacaoId == organizacaoId
                  && p.AulaId == aulaId
                  && p.AlunoId == alunoId,
                cancellationToken);
    }

    public async Task<bool> RegistrarPresencaAsync(
        Presenca presenca, CancellationToken cancellationToken)
    {
        try
        {
            var existente = await dbContext.Presencas
                .FirstOrDefaultAsync(
                    p => p.OrganizacaoId == presenca.OrganizacaoId
                      && p.AulaId == presenca.AulaId
                      && p.AlunoId == presenca.AlunoId,
                    cancellationToken);

            if (existente is not null)
            {
                existente.Registrar(presenca.Status, presenca.Observacoes, presenca.AtualizadoEmUtc);
                if (presenca.ChegouAs is not null || presenca.SaiuAs is not null)
                {
                    existente.RegistrarHorarios(presenca.ChegouAs, presenca.SaiuAs, presenca.AtualizadoEmUtc);
                }
            }
            else
            {
                dbContext.Presencas.Add(presenca);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Erro ao registrar presença: aula {AulaId} aluno {AlunoId}",
                presenca.AulaId, presenca.AlunoId);
            return false;
        }
    }

    public async Task<bool> RegistrarPresencasEmLoteAsync(
        IReadOnlyList<Presenca> presencas, CancellationToken cancellationToken)
    {
        try
        {
            foreach (var presenca in presencas)
            {
                var existente = await dbContext.Presencas
                    .FirstOrDefaultAsync(
                        p => p.OrganizacaoId == presenca.OrganizacaoId
                          && p.AulaId == presenca.AulaId
                          && p.AlunoId == presenca.AlunoId,
                        cancellationToken);

                if (existente is not null)
                {
                    existente.Registrar(presenca.Status, presenca.Observacoes, presenca.AtualizadoEmUtc);
                    if (presenca.ChegouAs is not null || presenca.SaiuAs is not null)
                    {
                        existente.RegistrarHorarios(presenca.ChegouAs, presenca.SaiuAs, presenca.AtualizadoEmUtc);
                    }
                }
                else
                {
                    dbContext.Presencas.Add(presenca);
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Erro ao registrar presenças em lote");
            return false;
        }
    }

    public async Task<IReadOnlyList<FrequenciaAlunoResumo>> ObterFrequenciaAsync(
        Guid organizacaoId, Guid unidadeId, Guid? turmaId,
        DateOnly dataInicio, DateOnly dataFim,
        CancellationToken cancellationToken)
    {
        var queryAulas = dbContext.Aulas.AsNoTracking()
            .Where(a => a.OrganizacaoId == organizacaoId
                     && a.UnidadeId == unidadeId
                     && a.Data >= dataInicio
                     && a.Data <= dataFim
                     && a.Status != StatusAula.Cancelada);

        if (turmaId.HasValue)
        {
            queryAulas = queryAulas.Where(a => a.TurmaId == turmaId.Value);
        }

        var aulas = await queryAulas.ToListAsync(cancellationToken);

        if (aulas.Count == 0)
            return [];

        var aulaIds = aulas.Select(a => a.Id).ToList();

        var presencas = await dbContext.Presencas.AsNoTracking()
            .Where(p => aulaIds.Contains(p.AulaId) && p.OrganizacaoId == organizacaoId)
            .ToListAsync(cancellationToken);

        var alunoIds = presencas.Select(p => p.AlunoId).Distinct().ToList();

        var alunos = await dbContext.Alunos.AsNoTracking()
            .Where(a => alunoIds.Contains(a.Id) && a.OrganizacaoId == organizacaoId)
            .ToDictionaryAsync(a => a.Id, cancellationToken);

        var totalAulasPorAluno = presencas
            .GroupBy(p => p.AlunoId)
            .ToDictionary(
                g => g.Key,
                g => new
                {
                    Total = aulas.Count,
                    Presentes = g.Count(p => p.Status == StatusPresenca.Presente),
                    Ausentes = g.Count(p => p.Status == StatusPresenca.Ausente),
                    Justificados = g.Count(p => p.Status == StatusPresenca.Justificado),
                    Isentos = g.Count(p => p.Status == StatusPresenca.Isento)
                });

        var resultado = new List<FrequenciaAlunoResumo>();

        foreach (var (alunoId, stats) in totalAulasPorAluno)
        {
            alunos.TryGetValue(alunoId, out var aluno);
            var frequencia = stats.Total > 0
                ? Math.Round((decimal)stats.Presentes / stats.Total * 100, 1)
                : 0m;

            resultado.Add(new FrequenciaAlunoResumo(
                alunoId,
                aluno?.NomeCompleto ?? string.Empty,
                stats.Total,
                stats.Presentes,
                stats.Ausentes,
                stats.Justificados,
                stats.Isentos,
                frequencia));
        }

        return resultado.OrderBy(f => f.NomeCompleto).ToList();
    }
}
