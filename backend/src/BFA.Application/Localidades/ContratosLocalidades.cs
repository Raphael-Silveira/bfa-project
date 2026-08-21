using BFA.Domain.Localidades;

namespace BFA.Application.Localidades;

public sealed record EstadoLocalidadeResumo(int CodigoIbge, string Sigla, string Nome);

public sealed record MunicipioLocalidadeResumo(int CodigoIbge, string Nome);

public sealed record EstadoIbgeDados(int CodigoIbge, string Sigla, string Nome);

public sealed record MunicipioIbgeDados(int CodigoIbge, string Nome);

public sealed record EstadoCatalogoDados(int CodigoIbge, string Sigla, string Nome);

public sealed record MunicipioCatalogoDados(
    int CodigoIbge,
    int EstadoCodigoIbge,
    string Nome);

public sealed record LocalidadesSincronizacaoResultado(
    int EstadosProcessados,
    int MunicipiosProcessados);

public sealed class CatalogoLocalidadesDados
{
    private CatalogoLocalidadesDados(
        IReadOnlyList<EstadoCatalogoDados> estados,
        IReadOnlyList<MunicipioCatalogoDados> municipios)
    {
        Estados = estados;
        Municipios = municipios;
    }

    public IReadOnlyList<EstadoCatalogoDados> Estados { get; }

    public IReadOnlyList<MunicipioCatalogoDados> Municipios { get; }

    public static IReadOnlyList<EstadoCatalogoDados> NormalizarEstados(
        IEnumerable<EstadoCatalogoDados> estados)
    {
        ArgumentNullException.ThrowIfNull(estados);
        EstadoCatalogoDados[] normalizados;

        try
        {
            normalizados = estados
                .Select(estado =>
                {
                    var entidade = new Estado(
                        estado.CodigoIbge,
                        estado.Sigla,
                        estado.Nome,
                        DateTime.UnixEpoch);
                    return new EstadoCatalogoDados(
                        entidade.CodigoIbge,
                        entidade.Sigla,
                        entidade.Nome);
                })
                .ToArray();
        }
        catch (ArgumentException exception)
        {
            throw new LocalidadesSincronizacaoException(
                "O IBGE retornou dados inválidos para Estados.",
                exception);
        }

        if (normalizados.Length == 0)
        {
            throw new LocalidadesSincronizacaoException(
                "O IBGE não retornou Estados para a sincronização.");
        }

        if (normalizados.Select(estado => estado.CodigoIbge).Distinct().Count()
            != normalizados.Length)
        {
            throw new LocalidadesSincronizacaoException(
                "O IBGE retornou códigos de Estado duplicados.");
        }

        if (normalizados.Select(estado => estado.Sigla)
                .Distinct(StringComparer.Ordinal)
                .Count()
            != normalizados.Length)
        {
            throw new LocalidadesSincronizacaoException(
                "O IBGE retornou siglas de Estado duplicadas.");
        }

        return normalizados;
    }

    public static CatalogoLocalidadesDados Criar(
        IEnumerable<EstadoCatalogoDados> estados,
        IEnumerable<MunicipioCatalogoDados> municipios)
    {
        ArgumentNullException.ThrowIfNull(municipios);
        var estadosNormalizados = NormalizarEstados(estados);
        var estadosCodigos = estadosNormalizados
            .Select(estado => estado.CodigoIbge)
            .ToHashSet();
        MunicipioCatalogoDados[] municipiosNormalizados;

        try
        {
            municipiosNormalizados = municipios
                .Select(municipio =>
                {
                    if (!estadosCodigos.Contains(municipio.EstadoCodigoIbge))
                    {
                        throw new LocalidadesSincronizacaoException(
                            $"O Município {municipio.CodigoIbge} referencia um Estado ausente no lote.");
                    }

                    var entidade = new Municipio(
                        municipio.CodigoIbge,
                        municipio.EstadoCodigoIbge,
                        municipio.Nome,
                        DateTime.UnixEpoch);
                    return new MunicipioCatalogoDados(
                        entidade.CodigoIbge,
                        entidade.EstadoCodigoIbge,
                        entidade.Nome);
                })
                .ToArray();
        }
        catch (ArgumentException exception)
        {
            throw new LocalidadesSincronizacaoException(
                "O IBGE retornou dados inválidos para Municípios.",
                exception);
        }

        if (municipiosNormalizados.Length == 0)
        {
            throw new LocalidadesSincronizacaoException(
                "O IBGE não retornou Municípios para a sincronização.");
        }

        if (municipiosNormalizados.Select(municipio => municipio.CodigoIbge)
                .Distinct()
                .Count()
            != municipiosNormalizados.Length)
        {
            throw new LocalidadesSincronizacaoException(
                "O IBGE retornou códigos de Município duplicados.");
        }

        return new CatalogoLocalidadesDados(estadosNormalizados, municipiosNormalizados);
    }
}
