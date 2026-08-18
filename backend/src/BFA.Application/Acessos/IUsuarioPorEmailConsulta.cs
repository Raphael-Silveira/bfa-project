namespace BFA.Application.Acessos;

public interface IUsuarioPorEmailConsulta
{
    Task<UsuarioPorEmail?> ObterAsync(
        string email,
        CancellationToken cancellationToken);
}

public sealed record UsuarioPorEmail(Guid Id, string Email);
