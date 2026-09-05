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

        return new UnidadeDashboardMetricas
        {
            TotalAlunosAtivos = totalAlunosAtivos,
            TotalTurmasAtivas = totalTurmasAtivas,
            TotalAulasSemana = totalAulasSemana,
            PercentualFrequencia = percentualFrequencia,
            ReceitaMes = receitaMes,
            Pendente = pendente,
            EmAtraso = emAtraso
        };
    }
}
