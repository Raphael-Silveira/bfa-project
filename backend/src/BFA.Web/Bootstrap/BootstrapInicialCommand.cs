using BFA.Application.Bootstrap;

namespace BFA.Web.Bootstrap;

public sealed class BootstrapInicialCommand(
    IHostEnvironment environment,
    IConfiguration configuration,
    IBootstrapInicial bootstrapInicial)
{
    private const string Argumento = "--bootstrap-inicial";

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
                "O bootstrap inicial somente pode ser executado em Development.");
            return 1;
        }

        try
        {
            var solicitacao = new BootstrapInicialSolicitacao(
                new CredenciaisAdministradorBootstrap(
                    ObterConfiguracaoObrigatoria("Bootstrap:Admin1:Email"),
                    ObterConfiguracaoObrigatoria("Bootstrap:Admin1:Password")),
                new CredenciaisAdministradorBootstrap(
                    ObterConfiguracaoObrigatoria("Bootstrap:Admin2:Email"),
                    ObterConfiguracaoObrigatoria("Bootstrap:Admin2:Password")));
            var resultado = await bootstrapInicial.ExecutarAsync(
                solicitacao,
                cancellationToken);

            await EscreverResultadoAsync(saida, resultado);
            return 0;
        }
        catch (BootstrapInicialException exception)
        {
            await erro.WriteLineAsync($"Bootstrap inicial não executado: {exception.Message}");
            return 1;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await erro.WriteLineAsync("Bootstrap inicial cancelado.");
            return 1;
        }
        catch (Exception)
        {
            await erro.WriteLineAsync(
                "Bootstrap inicial falhou. Nenhum detalhe sensível foi exibido.");
            return 1;
        }
    }

    private string ObterConfiguracaoObrigatoria(string chave)
    {
        var valor = configuration[chave];

        if (string.IsNullOrWhiteSpace(valor))
        {
            throw new BootstrapInicialException(
                $"Configuração obrigatória ausente: {chave}.");
        }

        return valor;
    }

    private static async Task EscreverResultadoAsync(
        TextWriter saida,
        BootstrapInicialResultado resultado)
    {
        await saida.WriteLineAsync(resultado.OrganizacaoCriada
            ? "Organização BFA criada."
            : "Organização BFA já existe.");

        foreach (var administrador in resultado.Administradores.OrderBy(item => item.Numero))
        {
            await saida.WriteLineAsync(administrador.UsuarioCriado
                ? $"Administrador {administrador.Numero} criado."
                : $"Administrador {administrador.Numero} já existe.");
            await saida.WriteLineAsync(administrador.VinculoCriado
                ? $"Administrador {administrador.Numero} vinculado como AdministradorRede."
                : $"Vínculo do Administrador {administrador.Numero} já existe.");
        }
    }
}
