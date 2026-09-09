using BFA.Application.Acessos;
using BFA.Application.Unidades;
using BFA.Web.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Claims;

namespace BFA.Web.Infrastructure;

public sealed class LogEnrichmentActionFilter(
    IUsuarioAtual usuarioAtual,
    IUnidadeContextoConsulta unidadeContextoConsulta,
    IMemoryCache memoryCache,
    ILogger<LogEnrichmentActionFilter> logger) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        if (!usuarioAtual.Autenticado || usuarioAtual.UsuarioId is not { } usuarioId)
        {
            await next();
            return;
        }

        var routeUnidadeId = context.RouteData.Values["unidadeId"]?.ToString()
                          ?? context.HttpContext.Request.Query["unidadeId"].FirstOrDefault();

        if (!Guid.TryParse(routeUnidadeId, out var unidadeId))
        {
            await next();
            return;
        }

        var cacheKey = $"unidade:{unidadeId}";
        if (!memoryCache.TryGetValue(cacheKey, out UnidadeContextoResumo? unidade))
        {
            unidade = await unidadeContextoConsulta.ObterAtivaAsync(unidadeId, context.HttpContext.RequestAborted);
            if (unidade is not null)
            {
                memoryCache.Set(cacheKey, unidade, new MemoryCacheEntryOptions
                {
                    SlidingExpiration = TimeSpan.FromMinutes(5)
                });
            }
        }

        if (unidade is null)
        {
            await next();
            return;
        }

        var contextoUnidade = new ContextoUnidade(unidade.OrganizacaoId, unidade.UnidadeId);
        context.HttpContext.Items["LogContextoUnidade"] = contextoUnidade;

        // UsuarioNome: tenta obter do claim 'name' (sem query extra)
        var usuarioNome = context.HttpContext.User?.FindFirst(ClaimTypes.Name)?.Value
                       ?? context.HttpContext.User?.FindFirst("name")?.Value
                       ?? usuarioId.ToString();

        // Armazena também os nomes para o middleware ler
        context.HttpContext.Items["LogUsuarioNome"] = usuarioNome;
        context.HttpContext.Items["LogUnidadeNome"] = unidade.Nome;
        context.HttpContext.Items["LogOrganizacaoNome"] = unidade.OrganizacaoNome;

        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["UsuarioId"] = usuarioId,
            ["UsuarioNome"] = usuarioNome,
            ["UnidadeId"] = unidade.UnidadeId,
            ["UnidadeNome"] = unidade.Nome,
            ["OrganizacaoId"] = unidade.OrganizacaoId,
            ["OrganizacaoNome"] = unidade.OrganizacaoNome
        });

        await next();
    }
}