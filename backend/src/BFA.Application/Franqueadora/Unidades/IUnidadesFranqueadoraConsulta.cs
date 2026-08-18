namespace BFA.Application.Franqueadora.Unidades;

public interface IUnidadesFranqueadoraConsulta
{
    Task<ResultadoUnidadesFranqueadora<IReadOnlyList<UnidadeResumo>>> ListarAsync(
        Guid usuarioId,
        CancellationToken cancellationToken);

    Task<ResultadoUnidadesFranqueadora<UnidadeDetalhe>> ObterAsync(
        Guid usuarioId,
        Guid unidadeId,
        CancellationToken cancellationToken);
}
