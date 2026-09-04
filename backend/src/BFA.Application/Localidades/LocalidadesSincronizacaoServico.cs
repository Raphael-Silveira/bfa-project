using Microsoft.Extensions.Logging;

namespace BFA.Application.Localidades;

public sealed class LocalidadesSincronizacaoServico(
    IIbgeLocalidadesClient ibgeClient,
    ILocalidadesSincronizacaoRepositorio repositorio,
    TimeProvider timeProvider,
    ILogger<LocalidadesSincronizacaoServico> logger) : ILocalidadesSincronizacaoServico
{
    public async Task<LocalidadesSincronizacaoResultado> SincronizarAsync(
        CancellationToken cancellationToken)
    {
        var estadosRemotos = await ibgeClient.ListarEstadosAsync(cancellationToken);
        var estados = CatalogoLocalidadesDados.NormalizarEstados(estadosRemotos.Select(
            estado => new EstadoCatalogoDados(
                estado.CodigoIbge,
                estado.Sigla,
                estado.Nome)));
        var municipios = new List<MunicipioCatalogoDados>();

        foreach (var estado in estados.OrderBy(estado => estado.CodigoIbge))
        {
            var municipiosRemotos = await ibgeClient.ListarMunicipiosAsync(
                estado.Sigla,
                cancellationToken);

            if (municipiosRemotos.Count == 0)
            {
                throw new LocalidadesSincronizacaoException(
                    $"O IBGE não retornou Municípios para o Estado {estado.Sigla}.");
            }

            municipios.AddRange(municipiosRemotos.Select(municipio =>
                new MunicipioCatalogoDados(
                    municipio.CodigoIbge,
                    estado.CodigoIbge,
                    municipio.Nome)));
        }

        var catalogo = CatalogoLocalidadesDados.Criar(estados, municipios);
        await repositorio.SincronizarAsync(
            catalogo,
            timeProvider.GetUtcNow().UtcDateTime,
            cancellationToken);

        logger.LogInformation(
            "SincronizarLocalidades concluído: {QtdEstados} estados, {QtdMunicipios} municípios",
            catalogo.Estados.Count,
            catalogo.Municipios.Count);

        return new LocalidadesSincronizacaoResultado(
            catalogo.Estados.Count,
            catalogo.Municipios.Count);
    }
}
