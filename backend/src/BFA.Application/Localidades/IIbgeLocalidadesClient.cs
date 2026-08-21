namespace BFA.Application.Localidades;

public interface IIbgeLocalidadesClient
{
    Task<IReadOnlyList<EstadoIbgeDados>> ListarEstadosAsync(
        CancellationToken cancellationToken);

    Task<IReadOnlyList<MunicipioIbgeDados>> ListarMunicipiosAsync(
        string siglaEstado,
        CancellationToken cancellationToken);
}
