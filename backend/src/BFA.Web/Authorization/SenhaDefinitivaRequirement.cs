using BFA.Application.Identidade;
using Microsoft.AspNetCore.Authorization;

namespace BFA.Web.Authorization;

public sealed class SenhaDefinitivaRequirement : IAuthorizationRequirement;

public sealed class SenhaDefinitivaHandler(
    ILogger<SenhaDefinitivaHandler> logger)
    : AuthorizationHandler<SenhaDefinitivaRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        SenhaDefinitivaRequirement requirement)
    {
        if (!context.User.HasClaim(IdentidadeClaims.TrocaSenhaObrigatoria, "true"))
        {
            context.Succeed(requirement);
        }
        else
        {
            logger.LogDebug("Acesso negado até a troca obrigatória de senha");
        }

        return Task.CompletedTask;
    }
}
