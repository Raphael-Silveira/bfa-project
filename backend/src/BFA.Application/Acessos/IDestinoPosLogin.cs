namespace BFA.Application.Acessos;

public enum DestinoAcesso
{
    Padrao = 1,
    AdministradorRede = 2
}

public interface IDestinoPosLogin
{
    Task<DestinoAcesso> ObterAsync(
        Guid usuarioId,
        CancellationToken cancellationToken);
}
