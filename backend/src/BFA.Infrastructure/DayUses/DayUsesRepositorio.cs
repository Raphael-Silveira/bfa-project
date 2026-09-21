using BFA.Application.DayUses;
using BFA.Domain.DayUses;
using BFA.Domain.Matriculas;
using BFA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BFA.Infrastructure.DayUses;

public sealed class DayUsesRepositorio(BfaDbContext dbContext, ILogger<DayUsesRepositorio> logger) : IDayUsesRepositorio
{
    public Task<DayUseUnidadeResumo?> ObterUnidadeAsync(Guid organizacaoId, Guid unidadeId, CancellationToken cancellationToken) =>
        dbContext.Unidades.AsNoTracking().Where(u => u.OrganizacaoId == organizacaoId && u.Id == unidadeId)
            .Select(u => new DayUseUnidadeResumo(u.OrganizacaoId, u.Id, u.Nome, u.ValorDayUseSugerido)).SingleOrDefaultAsync(cancellationToken);

    public async Task<bool> AtualizarValorSugeridoAsync(Guid organizacaoId, Guid unidadeId, decimal? valor, DateTime agoraUtc, CancellationToken cancellationToken)
    {
        var unidade = await dbContext.Unidades.SingleOrDefaultAsync(u => u.OrganizacaoId == organizacaoId && u.Id == unidadeId, cancellationToken);
        if (unidade is null) return false;
        unidade.ConfigurarValorDayUseSugerido(valor, agoraUtc);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<DayUseAlunoResumo>> ListarAlunosAsync(Guid organizacaoId, Guid unidadeId, string? texto, CancellationToken cancellationToken)
    {
        var query = from aluno in dbContext.Alunos.AsNoTracking()
                    join matricula in dbContext.Matriculas.AsNoTracking()
                        on new { aluno.OrganizacaoId, AlunoId = aluno.Id } equals new { matricula.OrganizacaoId, matricula.AlunoId }
                    where aluno.OrganizacaoId == organizacaoId && aluno.Ativo && matricula.UnidadeId == unidadeId
                    select aluno;
        if (!string.IsNullOrWhiteSpace(texto))
            query = query.Where(a => a.NomeCompleto.Contains(texto.Trim()));
        return await query.Distinct().OrderBy(a => a.NomeCompleto).Take(50)
            .Select(a => new DayUseAlunoResumo(a.Id, a.NomeCompleto, a.Telefone)).ToArrayAsync(cancellationToken);
    }

    public Task<bool> ExisteAlunoNaUnidadeAsync(Guid organizacaoId, Guid unidadeId, Guid alunoId, CancellationToken cancellationToken) =>
        dbContext.Alunos.AsNoTracking().AnyAsync(a => a.OrganizacaoId == organizacaoId && a.Id == alunoId && a.Ativo
            && dbContext.Matriculas.Any(m => m.OrganizacaoId == organizacaoId && m.UnidadeId == unidadeId && m.AlunoId == alunoId), cancellationToken);

    public Task<bool> ExisteDuplicadoAsync(Guid organizacaoId, Guid unidadeId, Guid alunoId, DateOnly dataUso, CancellationToken cancellationToken) =>
        dbContext.DayUses.AsNoTracking().AnyAsync(d => d.OrganizacaoId == organizacaoId && d.UnidadeId == unidadeId && d.AlunoId == alunoId && d.DataUso == dataUso, cancellationToken);

    public async Task<bool> CriarAsync(DayUse dayUse, CancellationToken cancellationToken)
    {
        try
        {
            dbContext.DayUses.Add(dayUse);
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException ex)
        {
            logger.LogWarning(ex, "Day Use rejeitado por conflito: unidade {UnidadeId}", dayUse.UnidadeId);
            return false;
        }
    }

    public async Task<DayUsePagina> ListarAsync(Guid organizacaoId, Guid unidadeId, FiltroDayUses filtro, CancellationToken cancellationToken)
    {
        var query = dbContext.DayUses.AsNoTracking()
            .Where(dayUse => dayUse.OrganizacaoId == organizacaoId && dayUse.UnidadeId == unidadeId);
        if (filtro.DataInicial is { } inicio) query = query.Where(i => i.DataUso >= inicio);
        if (filtro.DataFinal is { } fim) query = query.Where(i => i.DataUso <= fim);
        if (!string.IsNullOrWhiteSpace(filtro.Participante))
        {
            var participante = filtro.Participante.Trim();
            var padrao = $"%{participante.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";
            query = query.Where(dayUse =>
                (dayUse.NomeAvulso != null && EF.Functions.ILike(dayUse.NomeAvulso, padrao, "\\"))
                || (dayUse.AlunoId != null && dbContext.Alunos.Any(aluno =>
                    aluno.OrganizacaoId == organizacaoId
                    && aluno.Id == dayUse.AlunoId
                    && EF.Functions.ILike(aluno.NomeCompleto, padrao, "\\"))));
        }
        var total = await query.CountAsync(cancellationToken);
        var tamanho = Math.Clamp(filtro.TamanhoPagina, 5, 50);
        var totalPaginas = Math.Max(1, (int)Math.Ceiling(total / (double)tamanho));
        var pagina = Math.Clamp(filtro.Pagina, 1, totalPaginas);
        var registros = await query.OrderByDescending(i => i.DataUso).ThenBy(i => i.NomeAvulso)
            .Skip((pagina - 1) * tamanho).Take(tamanho)
            .Select(dayUse => new
            {
                dayUse.Id,
                dayUse.DataUso,
                dayUse.AlunoId,
                dayUse.NomeAvulso,
                dayUse.TelefoneAvulso,
                dayUse.EmailAvulso,
                dayUse.ValorSugerido,
                dayUse.ValorCobrado,
                dayUse.Pago,
                dayUse.CriadoPorUsuarioId
            }).ToArrayAsync(cancellationToken);
        var alunoIds = registros.Where(item => item.AlunoId.HasValue).Select(item => item.AlunoId!.Value).Distinct().ToArray();
        var alunos = await dbContext.Alunos.AsNoTracking()
            .Where(aluno => aluno.OrganizacaoId == organizacaoId && alunoIds.Contains(aluno.Id))
            .ToDictionaryAsync(aluno => aluno.Id, cancellationToken);
        var criadorIds = registros.Select(item => item.CriadoPorUsuarioId).Distinct().ToArray();
        var nomesProfessores = await (
            from vinculo in dbContext.ProfessoresUnidades.AsNoTracking()
            join professor in dbContext.Professores.AsNoTracking()
                on new { vinculo.OrganizacaoId, vinculo.ProfessorId }
                equals new { professor.OrganizacaoId, ProfessorId = professor.Id }
            where vinculo.OrganizacaoId == organizacaoId
                && vinculo.UnidadeId == unidadeId
                && professor.UsuarioId.HasValue
                && criadorIds.Contains(professor.UsuarioId.Value)
            select new { UsuarioId = professor.UsuarioId!.Value, professor.NomeCompleto })
            .ToDictionaryAsync(item => item.UsuarioId, item => item.NomeCompleto, cancellationToken);
        var nomesCriadores = await dbContext.PerfisUsuario.AsNoTracking()
            .Where(perfil => criadorIds.Contains(perfil.UsuarioId))
            .ToDictionaryAsync(perfil => perfil.UsuarioId, perfil => perfil.NomeCompleto, cancellationToken);
        var itens = registros.Select(item =>
        {
            var aluno = item.AlunoId is { } alunoId && alunos.TryGetValue(alunoId, out var encontrado) ? encontrado : null;
            return new DayUseListaItem(item.Id, item.DataUso, item.AlunoId,
                aluno?.NomeCompleto ?? item.NomeAvulso!, aluno is not null,
                aluno?.Telefone ?? item.TelefoneAvulso, aluno?.Email ?? item.EmailAvulso,
                item.ValorSugerido, item.ValorCobrado, item.Pago, item.CriadoPorUsuarioId,
                nomesProfessores.GetValueOrDefault(item.CriadoPorUsuarioId)
                    ?? nomesCriadores.GetValueOrDefault(item.CriadoPorUsuarioId)
                    ?? "Usuário não identificado");
        }).ToArray();
        return new(itens, pagina, totalPaginas, total);
    }

    public async Task<bool> MarcarComoPagoAsync(Guid organizacaoId, Guid unidadeId, Guid dayUseId, CancellationToken cancellationToken)
    {
        var dayUse = await dbContext.DayUses.SingleOrDefaultAsync(d => d.Id == dayUseId && d.OrganizacaoId == organizacaoId && d.UnidadeId == unidadeId, cancellationToken);
        if (dayUse is null) return false;
        try { dayUse.MarcarComoPago(); await dbContext.SaveChangesAsync(cancellationToken); return true; }
        catch (InvalidOperationException) { return false; }
    }

    public async Task<bool> ExcluirAsync(Guid organizacaoId, Guid unidadeId, Guid dayUseId, Guid? criadoPorUsuarioId, CancellationToken cancellationToken)
    {
        var query = dbContext.DayUses.Where(item => item.Id == dayUseId
            && item.OrganizacaoId == organizacaoId
            && item.UnidadeId == unidadeId);
        if (criadoPorUsuarioId is { } usuarioId)
            query = query.Where(item => item.CriadoPorUsuarioId == usuarioId);
        return await query.ExecuteDeleteAsync(cancellationToken) == 1;
    }
}
