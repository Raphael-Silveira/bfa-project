using System.Diagnostics;

namespace BFA.Web.Infrastructure;

public sealed class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var method = context.Request.Method;
        var path = context.Request.Path.Value ?? "/";
        var queryString = context.Request.QueryString.Value ?? "";
        var userId = context.User?.Identity?.IsAuthenticated == true
            ? context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            : null;

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
            var elapsed = TimeSpan.FromMilliseconds(durationMs);

            var logLevel = statusCode switch
            {
                >= 500 => LogLevel.Error,
                >= 400 => LogLevel.Warning,
                _ => LogLevel.Information
            };

            if (userId is not null)
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
