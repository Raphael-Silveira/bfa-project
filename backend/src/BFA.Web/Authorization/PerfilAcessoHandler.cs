using BFA.Application.Acessos;
using Microsoft.AspNetCore.Authorization;

namespace BFA.Web.Authorization;

public sealed class PerfilAcessoHandler(
    IUsuarioAtual usuarioAtual,
    IAcessoUsuarioConsulta acessoUsuarioConsulta)
    : AuthorizationHandler<PerfilAcessoRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PerfilAcessoRequirement requirement)
    {
        if (!usuarioAtual.Autenticado || usuarioAtual.UsuarioId is not { } usuarioId)
        {
            return;
        }

        if (await acessoUsuarioConsulta.PossuiAlgumPerfilAsync(
                usuarioId,
                requirement.PerfisPermitidos,
                CancellationToken.None))
        {
            context.Succeed(requirement);
        }
    }
}
