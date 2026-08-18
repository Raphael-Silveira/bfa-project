using BFA.Domain.Acessos;

namespace BFA.Application.Acessos;

public interface IAcessoUsuarioConsulta
{
    Task<bool> EhAdministradorRedeAsync(
        Guid usuarioId,
        CancellationToken cancellationToken);

    Task<bool> EhAdministradorRedeNaOrganizacaoAsync(
        Guid usuarioId,
        Guid organizacaoId,
        CancellationToken cancellationToken);

    Task<bool> PossuiAlgumPerfilAsync(
        Guid usuarioId,
        IReadOnlyCollection<PerfilAcesso> perfis,
        CancellationToken cancellationToken);

    Task<bool> PossuiPerfilNaOrganizacaoAsync(
        Guid usuarioId,
        Guid organizacaoId,
        PerfilAcesso perfil,
        CancellationToken cancellationToken);

    Task<bool> PossuiAcessoUnidadeAsync(
        Guid usuarioId,
        Guid organizacaoId,
        Guid unidadeId,
        CancellationToken cancellationToken);

    Task<bool> PossuiPerfilNaUnidadeAsync(
        Guid usuarioId,
        Guid organizacaoId,
        Guid unidadeId,
        PerfilAcesso perfil,
        CancellationToken cancellationToken);

    Task<bool> PossuiAlgumPerfilNaUnidadeAsync(
        Guid usuarioId,
        Guid organizacaoId,
        Guid unidadeId,
        IReadOnlyCollection<PerfilAcesso> perfis,
        CancellationToken cancellationToken);
}
