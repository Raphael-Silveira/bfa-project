namespace BFA.Application.Acessos;

public sealed class DestinoPosLogin(IAcessoUsuarioConsulta acessoUsuarioConsulta)
    : IDestinoPosLogin
{
    public async Task<DestinoAcesso> ObterAsync(
        Guid usuarioId,
        CancellationToken cancellationToken)
    {
        if (usuarioId == Guid.Empty)
        {
            return DestinoAcesso.Padrao;
        }

        return await acessoUsuarioConsulta.EhAdministradorRedeAsync(
            usuarioId,
            cancellationToken)
            ? DestinoAcesso.AdministradorRede
            : DestinoAcesso.Padrao;
    }
}
