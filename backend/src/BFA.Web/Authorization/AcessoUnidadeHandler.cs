using BFA.Application.Acessos;
using Microsoft.AspNetCore.Authorization;

namespace BFA.Web.Authorization;

public sealed class AcessoUnidadeHandler(
    IUsuarioAtual usuarioAtual,
    IAcessoUsuarioConsulta acessoUsuarioConsulta)
    : AuthorizationHandler<AcessoUnidadeRequirement, ContextoUnidade>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AcessoUnidadeRequirement requirement,
        ContextoUnidade resource)
    {
        if (!usuarioAtual.Autenticado || usuarioAtual.UsuarioId is not { } usuarioId)
        {
            return;
        }

        if (await acessoUsuarioConsulta.PossuiAcessoUnidadeAsync(
                usuarioId,
                resource.OrganizacaoId,
                resource.UnidadeId,
                CancellationToken.None))
        {
            context.Succeed(requirement);
        }
    }
}
