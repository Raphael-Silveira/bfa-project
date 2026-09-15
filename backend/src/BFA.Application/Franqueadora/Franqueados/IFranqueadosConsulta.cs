using BFA.Application;

namespace BFA.Application.Franqueadora.Franqueados;

public interface IFranqueadosConsulta
{
    Task<ResultadoFranqueado<PaginaResultado<FranqueadoResumo>>> ListarAsync(
        Guid usuarioAtualId,
        string? busca,
        int pagina,
        int tamanhoPagina,
        CancellationToken cancellationToken);

    Task<ResultadoFranqueado<FranqueadoDetalhe>> ObterAsync(
        Guid usuarioAtualId,
        Guid franqueadoId,
        CancellationToken cancellationToken);
}
