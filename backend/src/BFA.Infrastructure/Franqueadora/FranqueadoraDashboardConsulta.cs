using BFA.Application.Franqueadora;
using BFA.Domain.Acessos;
using BFA.Domain.Cobrancas;
using BFA.Domain.Matriculas;
using BFA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BFA.Infrastructure.Franqueadora;

public sealed class FranqueadoraDashboardConsulta(BfaDbContext dbContext)
    : IFranqueadoraDashboardConsulta
{
    public async Task<FranqueadoraDashboardResultado> ObterAsync(
        Guid usuarioId,
        CancellationToken cancellationToken)
    {
        if (usuarioId == Guid.Empty)
        {
            return FranqueadoraDashboardResultado.SemAcesso();
        }

        var organizacoesAdministradas = await dbContext.VinculosAcesso
            .AsNoTracking()
            .Where(vinculo => vinculo.UsuarioId == usuarioId
                && vinculo.Ativo
                && vinculo.Perfil == PerfilAcesso.AdministradorRede
                && vinculo.UnidadeId == null)
            .Select(vinculo => vinculo.OrganizacaoId)
            .Distinct()
            .Take(2)
            .ToArrayAsync(cancellationToken);

        if (organizacoesAdministradas.Length == 0)
        {
            return FranqueadoraDashboardResultado.SemAcesso();
        }

        if (organizacoesAdministradas.Length > 1)
        {
            return FranqueadoraDashboardResultado.SelecaoOrganizacaoNecessaria();
        }

        var organizacaoId = organizacoesAdministradas[0];
        var organizacao = await dbContext.Organizacoes
            .AsNoTracking()
            .Where(item => item.Id == organizacaoId)
            .Select(item => new { item.Id, item.Nome })
            .SingleOrDefaultAsync(cancellationToken);

        if (organizacao is null)
        {
            return FranqueadoraDashboardResultado.SemAcesso();
        }

        var unidades = await dbContext.Unidades
            .AsNoTracking()
            .Where(u => u.OrganizacaoId == organizacaoId)
            .Select(u => new { u.Id, u.Nome, u.Ativa })
            .ToListAsync(cancellationToken);

        var unidadeIds = unidades.Select(u => u.Id).ToList();

        var totalUnidades = unidades.Count;
        var unidadesAtivas = unidades.Count(u => u.Ativa);

        var matriculasPorUnidade = await dbContext.Matriculas
            .AsNoTracking()
            .Where(m => m.OrganizacaoId == organizacaoId
                     && m.Status == StatusMatricula.Ativa)
            .GroupBy(m => m.UnidadeId)
            .Select(g => new { UnidadeId = g.Key, Total = g.Count() })
            .ToListAsync(cancellationToken);

        var totalAlunosAtivos = await dbContext.Alunos
            .AsNoTracking()
            .CountAsync(a => a.OrganizacaoId == organizacaoId && a.Ativo, cancellationToken);

        var totalMatriculasAtivas = matriculasPorUnidade.Sum(m => m.Total);

        var totalProfessores = await dbContext.Professores
            .AsNoTracking()
            .CountAsync(p => p.OrganizacaoId == organizacaoId, cancellationToken);

        var cobrancas = await dbContext.Cobrancas
            .AsNoTracking()
            .Where(c => c.OrganizacaoId == organizacaoId)
            .Select(c => new { c.Status, c.Valor, c.ValorPago })
            .ToListAsync(cancellationToken);

        var totalReceita = cobrancas
            .Where(c => c.Status == StatusCobranca.Paga)
            .Sum(c => c.ValorPago);
        var totalPendente = cobrancas
            .Where(c => c.Status == StatusCobranca.Pendente)
            .Sum(c => c.Valor - c.ValorPago);
        var totalAtrasado = cobrancas
            .Where(c => c.Status == StatusCobranca.Atrasada)
            .Sum(c => c.Valor - c.ValorPago);

        var unidadesResumo = unidades.Select(u => new UnidadeResumoRede(
            u.Id,
            u.Nome,
            matriculasPorUnidade.FirstOrDefault(m => m.UnidadeId == u.Id)?.Total ?? 0,
            matriculasPorUnidade.FirstOrDefault(m => m.UnidadeId == u.Id)?.Total ?? 0,
            u.Ativa)).ToList();

        var resumo = new FranqueadoraDashboardResumo(
            organizacao.Id,
            organizacao.Nome,
            totalUnidades,
            unidadesAtivas,
            totalAlunosAtivos,
            totalMatriculasAtivas,
            totalProfessores,
            totalReceita,
            totalPendente,
            totalAtrasado,
            unidadesResumo);

        return FranqueadoraDashboardResultado.Disponivel(resumo);
    }
}
