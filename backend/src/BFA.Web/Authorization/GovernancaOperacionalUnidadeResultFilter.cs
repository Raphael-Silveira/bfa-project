using BFA.Application.Acessos;
using BFA.Application.Unidades;
using BFA.Web.ViewModels.Unidade;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace BFA.Web.Authorization;

public sealed class GovernancaOperacionalUnidadeResultFilter(
    IUsuarioAtual usuarioAtual,
    IUnidadeContextoConsulta unidadeContextoConsulta,
    IGovernancaOperacionalUnidade governancaOperacional)
    : IAsyncResultFilter
{
    public async Task OnResultExecutionAsync(
        ResultExecutingContext context,
        ResultExecutionDelegate next)
    {
        if (context.Result is ViewResult viewResult
            && usuarioAtual.UsuarioId is { } usuarioId
            && Guid.TryParse(
                context.RouteData.Values["unidadeId"]?.ToString(), out var unidadeId))
        {
            var unidade = await unidadeContextoConsulta.ObterAtivaAsync(
                unidadeId, context.HttpContext.RequestAborted);
            if (unidade is not null)
            {
                var governanca = await governancaOperacional.ObterAsync(
                    usuarioId,
                    unidade.OrganizacaoId,
                    unidadeId,
                    context.HttpContext.RequestAborted);
                if (governanca.PodeAcessar)
                {
                    viewResult.ViewData[GovernancaOperacionalUnidadeViewData.Chave] =
                        new GovernancaOperacionalUnidadeViewModel(
                            governanca.EhAdministradorRede,
                            governanca.PossuiFranqueadoAtivo,
                            governanca.PodeGerenciarTurmas);
                }
            }
        }

        await next();
    }
}
