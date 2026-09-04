using BFA.Application.Relatorios;
using BFA.Domain.Aulas;
using BFA.Domain.Cobrancas;
using BFA.Domain.Matriculas;
using BFA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BFA.Infrastructure.Relatorios;

public sealed class RelatoriosRepositorio(BfaDbContext dbContext, ILogger<RelatoriosRepositorio> logger) : IRelatoriosRepositorio
{
    public async Task<ResumoGeralRelatorios> ObterResumoGeralAsync(
        Guid organizacaoId, Guid unidadeId,
        CancellationToken cancellationToken)
    {
        var hoje = DateOnly.FromDateTime(DateTime.Today);

        var totalAlunos = await ContarAlunosAtivosAsync(organizacaoId, unidadeId, cancellationToken);
        var totalAulas = await ContarAulasConcluidasAsync(organizacaoId, unidadeId, hoje, cancellationToken);

        var cobrancas = await ListarCobrancasAsync(organizacaoId, unidadeId, null, null, cancellationToken);

        return new ResumoGeralRelatorios(
            totalAlunos,
            totalAlunos,
            totalAulas,
            cobrancas.Count(c => c.Status == StatusCobranca.Pendente),
            cobrancas.Count(c => c.Status == StatusCobranca.Atrasada),
            cobrancas.Where(c => c.Status == StatusCobranca.Paga).Sum(c => c.ValorPago),
            cobrancas.Where(c => c.Status == StatusCobranca.Pendente).Sum(c => c.Valor - c.ValorPago),
            cobrancas.Where(c => c.Status == StatusCobranca.Atrasada).Sum(c => c.Valor - c.ValorPago));
    }

    public async Task<IReadOnlyList<CobrancaRelatorio>> ListarCobrancasAsync(
        Guid organizacaoId, Guid unidadeId,
        DateOnly? dataInicio, DateOnly? dataFim,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Cobrancas.AsNoTracking()
            .Where(c => c.OrganizacaoId == organizacaoId && c.UnidadeId == unidadeId);

        if (dataInicio.HasValue)
            query = query.Where(c => c.DataEmissao >= dataInicio.Value);

        if (dataFim.HasValue)
            query = query.Where(c => c.DataEmissao <= dataFim.Value);

        return await query.Select(c => new CobrancaRelatorio(
            c.Tipo,
            c.Status,
            c.Valor,
            c.ValorPago,
            c.DataEmissao,
            c.DataVencimento,
            c.AlunoId)).ToListAsync(cancellationToken);
    }

    public async Task<int> ContarAlunosAtivosAsync(
        Guid organizacaoId, Guid unidadeId,
        CancellationToken cancellationToken)
    {
        return await dbContext.Matriculas.AsNoTracking()
            .CountAsync(m => m.OrganizacaoId == organizacaoId
                          && m.UnidadeId == unidadeId
                          && m.Status == StatusMatricula.Ativa, cancellationToken);
    }

    public async Task<int> ContarAulasConcluidasAsync(
        Guid organizacaoId, Guid unidadeId, DateOnly ate,
        CancellationToken cancellationToken)
    {
        return await dbContext.Aulas.AsNoTracking()
            .CountAsync(a => a.OrganizacaoId == organizacaoId
                          && a.UnidadeId == unidadeId
                          && a.Status == StatusAula.Concluida
                          && a.Data <= ate, cancellationToken);
    }
}
