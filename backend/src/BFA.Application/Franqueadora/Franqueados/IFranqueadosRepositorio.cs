using BFA.Domain.Acessos;
using BFA.Domain.Franqueados;

namespace BFA.Application.Franqueadora.Franqueados;

public interface IFranqueadosRepositorio
{
    Task<IReadOnlyList<FranqueadoResumo>> ListarAsync(
        Guid organizacaoId,
        CancellationToken cancellationToken);

    Task<FranqueadoDados?> ObterDadosAsync(
        Guid organizacaoId,
        Guid franqueadoId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<FranqueadoUsuarioResumo>> ListarUsuariosAsync(
        Guid organizacaoId,
        Guid franqueadoId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<FranqueadoUnidadeResumo>> ListarUnidadesAsync(
        Guid organizacaoId,
        Guid franqueadoId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<UnidadeDisponivelFranqueadoResumo>> ListarUnidadesDisponiveisAsync(
        Guid organizacaoId,
        CancellationToken cancellationToken);

    Task<Franqueado?> ObterParaAtualizacaoAsync(
        Guid organizacaoId,
        Guid franqueadoId,
        CancellationToken cancellationToken);

    Task<bool> ExisteDocumentoAsync(
        Guid organizacaoId,
        Guid franqueadoIdIgnorado,
        string documento,
        CancellationToken cancellationToken);

    Task<bool> UnidadeAtivaExisteAsync(
        Guid organizacaoId,
        Guid unidadeId,
        CancellationToken cancellationToken);

    Task<bool> UnidadePossuiOutroFranqueadoAtivoAsync(
        Guid organizacaoId,
        Guid franqueadoId,
        Guid unidadeId,
        CancellationToken cancellationToken);

    Task<FranqueadoUsuario?> ObterUsuarioPrincipalAtivoAsync(
        Guid franqueadoId,
        CancellationToken cancellationToken);

    Task<FranqueadoUnidade?> ObterVinculoUnidadeAsync(
        Guid organizacaoId,
        Guid franqueadoId,
        Guid unidadeId,
        CancellationToken cancellationToken);

    Task<VinculoAcesso?> ObterAcessoAdministradorUnidadeAsync(
        Guid organizacaoId,
        Guid unidadeId,
        Guid usuarioId,
        CancellationToken cancellationToken);

    void Adicionar(FranqueadoUnidade vinculo);

    void Adicionar(VinculoAcesso vinculo);

    Task<EstadoPersistenciaFranqueado> SalvarAsync(
        CancellationToken cancellationToken);
}
