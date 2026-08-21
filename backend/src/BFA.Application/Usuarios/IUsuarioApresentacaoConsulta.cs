namespace BFA.Application.Usuarios;

public interface IUsuarioApresentacaoConsulta
{
    Task<string?> ObterNomeCompletoAsync(
        Guid usuarioId,
        CancellationToken cancellationToken);
}
