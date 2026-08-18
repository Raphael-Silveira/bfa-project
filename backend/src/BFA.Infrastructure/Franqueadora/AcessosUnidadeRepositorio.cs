using BFA.Application.Franqueadora.AcessosUnidade;
using BFA.Domain.Acessos;
using BFA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BFA.Infrastructure.Franqueadora;

public sealed class AcessosUnidadeRepositorio(BfaDbContext dbContext)
    : IAcessosUnidadeRepositorio
{
    private const string RestricaoVinculoUnico =
        "uq_vinculos_acesso_usuario_organizacao_unidade_perfil";

    public Task<UnidadeAcessosResumo?> ObterUnidadeAsync(
        Guid organizacaoId,
        Guid unidadeId,
        CancellationToken cancellationToken)
    {
        return dbContext.Unidades
            .AsNoTracking()
            .Where(unidade => unidade.OrganizacaoId == organizacaoId
                && unidade.Id == unidadeId)
            .Select(unidade => new UnidadeAcessosResumo(
                unidade.Id,
                unidade.Nome,
                unidade.Ativa))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AdministradorUnidadeResumo>> ListarAdministradoresAsync(
        Guid organizacaoId,
        Guid unidadeId,
        CancellationToken cancellationToken)
    {
        return await (
            from vinculo in dbContext.VinculosAcesso.AsNoTracking()
            join usuario in dbContext.Users.AsNoTracking()
                on vinculo.UsuarioId equals usuario.Id
            where vinculo.OrganizacaoId == organizacaoId
                && vinculo.UnidadeId == unidadeId
                && vinculo.Perfil == PerfilAcesso.AdministradorUnidade
            orderby usuario.Email
            select new AdministradorUnidadeResumo(
                vinculo.Id,
                vinculo.UsuarioId,
                usuario.Email ?? string.Empty,
                vinculo.Ativo,
                vinculo.CriadoEmUtc))
            .ToArrayAsync(cancellationToken);
    }

    public Task<VinculoAcesso?> ObterAdministradorPorUsuarioAsync(
        Guid organizacaoId,
        Guid unidadeId,
        Guid usuarioId,
        CancellationToken cancellationToken)
    {
        return dbContext.VinculosAcesso.SingleOrDefaultAsync(
            vinculo => vinculo.OrganizacaoId == organizacaoId
                && vinculo.UnidadeId == unidadeId
                && vinculo.UsuarioId == usuarioId
                && vinculo.Perfil == PerfilAcesso.AdministradorUnidade,
            cancellationToken);
    }

    public Task<VinculoAcesso?> ObterAdministradorPorVinculoAsync(
        Guid organizacaoId,
        Guid unidadeId,
        Guid vinculoId,
        CancellationToken cancellationToken)
    {
        return dbContext.VinculosAcesso.SingleOrDefaultAsync(
            vinculo => vinculo.OrganizacaoId == organizacaoId
                && vinculo.UnidadeId == unidadeId
                && vinculo.Id == vinculoId
                && vinculo.Perfil == PerfilAcesso.AdministradorUnidade,
            cancellationToken);
    }

    public void Adicionar(VinculoAcesso vinculo)
    {
        ArgumentNullException.ThrowIfNull(vinculo);
        dbContext.VinculosAcesso.Add(vinculo);
    }

    public async Task<ResultadoPersistenciaAcessoUnidade> SalvarAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return ResultadoPersistenciaAcessoUnidade.Sucesso;
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: RestricaoVinculoUnico
            })
        {
            return ResultadoPersistenciaAcessoUnidade.VinculoDuplicado;
        }
    }
}
