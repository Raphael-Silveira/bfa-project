using BFA.Application.Unidades;
using BFA.Domain.Aulas;
using BFA.Domain.Cobrancas;
using BFA.Domain.Matriculas;
using BFA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BFA.Infrastructure.Unidades;

public sealed class UnidadeDashboardConsulta(BfaDbContext dbContext)
    : IUnidadeDashboardConsulta
{
    public async Task<UnidadeDashboardMetricas?> ObterMetricasAsync(
        Guid unidadeId,
        CancellationToken cancellationToken)
    {
        if (unidadeId == Guid.Empty)
        {
            return null;
        }

        var unidade = await dbContext.Unidades
            .AsNoTracking()
            .Where(u => u.Id == unidadeId && u.Ativa)
            .Select(u => new { u.Id })
            .SingleOrDefaultAsync(cancellationToken);

        if (unidade is null)
        {
            return null;
        }

        var totalAlunosAtivos = await dbContext.Matriculas
            .AsNoTracking()
            .CountAsync(m => m.UnidadeId == unidadeId
                && m.Status == StatusMatricula.Ativa, cancellationToken);

        var totalTurmasAtivas = await dbContext.Turmas
            .AsNoTracking()
            .CountAsync(t => t.UnidadeId == unidadeId
                && t.Ativo, cancellationToken);

        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var inicioSemana = hoje.AddDays(-(int)hoje.DayOfWeek);
        var fimSemana = inicioSemana.AddDays(6);

        var totalAulasSemana = await dbContext.Aulas
            .AsNoTracking()
            .CountAsync(a => a.UnidadeId == unidadeId
                && a.Data >= inicioSemana
                && a.Data <= fimSemana
                && a.Status != StatusAula.Cancelada, cancellationToken);

        var chamadasSemana = await dbContext.Presencas
            .AsNoTracking()
            .Where(p => p.UnidadeId == unidadeId)
            .Join(dbContext.Aulas
                .Where(a => a.UnidadeId == unidadeId
                    && a.Data >= inicioSemana
                    && a.Data <= fimSemana
                    && a.Status == StatusAula.Concluida),
                p => p.AulaId,
                a => a.Id,
                (p, a) => p)
            .CountAsync(cancellationToken);

        var presentesSemana = await dbContext.Presencas
            .AsNoTracking()
            .Where(p => p.UnidadeId == unidadeId
                && p.Status == StatusPresenca.Presente)
            .Join(dbContext.Aulas
                .Where(a => a.UnidadeId == unidadeId
                    && a.Data >= inicioSemana
                    && a.Data <= fimSemana
                    && a.Status == StatusAula.Concluida),
                p => p.AulaId,
                a => a.Id,
                (p, a) => p)
            .CountAsync(cancellationToken);

        var percentualFrequencia = chamadasSemana > 0
            ? Math.Round((decimal)presentesSemana / chamadasSemana * 100, 1)
            : 0m;

        var primeiroDiaMes = new DateOnly(hoje.Year, hoje.Month, 1);
        var ultimoDiaMes = primeiroDiaMes.AddMonths(1).AddDays(-1);

        var cobrancasMes = await dbContext.Cobrancas
            .AsNoTracking()
            .Where(c => c.UnidadeId == unidadeId
                && c.DataVencimento >= primeiroDiaMes
                && c.DataVencimento <= ultimoDiaMes)
            .Select(c => new { c.Status, c.Valor, c.ValorPago })
            .ToListAsync(cancellationToken);

        var receitaMes = cobrancasMes
            .Where(c => c.Status == StatusCobranca.Paga)
            .Sum(c => c.ValorPago);

        var pendente = cobrancasMes
            .Where(c => c.Status == StatusCobranca.Pendente)
            .Sum(c => c.Valor - c.ValorPago);

        var emAtraso = cobrancasMes
            .Where(c => c.Status == StatusCobranca.Atrasada)
            .Sum(c => c.Valor - c.ValorPago);

        var aulasHoje = await ObterAulasHojeAsync(unidadeId, hoje, cancellationToken);
        var atividadesRecentes = await ObterAtividadesRecentesAsync(unidadeId, cancellationToken);

        return new UnidadeDashboardMetricas
        {
            TotalAlunosAtivos = totalAlunosAtivos,
            TotalTurmasAtivas = totalTurmasAtivas,
            TotalAulasSemana = totalAulasSemana,
            PercentualFrequencia = percentualFrequencia,
            ReceitaMes = receitaMes,
            Pendente = pendente,
            EmAtraso = emAtraso,
            AulasHoje = aulasHoje,
            AtividadesRecentes = atividadesRecentes
        };
    }

    private async Task<IReadOnlyList<AulaHojeResumo>> ObterAulasHojeAsync(
        Guid unidadeId, DateOnly hoje, CancellationToken cancellationToken)
    {
        var aulas = await dbContext.Aulas
            .AsNoTracking()
            .Where(a => a.UnidadeId == unidadeId
                && a.Data == hoje
                && a.Status != StatusAula.Cancelada)
            .OrderBy(a => a.HoraInicio)
            .Select(a => new
            {
                a.Id,
                a.TurmaId,
                a.HoraInicio,
                a.HoraFim,
                a.Capacidade,
                a.Status
            })
            .ToListAsync(cancellationToken);

        if (aulas.Count == 0)
            return [];

        var turmaIds = aulas.Select(a => a.TurmaId).Distinct().ToList();
        var turmas = await dbContext.Turmas
            .AsNoTracking()
            .Where(t => t.UnidadeId == unidadeId && turmaIds.Contains(t.Id))
            .Select(t => new { t.Id, t.Nome, t.ProfessorUnidadeId })
            .ToDictionaryAsync(t => t.Id, cancellationToken);

        var professorUnidadeIds = turmas.Values
            .Select(t => t.ProfessorUnidadeId)
            .Distinct()
            .ToList();
        var professoresUnidades = await dbContext.ProfessoresUnidades
            .AsNoTracking()
            .Where(pu => pu.UnidadeId == unidadeId
                && professorUnidadeIds.Contains(pu.Id))
            .Select(pu => new { pu.Id, pu.ProfessorId })
            .ToDictionaryAsync(pu => pu.Id, cancellationToken);

        var professorIds = professoresUnidades.Values
            .Select(p => p.ProfessorId)
            .Distinct()
            .ToList();
        var professores = await dbContext.Professores
            .AsNoTracking()
            .Where(p => professorIds.Contains(p.Id))
            .Select(p => new { p.Id, p.NomeCompleto })
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        var aulaIds = aulas.Select(a => a.Id).ToList();
        var inscritosPorAula = (await dbContext.Presencas
            .AsNoTracking()
            .Where(p => aulaIds.Contains(p.AulaId))
            .GroupBy(p => p.AulaId)
            .Select(g => new { AulaId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken))
            .ToDictionary(x => x.AulaId, x => x.Count);

        return aulas.Select(a =>
        {
            var turma = turmas.GetValueOrDefault(a.TurmaId);
            var professorUnidade = turma is not null
                ? professoresUnidades.GetValueOrDefault(turma.ProfessorUnidadeId)
                : null;
            var professor = professorUnidade is not null
                ? professores.GetValueOrDefault(professorUnidade.ProfessorId)
                : null;
            var inscritos = inscritosPorAula.GetValueOrDefault(a.Id);

            return new AulaHojeResumo(
                a.Id,
                $"{a.HoraInicio:HH:mm}",
                turma?.Nome ?? "Turma",
                professor?.NomeCompleto ?? "Professor",
                inscritos,
                a.Capacidade,
                a.Status == StatusAula.Programada ? "Programada" : "Concluída");
        }).ToList();
    }

    private async Task<IReadOnlyList<AtividadeRecente>> ObterAtividadesRecentesAsync(
        Guid unidadeId, CancellationToken cancellationToken)
    {
        var atividades = new List<AtividadeRecente>();

        var matriculas = await dbContext.Matriculas
            .AsNoTracking()
            .Where(m => m.UnidadeId == unidadeId)
            .OrderByDescending(m => m.CriadoEmUtc)
            .Take(3)
            .Select(m => new { m.AlunoId, m.CriadoEmUtc })
            .ToListAsync(cancellationToken);

        if (matriculas.Count > 0)
        {
            var alunoIds = matriculas.Select(m => m.AlunoId).Distinct().ToList();
            var alunos = await dbContext.Alunos
                .AsNoTracking()
                .Where(a => a.OrganizacaoId != Guid.Empty
                    && alunoIds.Contains(a.Id))
                .Select(a => new { a.Id, a.NomeCompleto })
                .ToDictionaryAsync(a => a.Id, cancellationToken);

            foreach (var m in matriculas)
            {
                var nome = alunos.TryGetValue(m.AlunoId, out var aluno)
                    ? aluno.NomeCompleto : "Aluno";
                atividades.Add(new AtividadeRecente(
                    "matricula",
                    "Nova matrícula realizada",
                    nome,
                    FormatTimeRelative(m.CriadoEmUtc),
                    m.CriadoEmUtc));
            }
        }

        var turmas = await dbContext.Turmas
            .AsNoTracking()
            .Where(t => t.UnidadeId == unidadeId)
            .OrderByDescending(t => t.CriadoEmUtc)
            .Take(2)
            .Select(t => new { t.Nome, t.CriadoEmUtc })
            .ToListAsync(cancellationToken);

        foreach (var t in turmas)
        {
            atividades.Add(new AtividadeRecente(
                "turma",
                "Turma cadastrada",
                t.Nome,
                FormatTimeRelative(t.CriadoEmUtc),
                t.CriadoEmUtc));
        }

        var professores = await dbContext.ProfessoresUnidades
            .AsNoTracking()
            .Where(pu => pu.UnidadeId == unidadeId)
            .Join(dbContext.Professores
                .Where(p => p.Ativo),
                pu => pu.ProfessorId,
                p => p.Id,
                (pu, p) => new { p.NomeCompleto, pu.CriadoEmUtc })
            .OrderByDescending(x => x.CriadoEmUtc)
            .Take(2)
            .ToListAsync(cancellationToken);

        foreach (var p in professores)
        {
            atividades.Add(new AtividadeRecente(
                "professor",
                "Professor cadastrado",
                p.NomeCompleto,
                FormatTimeRelative(p.CriadoEmUtc),
                p.CriadoEmUtc));
        }

        return atividades
            .OrderByDescending(a => a.CriadoEmUtc)
            .Take(5)
            .ToList();
    }

    private static string FormatTimeRelative(DateTime utcDateTime)
    {
        var diff = DateTime.UtcNow - utcDateTime;
        if (diff.TotalMinutes < 1) return "agora";
        if (diff.TotalMinutes < 60) return $"há {(int)diff.TotalMinutes} min";
        if (diff.TotalHours < 24) return $"há {(int)diff.TotalHours} hora{((int)diff.TotalHours == 1 ? "" : "s")}";
        if (diff.TotalDays < 7) return $"há {(int)diff.TotalDays} dia{((int)diff.TotalDays == 1 ? "" : "s")}";
        return utcDateTime.ToString("dd/MM");
    }
}
