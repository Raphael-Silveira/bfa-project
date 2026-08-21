namespace BFA.Application.Localidades;

public interface ILocalidadesSincronizacaoServico
{
    Task<LocalidadesSincronizacaoResultado> SincronizarAsync(
        CancellationToken cancellationToken);
}
