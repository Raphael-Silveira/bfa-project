using BFA.Application.Cobrancas;
using BFA.Domain.Cobrancas;
using BFA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BFA.Infrastructure.Cobrancas;

public sealed class CobrancasRepositorio(BfaDbContext dbContext, ILogger<CobrancasRepositorio> logger) : ICobrancasRepositorio
{
    public async Task<IReadOnlyList<CobrancaListaItem>> ListarAsync(
        Guid organizacaoId, Guid unidadeId, FiltroCobrancas filtro,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Cobrancas.AsNoTracking()
            .Where(c => c.OrganizacaoId == organizacaoId && c.UnidadeId == unidadeId);

        if (filtro.AlunoId.HasValue)
            query = query.Where(c => c.AlunoId == filtro.AlunoId.Value);

        if (filtro.Status.HasValue)
            query = query.Where(c => c.Status == filtro.Status.Value);

        if (filtro.Tipo.HasValue)
            query = query.Where(c => c.Tipo == filtro.Tipo.Value);

        if (filtro.DataVencimentoInicio.HasValue)
            query = query.Where(c => c.DataVencimento >= filtro.DataVencimentoInicio.Value);

        if (filtro.DataVencimentoFim.HasValue)
            query = query.Where(c => c.DataVencimento <= filtro.DataVencimentoFim.Value);

        var cobrancas = await query
            .OrderByDescending(c => c.DataVencimento)
            .ThenByDescending(c => c.CriadoEmUtc)
            .ToListAsync(cancellationToken);

        if (cobrancas.Count == 0)
            return [];

        var alunoIds = cobrancas.Select(c => c.AlunoId).Distinct().ToList();
        var alunos = await dbContext.Alunos.AsNoTracking()
            .Where(a => a.OrganizacaoId == organizacaoId && alunoIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, cancellationToken);

        return cobrancas.Select(c => new CobrancaListaItem(
            c.Id,
            c.AlunoId,
            alunos.TryGetValue(c.AlunoId, out var aluno) ? aluno.NomeCompleto : "Aluno nao encontrado",
            c.Descricao,
            c.Tipo,
            c.Valor,
            c.ValorPago,
            c.DataVencimento,
            c.Status)).ToList();
    }

    public async Task<CobrancaDetalhe?> ObterAsync(
        Guid organizacaoId, Guid unidadeId, Guid cobrancaId,
        CancellationToken cancellationToken)
    {
        var cobranca = await dbContext.Cobrancas.AsNoTracking()
            .FirstOrDefaultAsync(c => c.OrganizacaoId == organizacaoId
                                   && c.UnidadeId == unidadeId
                                   && c.Id == cobrancaId, cancellationToken);

        if (cobranca is null)
            return null;

        var aluno = await dbContext.Alunos.AsNoTracking()
            .FirstOrDefaultAsync(a => a.OrganizacaoId == organizacaoId
                                   && a.Id == cobranca.AlunoId, cancellationToken);

        var pagamentos = await dbContext.Pagamentos.AsNoTracking()
            .Where(p => p.OrganizacaoId == organizacaoId
                     && p.UnidadeId == unidadeId
                     && p.CobrancaId == cobrancaId)
            .OrderByDescending(p => p.DataPagamento)
            .ToListAsync(cancellationToken);

        return new CobrancaDetalhe(
            cobranca.Id,
            cobranca.AlunoId,
            aluno?.NomeCompleto ?? "Aluno nao encontrado",
            aluno?.Cpf,
            cobranca.Descricao,
            cobranca.Tipo,
            cobranca.Valor,
            cobranca.ValorPago,
            cobranca.Valor - cobranca.ValorPago,
            cobranca.DataEmissao,
            cobranca.DataVencimento,
            cobranca.DataPagamento,
            cobranca.Status,
            cobranca.Observacoes,
            pagamentos.Select(p => new PagamentoResumo(
                p.Id,
                p.Valor,
                p.DataPagamento,
                p.FormaPagamento,
                p.Observacoes)).ToList());
    }

    public async Task<Cobranca?> ObterPorIdAsync(
        Guid organizacaoId, Guid unidadeId, Guid cobrancaId,
        CancellationToken cancellationToken)
    {
        return await dbContext.Cobrancas
            .FirstOrDefaultAsync(c => c.OrganizacaoId == organizacaoId
                                   && c.UnidadeId == unidadeId
                                   && c.Id == cobrancaId, cancellationToken);
    }

    public async Task<bool> CriarAsync(Cobranca cobranca, CancellationToken cancellationToken)
    {
        dbContext.Cobrancas.Add(cobranca);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> CancelarAsync(Cobranca cobranca, CancellationToken cancellationToken)
    {
        dbContext.Cobrancas.Update(cobranca);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<Pagamento?> ObterPagamentoAsync(
        Guid organizacaoId, Guid cobrancaId, Guid pagamentoId,
        CancellationToken cancellationToken)
    {
        return await dbContext.Pagamentos
            .FirstOrDefaultAsync(p => p.OrganizacaoId == organizacaoId
                                   && p.CobrancaId == cobrancaId
                                   && p.Id == pagamentoId, cancellationToken);
    }

    public async Task<bool> RegistrarPagamentoAsync(Pagamento pagamento, CancellationToken cancellationToken)
    {
        dbContext.Pagamentos.Add(pagamento);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<AlunoParaSelecao>> ListarAlunosAsync(
        Guid organizacaoId, Guid unidadeId,
        CancellationToken cancellationToken)
    {
        var matriculas = await dbContext.Matriculas.AsNoTracking()
            .Where(m => m.OrganizacaoId == organizacaoId
                     && m.UnidadeId == unidadeId
                     && m.Status == Domain.Matriculas.StatusMatricula.Ativa)
            .ToListAsync(cancellationToken);

        if (matriculas.Count == 0)
            return [];

        var alunoIds = matriculas.Select(m => m.AlunoId).Distinct().ToList();
        var alunos = await dbContext.Alunos.AsNoTracking()
            .Where(a => a.OrganizacaoId == organizacaoId && alunoIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, cancellationToken);

        return matriculas
            .Where(m => alunos.ContainsKey(m.AlunoId))
            .Select(m => new AlunoParaSelecao(
                m.AlunoId,
                alunos[m.AlunoId].NomeCompleto,
                alunos[m.AlunoId].Cpf,
                m.Id))
            .ToList();
    }

    public async Task<ResumoFinanceiro> ObterResumoAsync(
        Guid organizacaoId, Guid unidadeId,
        CancellationToken cancellationToken)
    {
        var cobrancas = await dbContext.Cobrancas.AsNoTracking()
            .Where(c => c.OrganizacaoId == organizacaoId && c.UnidadeId == unidadeId)
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

        var cobrancasPendentes = cobrancas.Count(c => c.Status == StatusCobranca.Pendente);
        var cobrancasAtrasadas = cobrancas.Count(c => c.Status == StatusCobranca.Atrasada);

        var alunosComDebito = cobrancas
            .Where(c => c.Status is StatusCobranca.Pendente or StatusCobranca.Atrasada)
            .Select(c => c.AlunoId)
            .Distinct()
            .Count();

        return new ResumoFinanceiro(
            totalReceita,
            totalPendente,
            totalAtrasado,
            cobrancasPendentes,
            cobrancasAtrasadas,
            alunosComDebito);
    }
}
