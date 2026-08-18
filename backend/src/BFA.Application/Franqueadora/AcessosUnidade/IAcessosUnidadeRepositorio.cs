using BFA.Domain.Acessos;

namespace BFA.Application.Franqueadora.AcessosUnidade;

public interface IAcessosUnidadeRepositorio
{
    Task<UnidadeAcessosResumo?> ObterUnidadeAsync(
        Guid organizacaoId,
        Guid unidadeId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<AdministradorUnidadeResumo>> ListarAdministradoresAsync(
        Guid organizacaoId,
        Guid unidadeId,
        CancellationToken cancellationToken);

    Task<VinculoAcesso?> ObterAdministradorPorUsuarioAsync(
        Guid organizacaoId,
        Guid unidadeId,
        Guid usuarioId,
        CancellationToken cancellationToken);

    Task<VinculoAcesso?> ObterAdministradorPorVinculoAsync(
        Guid organizacaoId,
        Guid unidadeId,
        Guid vinculoId,
        CancellationToken cancellationToken);

    void Adicionar(VinculoAcesso vinculo);

    Task<ResultadoPersistenciaAcessoUnidade> SalvarAsync(
        CancellationToken cancellationToken);
}
