using BFA.Application.Alunos;
using BFA.Domain.Alunos;
using BFA.Domain.Matriculas;
using BFA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BFA.Infrastructure.Alunos;

public sealed class AlunosRepositorio(BfaDbContext dbContext, ILogger<AlunosRepositorio> logger) : IAlunosRepositorio
{
    public async Task<IReadOnlyList<AlunoListaItem>> ListarAsync(
        Guid organizacaoId, Guid unidadeId, string? texto,
        CancellationToken cancellationToken)
    {
        var matriculas = await dbContext.Matriculas.AsNoTracking()
            .Where(m => m.OrganizacaoId == organizacaoId && m.UnidadeId == unidadeId)
            .ToListAsync(cancellationToken);

        var alunoIds = matriculas.Select(m => m.AlunoId).Distinct().ToList();

        var alunos = await dbContext.Alunos.AsNoTracking()
            .Where(a => alunoIds.Contains(a.Id) && a.OrganizacaoId == organizacaoId)
            .ToDictionaryAsync(a => a.Id, cancellationToken);

        var planoVersaoIds = matriculas.Select(m => m.PlanoVersaoId).Distinct().ToList();

        var planosVersoes = await dbContext.PlanosVersoes.AsNoTracking()
            .Where(pv => planoVersaoIds.Contains(pv.Id) && pv.OrganizacaoId == organizacaoId)
            .ToDictionaryAsync(pv => pv.Id, cancellationToken);

        var planoIds = planosVersoes.Values.Select(pv => pv.PlanoId).Distinct().ToList();

        var planos = await dbContext.Planos.AsNoTracking()
            .Where(p => planoIds.Contains(p.Id) && p.OrganizacaoId == organizacaoId)
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        if (!string.IsNullOrWhiteSpace(texto))
        {
            var termo = texto.Trim().ToUpper();
            matriculas = matriculas
                .Where(m => alunos.TryGetValue(m.AlunoId, out var a)
                    && a.NomeCompleto.ToUpper().Contains(termo))
                .ToList();
        }

        var resultado = new List<AlunoListaItem>();
        var vistos = new HashSet<Guid>();

        foreach (var matricula in matriculas
            .OrderBy(m => alunos.TryGetValue(m.AlunoId, out var a) ? a.NomeCompleto : string.Empty)
            .ThenByDescending(m => m.DataInicio))
        {
            if (!alunos.TryGetValue(matricula.AlunoId, out var aluno))
                continue;
            if (!vistos.Add(aluno.Id))
                continue;

            var matriculaAtiva = matriculas.FirstOrDefault(m =>
                m.AlunoId == aluno.Id && m.Status == StatusMatricula.Ativa);

            string? planoNome = null;
            int? freq = null;
            if (matriculaAtiva is not null
                && planosVersoes.TryGetValue(matriculaAtiva.PlanoVersaoId, out var pvAtiva)
                && planos.TryGetValue(pvAtiva.PlanoId, out var planoAtivo))
            {
                planoNome = planoAtivo.Nome;
                freq = pvAtiva.FrequenciaSemanal;
            }

            resultado.Add(new AlunoListaItem(
                aluno.Id,
                aluno.NomeCompleto,
                aluno.DataNascimento,
                aluno.Telefone,
                aluno.Email,
                aluno.Ativo,
                matriculaAtiva?.Id,
                planoNome,
                freq,
                matriculaAtiva?.Status,
                matriculaAtiva?.DataInicio,
                matriculaAtiva?.DataFimPrevista));
        }

        return resultado;
    }

    public async Task<AlunoDetalhe?> ObterAsync(
        Guid organizacaoId, Guid unidadeId, Guid alunoId,
        CancellationToken cancellationToken)
    {
        var aluno = await dbContext.Alunos.AsNoTracking()
            .FirstOrDefaultAsync(
                a => a.Id == alunoId && a.OrganizacaoId == organizacaoId,
                cancellationToken);
        if (aluno is null)
            return null;

        var possuiMatriculaNaUnidade = await dbContext.Matriculas.AsNoTracking()
            .AnyAsync(m =>
                m.OrganizacaoId == organizacaoId
                && m.UnidadeId == unidadeId
                && m.AlunoId == alunoId,
                cancellationToken);
        if (!possuiMatriculaNaUnidade)
            return null;

        var vinculosAluno = await dbContext.AlunosResponsaveis.AsNoTracking()
            .Where(ar => ar.OrganizacaoId == organizacaoId && ar.AlunoId == alunoId)
            .ToListAsync(cancellationToken);

        var responsavelIds = vinculosAluno.Select(v => v.ResponsavelId).Distinct().ToList();

        var responsaveisMap = await dbContext.Responsaveis.AsNoTracking()
            .Where(r => responsavelIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, cancellationToken);

        var vinculos = vinculosAluno
            .Where(ar => responsaveisMap.ContainsKey(ar.ResponsavelId))
            .Select(ar =>
            {
                var r = responsaveisMap[ar.ResponsavelId];
                return new ResponsavelAlunoResumo(
                    r.Id,
                    r.NomeCompleto,
                    r.Telefone,
                    r.Email,
                    ar.TipoRelacao,
                    ar.DescricaoRelacao,
                    ar.PrincipalContato,
                    ar.ResponsavelFinanceiro,
                    ar.Ativo,
                    r.Ativo);
            })
            .OrderBy(x => x.NomeCompleto)
            .ToList();

        var matriculasAluno = await dbContext.Matriculas.AsNoTracking()
            .Where(m => m.OrganizacaoId == organizacaoId
                && m.UnidadeId == unidadeId
                && m.AlunoId == alunoId)
            .ToListAsync(cancellationToken);

        var planoVersaoIds = matriculasAluno.Select(m => m.PlanoVersaoId).Distinct().ToList();

        var planosVersoes = await dbContext.PlanosVersoes.AsNoTracking()
            .Where(pv => planoVersaoIds.Contains(pv.Id) && pv.OrganizacaoId == organizacaoId)
            .ToDictionaryAsync(pv => pv.Id, cancellationToken);

        var planoIdsPlanosVersoes = planosVersoes.Values.Select(pv => pv.PlanoId).Distinct().ToList();

        var planos = await dbContext.Planos.AsNoTracking()
            .Where(p => planoIdsPlanosVersoes.Contains(p.Id) && p.OrganizacaoId == organizacaoId)
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        MatriculaAlunoResumo? matriculaAtiva = null;
        var mAtiva = matriculasAluno.FirstOrDefault(m => m.Status == StatusMatricula.Ativa);
        if (mAtiva is not null
            && planosVersoes.TryGetValue(mAtiva.PlanoVersaoId, out var pvAtiva)
            && planos.TryGetValue(pvAtiva.PlanoId, out var planoAtivo))
        {
            matriculaAtiva = new MatriculaAlunoResumo(
                mAtiva.Id,
                planoAtivo.Nome,
                pvAtiva.NumeroVersao,
                mAtiva.Status,
                mAtiva.DataInicio,
                mAtiva.DataFimPrevista,
                mAtiva.DataFimReal,
                pvAtiva.FrequenciaSemanal,
                mAtiva.ValorMensalContratado);
        }

        var historico = matriculasAluno
            .Where(m => m.Status != StatusMatricula.Ativa)
            .OrderByDescending(m => m.DataInicio)
            .Select(m =>
            {
                if (planosVersoes.TryGetValue(m.PlanoVersaoId, out var pv)
                    && planos.TryGetValue(pv.PlanoId, out var p))
                {
                    return new MatriculaAlunoResumo(
                        m.Id,
                        p.Nome,
                        pv.NumeroVersao,
                        m.Status,
                        m.DataInicio,
                        m.DataFimPrevista,
                        m.DataFimReal,
                        pv.FrequenciaSemanal,
                        m.ValorMensalContratado);
                }
                return new MatriculaAlunoResumo(
                    m.Id,
                    string.Empty,
                    0,
                    m.Status,
                    m.DataInicio,
                    m.DataFimPrevista,
                    m.DataFimReal,
                    0,
                    m.ValorMensalContratado);
            })
            .ToList();

        return new AlunoDetalhe(
            aluno.Id,
            aluno.NomeCompleto,
            aluno.DataNascimento,
            aluno.Cpf,
            aluno.Telefone,
            aluno.Email,
            aluno.Ativo,
            vinculos,
            matriculaAtiva,
            historico);
    }

    public async Task<DadosEdicaoAluno?> ObterParaEdicaoAsync(
        Guid organizacaoId, Guid unidadeId, Guid alunoId,
        CancellationToken cancellationToken)
    {
        var aluno = await dbContext.Alunos
            .FirstOrDefaultAsync(
                a => a.Id == alunoId && a.OrganizacaoId == organizacaoId,
                cancellationToken);
        if (aluno is null)
            return null;

        var possuiMatriculaNaUnidade = await dbContext.Matriculas.AsNoTracking()
            .AnyAsync(m =>
                m.OrganizacaoId == organizacaoId
                && m.UnidadeId == unidadeId
                && m.AlunoId == alunoId,
                cancellationToken);
        if (!possuiMatriculaNaUnidade)
            return null;

        var dados = new AlunoDadosEdicao(
            aluno.Id,
            aluno.NomeCompleto,
            aluno.DataNascimento,
            aluno.Cpf,
            aluno.Telefone,
            aluno.Email);

        return new DadosEdicaoAluno(dados, [], []);
    }

    public async Task<IReadOnlyList<MatriculaAtivaResumo>> ObterMatriculasAtivasAlunoAsync(
        Guid organizacaoId, Guid alunoId,
        CancellationToken cancellationToken)
    {
        return await dbContext.Matriculas.AsNoTracking()
            .Where(m =>
                m.OrganizacaoId == organizacaoId
                && m.AlunoId == alunoId
                && m.Status == StatusMatricula.Ativa)
            .Select(m => new MatriculaAtivaResumo(m.Id, m.DataInicio))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ResponsavelAtivoResumo>> ObterResponsaveisAlunoAsync(
        Guid organizacaoId, Guid alunoId,
        CancellationToken cancellationToken)
    {
        var vinculosAluno = await dbContext.AlunosResponsaveis.AsNoTracking()
            .Where(ar => ar.OrganizacaoId == organizacaoId && ar.AlunoId == alunoId)
            .ToListAsync(cancellationToken);

        var responsavelIds = vinculosAluno.Select(v => v.ResponsavelId).Distinct().ToList();

        var responsaveisMap = await dbContext.Responsaveis.AsNoTracking()
            .Where(r => responsavelIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, cancellationToken);

        return vinculosAluno
            .Where(ar => responsaveisMap.ContainsKey(ar.ResponsavelId))
            .Select(ar => new ResponsavelAtivoResumo(
                ar.ResponsavelId, ar.Ativo, responsaveisMap[ar.ResponsavelId].Ativo))
            .ToList();
    }

    public async Task<bool> ExisteRelacaoAlunoUnidadeAsync(
        Guid organizacaoId, Guid unidadeId, Guid alunoId,
        CancellationToken cancellationToken)
    {
        return await dbContext.Matriculas.AsNoTracking()
            .AnyAsync(m =>
                m.OrganizacaoId == organizacaoId
                && m.UnidadeId == unidadeId
                && m.AlunoId == alunoId,
                cancellationToken);
    }

    public async Task<bool> PersistirAtualizacaoAsync(
        Aluno aluno, CancellationToken cancellationToken)
    {
        var existente = await dbContext.Alunos
            .FirstOrDefaultAsync(
                a => a.Id == aluno.Id && a.OrganizacaoId == aluno.OrganizacaoId,
                cancellationToken);
        if (existente is null)
            return false;

        existente.AtualizarDados(
            aluno.NomeCompleto,
            aluno.DataNascimento,
            DateOnly.FromDateTime(DateTime.Today),
            aluno.Cpf,
            aluno.Telefone,
            aluno.Email,
            DateTime.UtcNow);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception,
                "Falha ao persistir atualizacao do aluno {AlunoId} na organizacao {OrganizacaoId}",
                aluno.Id, aluno.OrganizacaoId);
            throw;
        }

        return true;
    }

    public async Task<IReadOnlyList<ResponsavelAlunoResumo>> ListarResponsaveisAlunoAsync(
        Guid organizacaoId, Guid alunoId, CancellationToken cancellationToken)
    {
        var vinculosAluno = await dbContext.AlunosResponsaveis.AsNoTracking()
            .Where(ar => ar.OrganizacaoId == organizacaoId && ar.AlunoId == alunoId)
            .ToListAsync(cancellationToken);

        var responsavelIds = vinculosAluno.Select(v => v.ResponsavelId).Distinct().ToList();

        var responsaveisMap = await dbContext.Responsaveis.AsNoTracking()
            .Where(r => responsavelIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, cancellationToken);

        return vinculosAluno
            .Where(ar => responsaveisMap.ContainsKey(ar.ResponsavelId))
            .Select(ar =>
            {
                var r = responsaveisMap[ar.ResponsavelId];
                return new ResponsavelAlunoResumo(
                    r.Id,
                    r.NomeCompleto,
                    r.Telefone,
                    r.Email,
                    ar.TipoRelacao,
                    ar.DescricaoRelacao,
                    ar.PrincipalContato,
                    ar.ResponsavelFinanceiro,
                    ar.Ativo,
                    r.Ativo);
            })
            .OrderBy(x => x.NomeCompleto)
            .ToList();
    }

    public async Task<Responsavel?> ObterResponsavelAsync(
        Guid organizacaoId, Guid responsavelId, CancellationToken cancellationToken)
    {
        return await dbContext.Responsaveis.AsNoTracking()
            .FirstOrDefaultAsync(
                r => r.Id == responsavelId && r.OrganizacaoId == organizacaoId,
                cancellationToken);
    }

    public async Task<AlunoResponsavel?> ObterVinculoAsync(
        Guid organizacaoId, Guid alunoId, Guid responsavelId, CancellationToken cancellationToken)
    {
        return await dbContext.AlunosResponsaveis.AsNoTracking()
            .FirstOrDefaultAsync(
                ar => ar.OrganizacaoId == organizacaoId
                    && ar.AlunoId == alunoId
                    && ar.ResponsavelId == responsavelId,
                cancellationToken);
    }

    public async Task<bool> CriarResponsavelAsync(
        Responsavel responsavel, AlunoResponsavel vinculo, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            dbContext.Responsaveis.Add(responsavel);
            dbContext.AlunosResponsaveis.Add(vinculo);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch (Exception exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            logger.LogError(exception,
                "Falha ao criar responsavel {ResponsavelId} na organizacao {OrganizacaoId}",
                responsavel.Id, responsavel.OrganizacaoId);
            throw;
        }
    }

    public async Task<bool> AtualizarResponsavelAsync(
        Responsavel responsavel, CancellationToken cancellationToken)
    {
        var existente = await dbContext.Responsaveis
            .FirstOrDefaultAsync(
                r => r.Id == responsavel.Id && r.OrganizacaoId == responsavel.OrganizacaoId,
                cancellationToken);
        if (existente is null)
            return false;

        existente.AtualizarDados(
            responsavel.NomeCompleto,
            responsavel.Cpf,
            responsavel.Telefone,
            responsavel.Email,
            DateTime.UtcNow);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception,
                "Falha ao atualizar responsavel {ResponsavelId} na organizacao {OrganizacaoId}",
                responsavel.Id, responsavel.OrganizacaoId);
            throw;
        }

        return true;
    }

    public async Task<bool> AtualizarVinculoAsync(
        AlunoResponsavel vinculo, CancellationToken cancellationToken)
    {
        var existente = await dbContext.AlunosResponsaveis
            .FirstOrDefaultAsync(
                ar => ar.Id == vinculo.Id && ar.OrganizacaoId == vinculo.OrganizacaoId,
                cancellationToken);
        if (existente is null)
            return false;

        existente.AtualizarClassificacao(
            vinculo.TipoRelacao,
            vinculo.DescricaoRelacao,
            vinculo.PrincipalContato,
            vinculo.ResponsavelFinanceiro,
            DateTime.UtcNow);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception,
                "Falha ao atualizar vinculo {VinculoId} na organizacao {OrganizacaoId}",
                vinculo.Id, vinculo.OrganizacaoId);
            throw;
        }

        return true;
    }

    public async Task<bool> DesativarVinculoAsync(
        Guid organizacaoId, Guid alunoId, Guid responsavelId,
        DateTime atualizadoEmUtc, CancellationToken cancellationToken)
    {
        var vinculo = await dbContext.AlunosResponsaveis
            .FirstOrDefaultAsync(
                ar => ar.OrganizacaoId == organizacaoId
                    && ar.AlunoId == alunoId
                    && ar.ResponsavelId == responsavelId,
                cancellationToken);
        if (vinculo is null)
            return false;

        vinculo.Desativar(atualizadoEmUtc);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception,
                "Falha ao desativar vinculo aluno {AlunoId} responsavel {ResponsavelId} na organizacao {OrganizacaoId}",
                alunoId, responsavelId, organizacaoId);
            throw;
        }

        return true;
    }

    public async Task<bool> AtivarVinculoAsync(
        Guid organizacaoId, Guid alunoId, Guid responsavelId,
        DateTime atualizadoEmUtc, CancellationToken cancellationToken)
    {
        var vinculo = await dbContext.AlunosResponsaveis
            .FirstOrDefaultAsync(
                ar => ar.OrganizacaoId == organizacaoId
                    && ar.AlunoId == alunoId
                    && ar.ResponsavelId == responsavelId,
                cancellationToken);
        if (vinculo is null)
            return false;

        vinculo.Ativar(atualizadoEmUtc);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception,
                "Falha ao ativar vinculo aluno {AlunoId} responsavel {ResponsavelId} na organizacao {OrganizacaoId}",
                alunoId, responsavelId, organizacaoId);
            throw;
        }

        return true;
    }

    public async Task<bool> ExisteResponsavelNaOrganizacaoAsync(
        Guid organizacaoId, Guid responsavelId, CancellationToken cancellationToken)
    {
        return await dbContext.Responsaveis.AsNoTracking()
            .AnyAsync(
                r => r.Id == responsavelId && r.OrganizacaoId == organizacaoId,
                cancellationToken);
    }

    public async Task<bool> ExisteVinculoAtivoAlunoResponsavelAsync(
        Guid organizacaoId, Guid alunoId, Guid responsavelId, CancellationToken cancellationToken)
    {
        return await dbContext.AlunosResponsaveis.AsNoTracking()
            .AnyAsync(
                ar => ar.OrganizacaoId == organizacaoId
                    && ar.AlunoId == alunoId
                    && ar.ResponsavelId == responsavelId
                    && ar.Ativo,
                cancellationToken);
    }
}
