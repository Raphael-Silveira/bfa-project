using BFA.Application.Acessos;
using Microsoft.AspNetCore.Authorization;

namespace BFA.Web.Authorization;

public sealed class AcessoUnidadePorPerfilHandler(
    IUsuarioAtual usuarioAtual,
    IAcessoUsuarioConsulta acessoUsuarioConsulta)
    : AuthorizationHandler<AcessoUnidadePorPerfilRequirement, ContextoUnidade>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AcessoUnidadePorPerfilRequirement requirement,
        ContextoUnidade resource)
    {
        if (!usuarioAtual.Autenticado || usuarioAtual.UsuarioId is not { } usuarioId)
        {
            return;
        }

        if (await acessoUsuarioConsulta.EhAdministradorRedeNaOrganizacaoAsync(
                usuarioId,
                resource.OrganizacaoId,
                CancellationToken.None)
            || await acessoUsuarioConsulta.PossuiAlgumPerfilNaUnidadeAsync(
                usuarioId,
                resource.OrganizacaoId,
                resource.UnidadeId,
                requirement.PerfisPermitidos,
                CancellationToken.None))
        {
            context.Succeed(requirement);
        }
    }
}
