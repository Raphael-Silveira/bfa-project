using System.Security.Claims;
using BFA.Application.Acessos;

namespace BFA.Web.Acessos;

public sealed class UsuarioAtual(IHttpContextAccessor httpContextAccessor) : IUsuarioAtual
{
    public bool Autenticado =>
        httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated == true;

    public Guid? UsuarioId
    {
        get
        {
            var usuario = httpContextAccessor.HttpContext?.User;

            if (usuario?.Identity?.IsAuthenticated != true)
            {
                return null;
            }

            var identificador = usuario.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            return Guid.TryParse(identificador, out var usuarioId)
                ? usuarioId
                : null;
        }
    }
}
