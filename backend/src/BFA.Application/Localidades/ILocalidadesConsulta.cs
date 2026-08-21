namespace BFA.Application.Localidades;

public interface ILocalidadesConsulta
{
    Task<IReadOnlyList<EstadoLocalidadeResumo>> ListarEstadosAtivosAsync(
        CancellationToken cancellationToken);

    Task<IReadOnlyList<MunicipioLocalidadeResumo>> ListarMunicipiosAtivosAsync(
        int estadoCodigoIbge,
        CancellationToken cancellationToken);
}
