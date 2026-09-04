using BFA.Application.Franqueadora.Franqueados;

namespace BFA.Web.Franqueados;

public sealed class DiagnosticarVinculosFranqueadoCommand(
    IHostEnvironment environment,
    IDiagnosticoVinculosFranqueadoConsulta consulta,
    ILogger<DiagnosticarVinculosFranqueadoCommand> logger)
{
    private const string Argumento = "--diagnosticar-vinculos-franqueados";

    public static bool Solicitado(IEnumerable<string> argumentos)
    {
        ArgumentNullException.ThrowIfNull(argumentos);

        return argumentos.Any(argumento =>
            string.Equals(argumento, Argumento, StringComparison.Ordinal));
    }

    public async Task<int> ExecutarAsync(
        TextWriter saida,
        TextWriter erro,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(saida);
        ArgumentNullException.ThrowIfNull(erro);

        if (!environment.IsDevelopment())
        {
            logger.LogWarning(
                "Diagnóstico de vínculos recusado: ambiente não é Development");
            await erro.WriteLineAsync(
                "O diagnóstico de vínculos somente pode ser executado em Development.");
            return 1;
        }

        try
        {
            logger.LogInformation("Diagnóstico de vínculos iniciado");
            var resultado = await consulta.DiagnosticarAsync(cancellationToken);

            logger.LogInformation("Diagnóstico somente leitura concluído");
            await saida.WriteLineAsync("Diagnóstico somente leitura concluído.");
            await EscreverGrupoAsync(
                saida,
                "Acessos administrativos sem vínculo comercial ativo",
                resultado.AcessosSemVinculoComercial);
            await EscreverGrupoAsync(
                saida,
                "Vínculos comerciais ativos sem acesso do usuário principal",
                resultado.VinculosComerciaisSemAcessoPrincipal);
            await saida.WriteLineAsync("Nenhum dado foi alterado.");
            return 0;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Diagnóstico de vínculos cancelado");
            await erro.WriteLineAsync("Diagnóstico de vínculos cancelado.");
            return 1;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Diagnóstico de vínculos falhou");
            await erro.WriteLineAsync(
                "Diagnóstico de vínculos falhou. Nenhum detalhe interno foi exibido.");
            return 1;
        }
    }

    private static async Task EscreverGrupoAsync(
        TextWriter saida,
        string titulo,
        IReadOnlyList<InconsistenciaVinculosFranqueado> inconsistencias)
    {
        await saida.WriteLineAsync($"{titulo}: {inconsistencias.Count}.");

        foreach (var item in inconsistencias)
        {
            await saida.WriteLineAsync(
                $"- Franqueado: {item.Franqueado} ({item.FranqueadoId}); "
                + $"usuário principal: {item.UsuarioPrincipalId}; "
                + $"unidade: {item.Unidade} ({item.UnidadeId}).");
        }
    }
}
