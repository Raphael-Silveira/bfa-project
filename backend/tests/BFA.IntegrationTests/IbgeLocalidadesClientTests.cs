using System.Net;
using System.Text;
using BFA.Application.Localidades;
using BFA.Infrastructure.Localidades;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace BFA.IntegrationTests;

public sealed class IbgeLocalidadesClientTests
{
    private static readonly Uri BaseAddress = new(
        "https://servicodados.ibge.gov.br/api/v1/localidades/");

    [Fact]
    public async Task Le_estados_no_contrato_minimo_e_rota_oficial()
    {
        var handler = new TestHttpMessageHandler(_ => Json(
            """
            [{"id":35,"sigla":"SP","nome":"São Paulo","regiao":{"id":3}}]
            """));
        var client = CreateClient(handler);

        var estados = await client.ListarEstadosAsync(CancellationToken.None);

        var estado = Assert.Single(estados);
        Assert.Equal(new EstadoIbgeDados(35, "SP", "São Paulo"), estado);
        Assert.Equal(new Uri(BaseAddress, "estados"), Assert.Single(handler.Requests));
    }

    [Fact]
    public async Task Le_municipios_preserva_acentos_e_usa_estado_na_rota()
    {
        var handler = new TestHttpMessageHandler(_ => Json(
            """
            [{"id":3100203,"nome":"Abaeté","microrregiao":null}]
            """));
        var client = CreateClient(handler);

        var municipios = await client.ListarMunicipiosAsync(" mg ", CancellationToken.None);

        Assert.Equal(new MunicipioIbgeDados(3100203, "Abaeté"), Assert.Single(municipios));
        Assert.Equal(
            new Uri(BaseAddress, "estados/MG/municipios"),
            Assert.Single(handler.Requests));
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, "[]")]
    [InlineData(HttpStatusCode.OK, "[]")]
    [InlineData(HttpStatusCode.OK, "{\"id\":35}")]
    [InlineData(HttpStatusCode.OK, "conteúdo inválido")]
    public async Task Resposta_http_vazia_ou_invalida_gera_erro_controlado(
        HttpStatusCode statusCode,
        string content)
    {
        var handler = new TestHttpMessageHandler(_ => Json(content, statusCode));
        var client = CreateClient(handler);

        await Assert.ThrowsAsync<IbgeLocalidadesException>(() =>
            client.ListarEstadosAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Cancelamento_do_chamador_e_preservado()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var handler = new TestHttpMessageHandler(request =>
        {
            request.GetHashCode();
            return Json("[]");
        });
        var client = CreateClient(handler);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.ListarEstadosAsync(cancellation.Token));
    }

    [Fact]
    public async Task Timeout_do_http_client_gera_erro_controlado()
    {
        var httpClient = new HttpClient(new HangingHttpMessageHandler())
        {
            BaseAddress = BaseAddress,
            Timeout = TimeSpan.FromMilliseconds(100),
        };
        var client = new IbgeLocalidadesClient(httpClient, NullLogger<IbgeLocalidadesClient>.Instance);

        await Assert.ThrowsAsync<IbgeLocalidadesException>(() =>
            client.ListarEstadosAsync(CancellationToken.None));
    }

    private static IbgeLocalidadesClient CreateClient(HttpMessageHandler handler)
    {
        return new IbgeLocalidadesClient(new HttpClient(handler)
        {
            BaseAddress = BaseAddress,
            Timeout = TimeSpan.FromSeconds(2),
        }, NullLogger<IbgeLocalidadesClient>.Instance);
    }

    private static HttpResponseMessage Json(
        string content,
        HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/json"),
        };
    }

    private sealed class TestHttpMessageHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request.RequestUri!);
            return Task.FromResult(responder(request));
        }
    }

    private sealed class HangingHttpMessageHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            request.GetHashCode();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("Unreachable.");
        }
    }
}
