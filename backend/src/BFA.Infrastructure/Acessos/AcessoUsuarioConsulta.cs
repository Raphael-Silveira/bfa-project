using BFA.Application.Acessos;
using BFA.Domain.Acessos;
using BFA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BFA.Infrastructure.Acessos;

public sealed class AcessoUsuarioConsulta(BfaDbContext dbContext) : IAcessoUsuarioConsulta
{
    public Task<bool> EhAdministradorRedeAsync(
        Guid usuarioId,
        CancellationToken cancellationToken)
    {
        return VinculosAtivos(usuarioId).AnyAsync(
            vinculo => vinculo.Perfil == PerfilAcesso.AdministradorRede
                && vinculo.UnidadeId == null,
            cancellationToken);
    }

    public Task<bool> EhAdministradorRedeNaOrganizacaoAsync(
        Guid usuarioId,
        Guid organizacaoId,
        CancellationToken cancellationToken)
    {
        return VinculosAtivos(usuarioId).AnyAsync(
            vinculo => vinculo.OrganizacaoId == organizacaoId
                && vinculo.Perfil == PerfilAcesso.AdministradorRede
                && vinculo.UnidadeId == null,
            cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> ListarOrganizacoesAdministradorRedeAsync(
        Guid usuarioId,
        CancellationToken cancellationToken)
    {
        return await VinculosAtivos(usuarioId)
            .Where(vinculo => vinculo.Perfil == PerfilAcesso.AdministradorRede
                && vinculo.UnidadeId == null)
            .Select(vinculo => vinculo.OrganizacaoId)
            .Distinct()
            .Take(2)
            .ToArrayAsync(cancellationToken);
    }

    public Task<bool> PossuiAlgumPerfilAsync(
        Guid usuarioId,
        IReadOnlyCollection<PerfilAcesso> perfis,
        CancellationToken cancellationToken)
    {
        var perfisConsultados = PerfisDistintos(perfis);

        return VinculosAtivos(usuarioId).AnyAsync(
            vinculo => perfisConsultados.Contains(vinculo.Perfil),
            cancellationToken);
    }

    public Task<bool> PossuiPerfilNaOrganizacaoAsync(
        Guid usuarioId,
        Guid organizacaoId,
        PerfilAcesso perfil,
        CancellationToken cancellationToken)
    {
        return VinculosAtivos(usuarioId).AnyAsync(
            vinculo => vinculo.OrganizacaoId == organizacaoId
                && vinculo.Perfil == perfil,
            cancellationToken);
    }

    public Task<bool> PossuiAcessoUnidadeAsync(
        Guid usuarioId,
        Guid organizacaoId,
        Guid unidadeId,
        CancellationToken cancellationToken)
    {
        return VinculosAtivos(usuarioId).AnyAsync(
            vinculo => vinculo.OrganizacaoId == organizacaoId
                && ((vinculo.Perfil == PerfilAcesso.AdministradorRede
                        && vinculo.UnidadeId == null)
                    || vinculo.UnidadeId == unidadeId),
            cancellationToken);
    }

    public Task<bool> PossuiPerfilNaUnidadeAsync(
        Guid usuarioId,
        Guid organizacaoId,
        Guid unidadeId,
        PerfilAcesso perfil,
        CancellationToken cancellationToken)
    {
        return VinculosAtivos(usuarioId).AnyAsync(
            vinculo => vinculo.OrganizacaoId == organizacaoId
                && vinculo.UnidadeId == unidadeId
                && vinculo.Perfil == perfil,
            cancellationToken);
    }

    public Task<bool> PossuiAlgumPerfilNaUnidadeAsync(
        Guid usuarioId,
        Guid organizacaoId,
        Guid unidadeId,
        IReadOnlyCollection<PerfilAcesso> perfis,
        CancellationToken cancellationToken)
    {
        var perfisConsultados = PerfisDistintos(perfis);

        return VinculosAtivos(usuarioId).AnyAsync(
            vinculo => vinculo.OrganizacaoId == organizacaoId
                && vinculo.UnidadeId == unidadeId
                && perfisConsultados.Contains(vinculo.Perfil),
            cancellationToken);
    }

    private IQueryable<VinculoAcesso> VinculosAtivos(Guid usuarioId)
    {
        return dbContext.VinculosAcesso
            .AsNoTracking()
            .Where(vinculo => vinculo.UsuarioId == usuarioId && vinculo.Ativo);
    }

    private static PerfilAcesso[] PerfisDistintos(IReadOnlyCollection<PerfilAcesso> perfis)
    {
        ArgumentNullException.ThrowIfNull(perfis);
        return [.. perfis.Distinct()];
    }
}
