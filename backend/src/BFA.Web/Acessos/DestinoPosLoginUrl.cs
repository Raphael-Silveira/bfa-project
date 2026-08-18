using BFA.Application.Acessos;

namespace BFA.Web.Acessos;

public static class DestinoPosLoginUrl
{
    public static string Obter(DestinoAcesso destino)
    {
        return destino switch
        {
            DestinoAcesso.AdministradorRede => "/franqueadora",
            DestinoAcesso.Padrao => "/",
            _ => throw new ArgumentOutOfRangeException(nameof(destino), destino, null)
        };
    }
}
