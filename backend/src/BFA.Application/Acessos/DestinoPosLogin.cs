using BFA.Application.Unidades;

namespace BFA.Application.Acessos;

public sealed class DestinoPosLogin(
    IAcessoUsuarioConsulta acessoUsuarioConsulta,
    IUnidadesUsuarioConsulta unidadesUsuarioConsulta)
    : IDestinoPosLogin
{
    public async Task<DestinoPosLoginResultado> ObterAsync(
        Guid usuarioId,
        CancellationToken cancellationToken)
    {
        if (usuarioId == Guid.Empty)
        {
            return new(DestinoAcesso.SemAcesso);
        }

        if (await acessoUsuarioConsulta.EhAdministradorRedeAsync(
                usuarioId,
                cancellationToken))
        {
            return new(DestinoAcesso.AdministradorRede);
        }

        var unidades = await unidadesUsuarioConsulta.ListarAdministradasAsync(
            usuarioId,
            cancellationToken);

        return unidades.Count switch
        {
            0 => new(DestinoAcesso.SemAcesso),
            1 => new(DestinoAcesso.Unidade, unidades[0].UnidadeId),
            _ => new(DestinoAcesso.SelecionarUnidade)
        };
    }
}
