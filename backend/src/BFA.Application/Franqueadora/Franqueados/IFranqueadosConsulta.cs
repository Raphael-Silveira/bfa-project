namespace BFA.Application.Franqueadora.Franqueados;

public interface IFranqueadosConsulta
{
    Task<ResultadoFranqueado<IReadOnlyList<FranqueadoResumo>>> ListarAsync(
        Guid usuarioAtualId,
        CancellationToken cancellationToken);

    Task<ResultadoFranqueado<FranqueadoDetalhe>> ObterAsync(
        Guid usuarioAtualId,
        Guid franqueadoId,
        CancellationToken cancellationToken);
}
