namespace BFA.Application.Franqueadora.Usuarios;

public interface IUsuariosFranqueadoraRepositorio
{
    Task<IReadOnlyList<UsuarioFranqueadoraResumo>> ListarAsync(
        Guid organizacaoId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<UnidadeSelecaoUsuarioResumo>> ListarUnidadesAtivasAsync(
        Guid organizacaoId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Guid>> ListarUnidadesValidasAsync(
        Guid organizacaoId,
        IReadOnlyCollection<Guid> unidadesIds,
        CancellationToken cancellationToken);

    Task<string?> ObterUnidadeComFranqueadoAtivoAsync(
        Guid organizacaoId,
        IReadOnlyCollection<Guid> unidadesIds,
        CancellationToken cancellationToken);

    Task<bool> ExisteUsuarioPorEmailAsync(
        string email,
        CancellationToken cancellationToken);

    Task<bool> ExisteFranqueadoPorDocumentoAsync(
        Guid organizacaoId,
        string documento,
        CancellationToken cancellationToken);

    Task<ResultadoPersistenciaCadastroUsuario> CriarAsync(
        CadastroUsuarioFranqueadora cadastro,
        CancellationToken cancellationToken);

    Task<UsuarioFranqueadoraEdicaoContexto?> ObterEdicaoAsync(
        Guid usuarioId,
        CancellationToken cancellationToken);

    Task<ResultadoPersistenciaEdicaoUsuario> AtualizarAsync(
        AtualizarUsuarioFranqueadoraDados dados,
        CancellationToken cancellationToken);
}
