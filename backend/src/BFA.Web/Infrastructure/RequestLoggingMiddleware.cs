using System.Diagnostics;
using BFA.Web.Authorization;

namespace BFA.Web.Infrastructure;

public sealed class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var method = context.Request.Method;
        var path = context.Request.Path.Value ?? "/";
        var queryString = context.Request.QueryString.Value ?? "";

        var stopwatch = Stopwatch.StartNew();

        try
        {
            await next(context);
        }
        finally
        {
            stopwatch.Stop();
            var statusCode = context.Response.StatusCode;
            var durationMs = stopwatch.ElapsedMilliseconds;

            var logLevel = statusCode switch
            {
                >= 500 => LogLevel.Error,
                >= 400 => LogLevel.Warning,
                _ => LogLevel.Information
            };

            var userId = context.User?.Identity?.IsAuthenticated == true
                ? context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                : null;

            Guid? unidadeId = null;
            Guid? organizacaoId = null;
            string? usuarioNome = null;
            string? unidadeNome = null;
            string? organizacaoNome = null;

            if (context.Items.TryGetValue("LogContextoUnidade", out var ctxObj)
                && ctxObj is ContextoUnidade contextoUnidade)
            {
                unidadeId = contextoUnidade.UnidadeId;
                organizacaoId = contextoUnidade.OrganizacaoId;
            }

            context.Items.TryGetValue("LogUsuarioNome", out var uNomeObj);
            usuarioNome = uNomeObj as string;

            context.Items.TryGetValue("LogUnidadeNome", out var unNomeObj);
            unidadeNome = unNomeObj as string;

            context.Items.TryGetValue("LogOrganizacaoNome", out var oNomeObj);
            organizacaoNome = oNomeObj as string;

            if (userId is not null && unidadeId is not null && organizacaoId is not null)
            {
                logger.Log(logLevel,
                    "{Method} {Path}{QueryString} respondido {StatusCode} em {Duration}ms [Usuario: {UsuarioId} ({UsuarioNome}), Unidade: {UnidadeId} ({UnidadeNome}), Organizacao: {OrganizacaoId} ({OrganizacaoNome})]",
                    method, path, queryString, statusCode, durationMs,
                    userId, usuarioNome ?? userId,
                    unidadeId, unidadeNome ?? unidadeId.ToString(),
                    organizacaoId, organizacaoNome ?? organizacaoId.ToString());
            }
            else if (userId is not null)
            {
                logger.Log(logLevel,
                    "{Method} {Path}{QueryString} respondido {StatusCode} em {Duration}ms [Usuario: {UsuarioId}]",
                    method, path, queryString, statusCode, durationMs, userId);
            }
            else
            {
                logger.Log(logLevel,
                    "{Method} {Path}{QueryString} respondido {StatusCode} em {Duration}ms",
                    method, path, queryString, statusCode, durationMs);
            }
        }
    }
}
