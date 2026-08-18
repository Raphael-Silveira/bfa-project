namespace BFA.Application.Bootstrap;

public interface IBootstrapInicial
{
    Task<BootstrapInicialResultado> ExecutarAsync(
        BootstrapInicialSolicitacao solicitacao,
        CancellationToken cancellationToken);
}
