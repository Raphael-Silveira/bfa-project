using BFA.Application.Franqueadora.Unidades;
using BFA.Domain.Unidades;
using BFA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BFA.Infrastructure.Franqueadora;

public sealed class UnidadesFranqueadoraRepositorio(BfaDbContext dbContext)
    : IUnidadesFranqueadoraRepositorio
{
    private const string RestricaoSlugUnico = "uq_unidades_organizacao_id_slug";

    public async Task<IReadOnlyList<UnidadeResumo>> ListarAsync(
        Guid organizacaoId,
        CancellationToken cancellationToken)
    {
        return await dbContext.Unidades
            .AsNoTracking()
            .Where(unidade => unidade.OrganizacaoId == organizacaoId)
            .OrderBy(unidade => unidade.Nome)
            .Select(unidade => new UnidadeResumo(
                unidade.Id,
                unidade.Nome,
                unidade.Slug,
                unidade.Ativa,
                unidade.CriadoEmUtc))
            .ToArrayAsync(cancellationToken);
    }

    public Task<UnidadeDetalhe?> ObterDetalheAsync(
        Guid organizacaoId,
        Guid unidadeId,
        CancellationToken cancellationToken)
    {
        return dbContext.Unidades
            .AsNoTracking()
            .Where(unidade => unidade.OrganizacaoId == organizacaoId
                && unidade.Id == unidadeId)
            .Select(unidade => new UnidadeDetalhe(
                unidade.Id,
                unidade.Nome,
                unidade.Slug,
                unidade.Ativa,
                unidade.CriadoEmUtc))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public Task<Unidade?> ObterParaAlteracaoAsync(
        Guid organizacaoId,
        Guid unidadeId,
        CancellationToken cancellationToken)
    {
        return dbContext.Unidades.SingleOrDefaultAsync(
            unidade => unidade.OrganizacaoId == organizacaoId
                && unidade.Id == unidadeId,
            cancellationToken);
    }

    public Task<bool> ExisteSlugAsync(
        Guid organizacaoId,
        string slug,
        Guid? unidadeIgnoradaId,
        CancellationToken cancellationToken)
    {
        return dbContext.Unidades
            .AsNoTracking()
            .AnyAsync(
                unidade => unidade.OrganizacaoId == organizacaoId
                    && unidade.Slug == slug
                    && (!unidadeIgnoradaId.HasValue
                        || unidade.Id != unidadeIgnoradaId.Value),
                cancellationToken);
    }

    public void Adicionar(Unidade unidade)
    {
        ArgumentNullException.ThrowIfNull(unidade);
        dbContext.Unidades.Add(unidade);
    }

    public async Task<ResultadoPersistenciaUnidade> SalvarAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return ResultadoPersistenciaUnidade.Sucesso;
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: RestricaoSlugUnico
            })
        {
            return ResultadoPersistenciaUnidade.SlugDuplicado;
        }
    }
}
