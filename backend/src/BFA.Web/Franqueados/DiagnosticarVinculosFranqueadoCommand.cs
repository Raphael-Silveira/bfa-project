using BFA.Application.Franqueadora.Franqueados;

namespace BFA.Web.Franqueados;

public sealed class DiagnosticarVinculosFranqueadoCommand(
    IHostEnvironment environment,
    IDiagnosticoVinculosFranqueadoConsulta consulta)
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
            await erro.WriteLineAsync(
                "O diagnóstico de vínculos somente pode ser executado em Development.");
            return 1;
        }

        try
        {
            var resultado = await consulta.DiagnosticarAsync(cancellationToken);

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
            await erro.WriteLineAsync("Diagnóstico de vínculos cancelado.");
            return 1;
        }
        catch (Exception)
        {
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
