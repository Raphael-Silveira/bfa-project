using BFA.Application.Acessos;
using Microsoft.AspNetCore.Authorization;

namespace BFA.Web.Authorization;

public sealed class AcessoUnidadePorPerfilHandler(
    IUsuarioAtual usuarioAtual,
    IAcessoUsuarioConsulta acessoUsuarioConsulta,
    ILogger<AcessoUnidadePorPerfilHandler> logger)
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
            logger.LogDebug(
                "AcessoUnidadePorPerfilHandler: Concedido para {UsuarioId} na Unidade {UnidadeId}",
                usuarioId, resource.UnidadeId);
            context.Succeed(requirement);
        }
        else
        {
            logger.LogDebug(
                "AcessoUnidadePorPerfilHandler: Negado para {UsuarioId} na Unidade {UnidadeId}",
                usuarioId, resource.UnidadeId);
        }
    }
}
