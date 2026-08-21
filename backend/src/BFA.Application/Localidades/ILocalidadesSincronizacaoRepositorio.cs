namespace BFA.Application.Localidades;

public interface ILocalidadesSincronizacaoRepositorio
{
    Task SincronizarAsync(
        CatalogoLocalidadesDados catalogo,
        DateTime atualizadoEmUtc,
        CancellationToken cancellationToken);
}
