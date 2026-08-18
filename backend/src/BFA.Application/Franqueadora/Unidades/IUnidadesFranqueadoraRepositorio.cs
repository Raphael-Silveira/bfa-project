using BFA.Domain.Unidades;

namespace BFA.Application.Franqueadora.Unidades;

public interface IUnidadesFranqueadoraRepositorio
{
    Task<IReadOnlyList<UnidadeResumo>> ListarAsync(
        Guid organizacaoId,
        CancellationToken cancellationToken);

    Task<UnidadeDetalhe?> ObterDetalheAsync(
        Guid organizacaoId,
        Guid unidadeId,
        CancellationToken cancellationToken);

    Task<Unidade?> ObterParaAlteracaoAsync(
        Guid organizacaoId,
        Guid unidadeId,
        CancellationToken cancellationToken);

    Task<bool> ExisteSlugAsync(
        Guid organizacaoId,
        string slug,
        Guid? unidadeIgnoradaId,
        CancellationToken cancellationToken);

    void Adicionar(Unidade unidade);

    Task<ResultadoPersistenciaUnidade> SalvarAsync(CancellationToken cancellationToken);
}
