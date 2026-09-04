using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BFA.Application.Localidades;
using Microsoft.Extensions.Logging;

namespace BFA.Infrastructure.Localidades;

public sealed class IbgeLocalidadesClient(HttpClient httpClient, ILogger<IbgeLocalidadesClient> logger) : IIbgeLocalidadesClient
{
    public async Task<IReadOnlyList<EstadoIbgeDados>> ListarEstadosAsync(
        CancellationToken cancellationToken)
    {
        var estados = await ObterAsync<EstadoIbgeResposta>(
            "estados",
            "Estados",
            cancellationToken);

        return estados
            .Select(estado => new EstadoIbgeDados(estado.Id, estado.Sigla, estado.Nome))
            .ToArray();
    }

    public async Task<IReadOnlyList<MunicipioIbgeDados>> ListarMunicipiosAsync(
        string siglaEstado,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(siglaEstado))
        {
            throw new ArgumentException("A sigla do Estado deve ser informada.", nameof(siglaEstado));
        }

        var sigla = Uri.EscapeDataString(siglaEstado.Trim().ToUpperInvariant());
        var municipios = await ObterAsync<MunicipioIbgeResposta>(
            $"estados/{sigla}/municipios",
            "Municípios",
            cancellationToken);

        return municipios
            .Select(municipio => new MunicipioIbgeDados(municipio.Id, municipio.Nome))
            .ToArray();
    }

    private async Task<IReadOnlyList<T>> ObterAsync<T>(
        string caminho,
        string recurso,
        CancellationToken cancellationToken)
    {
        logger.LogDebug("Consultando IBGE: {Recurso} em {Caminho}", recurso, caminho);

        try
        {
            using var resposta = await httpClient.GetAsync(
                caminho,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (!resposta.IsSuccessStatusCode)
            {
                logger.LogError(
                    "IBGE retornou status {StatusCode} ao obter {Recurso}",
                    resposta.StatusCode, recurso);
                throw new IbgeLocalidadesException(
                    $"Não foi possível obter {recurso} no serviço de localidades do IBGE.");
            }

            var itens = await resposta.Content.ReadFromJsonAsync<T[]>(
                cancellationToken: cancellationToken);

            if (itens is null || itens.Length == 0)
            {
                throw new IbgeLocalidadesException(
                    $"O serviço de localidades do IBGE retornou {recurso} em formato vazio ou inválido.");
            }

            return itens;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (IbgeLocalidadesException)
        {
            throw;
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(exception,
                "Falha de conexao ao obter {Recurso} no IBGE", recurso);
            throw new IbgeLocalidadesException(
                $"Não foi possível acessar {recurso} no serviço de localidades do IBGE.",
                exception);
        }
        catch (OperationCanceledException exception)
        {
            logger.LogError(exception,
                "Timeout ao obter {Recurso} no IBGE", recurso);
            throw new IbgeLocalidadesException(
                $"O tempo limite para obter {recurso} no serviço de localidades do IBGE foi excedido.",
                exception);
        }
        catch (JsonException exception)
        {
            logger.LogError(exception,
                "Resposta invalida ao obter {Recurso} no IBGE", recurso);
            throw new IbgeLocalidadesException(
                $"O serviço de localidades do IBGE retornou {recurso} em formato inválido.",
                exception);
        }
        catch (NotSupportedException exception)
        {
            logger.LogError(exception,
                "Formato nao suportado ao obter {Recurso} no IBGE", recurso);
            throw new IbgeLocalidadesException(
                $"O serviço de localidades do IBGE retornou {recurso} em formato inválido.",
                exception);
        }
    }

    private sealed record EstadoIbgeResposta(
        [property: JsonPropertyName("id")] int Id,
        [property: JsonPropertyName("sigla")] string Sigla,
        [property: JsonPropertyName("nome")] string Nome);

    private sealed record MunicipioIbgeResposta(
        [property: JsonPropertyName("id")] int Id,
        [property: JsonPropertyName("nome")] string Nome);
}
