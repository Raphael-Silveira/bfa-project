using BFA.Application.Acessos;
using Microsoft.AspNetCore.Authorization;

namespace BFA.Web.Authorization;

public sealed class AdministradorRedeHandler(
    IUsuarioAtual usuarioAtual,
    IAcessoUsuarioConsulta acessoUsuarioConsulta,
    ILogger<AdministradorRedeHandler> logger)
    : AuthorizationHandler<AdministradorRedeRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AdministradorRedeRequirement requirement)
    {
        if (!usuarioAtual.Autenticado || usuarioAtual.UsuarioId is not { } usuarioId)
        {
            return;
        }

        if (await acessoUsuarioConsulta.EhAdministradorRedeAsync(
                usuarioId,
                CancellationToken.None))
        {
            logger.LogDebug(
                "AdministradorRedeHandler: Concedido para {UsuarioId}", usuarioId);
            context.Succeed(requirement);
        }
        else
        {
            logger.LogDebug(
                "AdministradorRedeHandler: Negado para {UsuarioId}", usuarioId);
        }
    }
}
