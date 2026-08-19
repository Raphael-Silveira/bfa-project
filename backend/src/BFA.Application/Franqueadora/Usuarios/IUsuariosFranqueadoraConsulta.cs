namespace BFA.Application.Franqueadora.Usuarios;

public interface IUsuariosFranqueadoraConsulta
{
    Task<ResultadoUsuariosFranqueadora<IReadOnlyList<UsuarioFranqueadoraResumo>>> ListarAsync(
        Guid usuarioAtualId,
        CancellationToken cancellationToken);

    Task<ResultadoUsuariosFranqueadora<IReadOnlyList<UnidadeSelecaoUsuarioResumo>>> ListarUnidadesAsync(
        Guid usuarioAtualId,
        CancellationToken cancellationToken);

    Task<ResultadoUsuariosFranqueadora<UsuarioFranqueadoraEdicao>> ObterEdicaoAsync(
        Guid usuarioAtualId,
        Guid usuarioId,
        CancellationToken cancellationToken);
}
