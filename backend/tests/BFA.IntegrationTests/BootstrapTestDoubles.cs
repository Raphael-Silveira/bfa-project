using BFA.Application.Bootstrap;

namespace BFA.IntegrationTests;

public sealed class TestBootstrapInicial : IBootstrapInicial
{
    public int Execucoes { get; private set; }

    public BootstrapInicialSolicitacao? UltimaSolicitacao { get; private set; }

    public BootstrapInicialResultado Resultado { get; set; } = new(
        true,
        [
            new AdministradorBootstrapResultado(1, true, true),
            new AdministradorBootstrapResultado(2, true, true)
        ]);

    public Task<BootstrapInicialResultado> ExecutarAsync(
        BootstrapInicialSolicitacao solicitacao,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Execucoes++;
        UltimaSolicitacao = solicitacao;
        return Task.FromResult(Resultado);
    }
}
