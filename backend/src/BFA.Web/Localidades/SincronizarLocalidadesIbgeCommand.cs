using BFA.Application.Localidades;

namespace BFA.Web.Localidades;

public sealed class SincronizarLocalidadesIbgeCommand(
    ILocalidadesSincronizacaoServico sincronizacaoServico,
    ILogger<SincronizarLocalidadesIbgeCommand> logger)
{
    private const string Argumento = "--sincronizar-localidades-ibge";

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

        try
        {
            logger.LogInformation("Sincronização de localidades do IBGE iniciada");
            var resultado = await sincronizacaoServico.SincronizarAsync(cancellationToken);
            logger.LogInformation(
                "Sincronização de localidades do IBGE concluída: {Estados} estados, {Municipios} municípios",
                resultado.EstadosProcessados, resultado.MunicipiosProcessados);
            await saida.WriteLineAsync("Sincronização de localidades do IBGE concluída.");
            await saida.WriteLineAsync($"Estados processados: {resultado.EstadosProcessados}.");
            await saida.WriteLineAsync($"Municípios processados: {resultado.MunicipiosProcessados}.");
            return 0;
        }
        catch (LocalidadesSincronizacaoException exception)
        {
            logger.LogWarning(exception, "Sincronização de localidades não executada");
            await erro.WriteLineAsync(
                $"Sincronização de localidades não executada: {exception.Message}");
            return 1;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Sincronização de localidades cancelada");
            await erro.WriteLineAsync("Sincronização de localidades cancelada.");
            return 1;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Sincronização de localidades falhou");
            await erro.WriteLineAsync(
                "Sincronização de localidades falhou. Nenhum detalhe interno foi exibido.");
            return 1;
        }
    }
}
