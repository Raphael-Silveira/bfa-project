using BFA.Application.Planos;
using BFA.Domain.Planos;
using BFA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace BFA.Infrastructure.Planos;

public sealed class PlanosRepositorio(BfaDbContext dbContext, ILogger<PlanosRepositorio> logger) : IPlanosRepositorio
{
    public async Task<IReadOnlyList<PlanoResumo>> ListarAsync(
        Guid organizacaoId,
        Guid? unidadeId,
        FiltroPlanos filtro,
        CancellationToken cancellationToken)
    {
        var consulta = dbContext.Planos.AsNoTracking()
            .Where(plano => plano.OrganizacaoId == organizacaoId
                && plano.UnidadeId == unidadeId);
        consulta = filtro switch
        {
            FiltroPlanos.Ativos => consulta.Where(plano => plano.Ativo),
            FiltroPlanos.Inativos => consulta.Where(plano => !plano.Ativo),
            _ => consulta
        };
        var planos = await consulta
            .OrderBy(plano => plano.Nome)
            .Select(plano => new { plano.Id, plano.Nome, plano.Ativo })
            .ToArrayAsync(cancellationToken);
        var ids = planos.Select(plano => plano.Id).ToArray();
        var versoes = await dbContext.PlanosVersoes.AsNoTracking()
            .Where(versao => versao.OrganizacaoId == organizacaoId
                && ids.Contains(versao.PlanoId)
                && versao.VigenciaFim == null)
            .Select(versao => new
            {
                versao.PlanoId,
                Resumo = new PlanoVersaoResumo(
                    versao.Id,
                    versao.NumeroVersao,
                    versao.DuracaoMeses,
                    versao.FrequenciaSemanal,
                    versao.ValorMensal,
                    versao.CobraMatricula,
                    versao.ValorMatricula,
                    versao.VigenciaInicio,
                    versao.VigenciaFim)
            })
            .ToArrayAsync(cancellationToken);
        return planos.Select(plano => new PlanoResumo(
            plano.Id,
            plano.Nome,
            plano.Ativo,
            versoes.SingleOrDefault(versao => versao.PlanoId == plano.Id)?.Resumo))
            .ToArray();
    }

    public async Task<PlanoDetalheResumo?> ObterAsync(
        Guid organizacaoId,
        Guid? unidadeId,
        Guid planoId,
        CancellationToken cancellationToken)
    {
        var plano = await dbContext.Planos.AsNoTracking()
            .Where(item => item.Id == planoId
                && item.OrganizacaoId == organizacaoId
                && item.UnidadeId == unidadeId)
            .Select(item => new
            {
                item.Id,
                item.OrganizacaoId,
                item.UnidadeId,
                item.Nome,
                item.Ativo
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (plano is null) return null;
        var versoes = await dbContext.PlanosVersoes.AsNoTracking()
            .Where(versao => versao.OrganizacaoId == organizacaoId
                && versao.PlanoId == planoId)
            .OrderByDescending(versao => versao.NumeroVersao)
            .Select(versao => new PlanoVersaoResumo(
                versao.Id,
                versao.NumeroVersao,
                versao.DuracaoMeses,
                versao.FrequenciaSemanal,
                versao.ValorMensal,
                versao.CobraMatricula,
                versao.ValorMatricula,
                versao.VigenciaInicio,
                versao.VigenciaFim))
            .ToArrayAsync(cancellationToken);
        return new(
            plano.Id, plano.OrganizacaoId, plano.UnidadeId,
            plano.Nome, plano.Ativo, versoes);
    }

    public async Task<EstadoPersistenciaPlano> CriarAsync(
        Plano plano,
        PlanoVersao versao,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(cancellationToken);
        try
        {
            dbContext.Planos.Add(plano);
            dbContext.PlanosVersoes.Add(versao);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return EstadoPersistenciaPlano.Sucesso;
        }
        catch (DbUpdateException exception) when (EhConflitoControlado(exception))
        {
            logger.LogWarning(exception,
                "Conflito ao criar plano na organizacao {OrganizacaoId}", plano.OrganizacaoId);
            await transaction.RollbackAsync(cancellationToken);
            return EstadoPersistenciaPlano.ConflitoConcorrencia;
        }
    }

    public async Task<EstadoPersistenciaPlano> CriarNovaVersaoAsync(
        Guid organizacaoId,
        Guid? unidadeId,
        Guid planoId,
        PlanoTermosSolicitacao termos,
        Guid usuarioId,
        DateTime agoraUtc,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(cancellationToken);
        try
        {
            var plano = await dbContext.Planos
                .FromSqlInterpolated($"""
                    SELECT * FROM planos
                    WHERE organizacao_id = {organizacaoId} AND id = {planoId}
                    FOR UPDATE
                    """)
                .SingleOrDefaultAsync(cancellationToken);
            if (plano is null || plano.UnidadeId != unidadeId)
                return EstadoPersistenciaPlano.PlanoNaoEncontrado;

            var atual = await dbContext.PlanosVersoes.SingleOrDefaultAsync(
                versao => versao.OrganizacaoId == organizacaoId
                    && versao.PlanoId == planoId
                    && versao.VigenciaFim == null,
                cancellationToken);
            if (atual is null) return EstadoPersistenciaPlano.SemVersaoAberta;
            if (termos.VigenciaInicio <= atual.VigenciaInicio)
                return EstadoPersistenciaPlano.VigenciaInvalida;

            var proximoNumero = await dbContext.PlanosVersoes
                .Where(versao => versao.OrganizacaoId == organizacaoId
                    && versao.PlanoId == planoId)
                .MaxAsync(versao => versao.NumeroVersao, cancellationToken) + 1;

            atual.Encerrar(termos.VigenciaInicio.AddDays(-1));
            await dbContext.SaveChangesAsync(cancellationToken);

            var nova = new PlanoVersao(
                Guid.NewGuid(), organizacaoId, planoId, proximoNumero,
                termos.DuracaoMeses, termos.FrequenciaSemanal,
                termos.ValorMensal, termos.CobraMatricula, termos.ValorMatricula,
                termos.VigenciaInicio, null, usuarioId, agoraUtc);
            dbContext.PlanosVersoes.Add(nova);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return EstadoPersistenciaPlano.Sucesso;
        }
        catch (ArgumentException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return EstadoPersistenciaPlano.DadosInvalidos;
        }
        catch (DbUpdateException exception) when (EhConflitoControlado(exception))
        {
            logger.LogWarning(exception,
                "Conflito ao criar nova versao do plano {PlanoId} na organizacao {OrganizacaoId}",
                planoId, organizacaoId);
            await transaction.RollbackAsync(cancellationToken);
            return EstadoPersistenciaPlano.ConflitoConcorrencia;
        }
    }

    public async Task<EstadoPersistenciaPlano> AlterarEstadoAsync(
        Guid organizacaoId,
        Guid? unidadeId,
        Guid planoId,
        bool ativar,
        Guid usuarioId,
        DateTime agoraUtc,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(cancellationToken);
        var plano = await dbContext.Planos
            .FromSqlInterpolated($"""
                SELECT * FROM planos
                WHERE organizacao_id = {organizacaoId} AND id = {planoId}
                FOR UPDATE
                """)
            .SingleOrDefaultAsync(cancellationToken);
        if (plano is null || plano.UnidadeId != unidadeId)
            return EstadoPersistenciaPlano.PlanoNaoEncontrado;
        if (ativar && !await dbContext.PlanosVersoes.AsNoTracking().AnyAsync(
                versao => versao.OrganizacaoId == organizacaoId
                    && versao.PlanoId == planoId
                    && versao.VigenciaFim == null,
                cancellationToken))
            return EstadoPersistenciaPlano.SemVersaoAberta;

        if (ativar) plano.Ativar(usuarioId, agoraUtc);
        else plano.Desativar(usuarioId, agoraUtc);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception,
                "Falha ao alterar estado do plano {PlanoId} na organizacao {OrganizacaoId}",
                planoId, organizacaoId);
            throw;
        }
        return EstadoPersistenciaPlano.Sucesso;
    }

    private static bool EhConflitoControlado(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.CheckViolation
        };

}
