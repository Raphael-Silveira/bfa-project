using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using BFA.Domain.Acessos;
using BFA.Domain.Contratos;
using BFA.Domain.Franqueados;
using BFA.Domain.Unidades;
using BFA.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BFA.IntegrationTests;

public sealed partial class ContratosFranquiaEndpointTests
{
    [Fact]
    public async Task Anonimo_e_administrador_unidade_nao_acessam_contratos()
    {
        var rota = $"/franqueadora/franqueados/{Guid.NewGuid()}/unidades/{Guid.NewGuid()}/contrato";
        var rotaDownload = $"{rota}/{Guid.NewGuid()}/versoes/{Guid.NewGuid()}/documentos/{Guid.NewGuid()}/baixar";
        using var anonima = new ContratosFranquiaWebApplicationFactory();
        using var clienteAnonimo = CriarCliente(anonima);
        using var respostaAnonima = await clienteAnonimo.GetAsync(rota);
        using var downloadAnonimo = await clienteAnonimo.GetAsync(rotaDownload);

        using var unidade = new ContratosFranquiaWebApplicationFactory();
        await unidade.InicializarAdministradorAsync(PerfilAcesso.AdministradorUnidade);
        using var clienteUnidade = CriarCliente(unidade);
        await LoginAsync(clienteUnidade, unidade);
        using var respostaUnidade = await clienteUnidade.GetAsync(rota);
        using var downloadUnidade = await clienteUnidade.GetAsync(rotaDownload);

        Assert.Equal(HttpStatusCode.Found, respostaAnonima.StatusCode);
        Assert.StartsWith("/login?", respostaAnonima.Headers.Location?.PathAndQuery, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Found, downloadAnonimo.StatusCode);
        Assert.StartsWith("/login?", downloadAnonimo.Headers.Location?.PathAndQuery, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Found, respostaUnidade.StatusCode);
        Assert.StartsWith("/acesso-negado?", respostaUnidade.Headers.Location?.PathAndQuery, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Found, downloadUnidade.StatusCode);
        Assert.StartsWith("/acesso-negado?", downloadUnidade.Headers.Location?.PathAndQuery, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Tela_do_franqueado_expoe_acao_contextual_e_outro_tenant_retorna_404()
    {
        using var application = new ContratosFranquiaWebApplicationFactory();
        var organizacaoId = await application.InicializarAdministradorAsync();
        var contexto = await CriarContextoAsync(application, organizacaoId);
        var externo = await CriarContextoAsync(application, Guid.NewGuid());
        var documentoExterno = await CriarContratoComDocumentoAsync(application, externo.VinculoId);
        using var client = CriarCliente(application);
        await LoginAsync(client, application);

        var detalhe = WebUtility.HtmlDecode(await client.GetStringAsync(
            $"/franqueadora/franqueados/{contexto.FranqueadoId}"));
        using var respostaExterna = await client.GetAsync(externo.RotaContrato);
        using var downloadExterno = await client.GetAsync(
            $"{externo.RotaContrato}/{documentoExterno.ContratoId}/versoes/{documentoExterno.VersaoId}/documentos/{documentoExterno.DocumentoId}/baixar");

        Assert.Contains(contexto.RotaContrato, detalhe, StringComparison.Ordinal);
        Assert.Contains("Criar contrato", detalhe, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.NotFound, respostaExterna.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, downloadExterno.StatusCode);
    }

    [Fact]
    public async Task Cria_rascunho_envia_pdf_ativa_e_faz_download_privado_sem_expor_chave()
    {
        using var application = new ContratosFranquiaWebApplicationFactory();
        var organizacaoId = await application.InicializarAdministradorAsync();
        var contexto = await CriarContextoAsync(application, organizacaoId);
        using var client = CriarCliente(application);
        await LoginAsync(client, application);
        var token = await ObterAntiforgeryAsync(client, $"{contexto.RotaContrato}/novo");
        using var criar = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["NumeroContrato"] = "BFA-2026-001",
            ["DataInicio"] = "2026-09-01",
            ["PercentualRoyalties"] = "8,00",
            ["MensalidadeFixa"] = "500,00",
            ["TaxaAdesao"] = "1000,00",
            ["DiaVencimento"] = "10",
            ["Observacoes"] = "Contrato inicial",
            ["__RequestVerificationToken"] = token
        });
        using var respostaCriar = await client.PostAsync($"{contexto.RotaContrato}/novo", criar);
        Assert.Equal(HttpStatusCode.Found, respostaCriar.StatusCode);

        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        var contrato = await dbContext.ContratosFranquia.SingleAsync();
        var versao = await dbContext.ContratosFranquiaVersoes.SingleAsync();
        Assert.Equal(StatusContratoFranquia.Rascunho, contrato.Status);
        Assert.Equal(StatusVersaoContratoFranquia.Rascunho, versao.Status);

        var pagina = WebUtility.HtmlDecode(await client.GetStringAsync(contexto.RotaContrato));
        Assert.Contains("Adicionar documento PDF", pagina, StringComparison.Ordinal);
        Assert.DoesNotContain(".storage", pagina, StringComparison.OrdinalIgnoreCase);
        token = ExtrairAntiforgery(pagina);
        using var upload = new MultipartFormDataContent();
        upload.Add(new StringContent(nameof(TipoDocumentoContratoFranquia.Contrato)), "TipoDocumento");
        upload.Add(new StringContent(token), "__RequestVerificationToken");
        var pdfBytes = "%PDF-1.7\nBFA teste"u8.ToArray();
        var arquivo = new ByteArrayContent(pdfBytes);
        arquivo.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        upload.Add(arquivo, "Arquivo", "contrato-oficial.pdf");
        using var respostaUpload = await client.PostAsync(
            $"{contexto.RotaContrato}/{contrato.Id}/versoes/{versao.Id}/documentos",
            upload);
        Assert.Equal(HttpStatusCode.Found, respostaUpload.StatusCode);

        dbContext.ChangeTracker.Clear();
        var documento = await dbContext.DocumentosContratoFranquia.SingleAsync();
        Assert.Equal(64, documento.HashSha256?.Length);
        Assert.StartsWith($"contratos/{contrato.Id:N}/versoes/{versao.Id:N}/", documento.ChaveArmazenamento, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(
            application.DiretorioArmazenamento,
            documento.ChaveArmazenamento.Replace('/', Path.DirectorySeparatorChar))));

        token = await ObterAntiforgeryAsync(client, contexto.RotaContrato);
        using var ativar = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token
        });
        using var respostaAtivar = await client.PostAsync(
            $"{contexto.RotaContrato}/{contrato.Id}/ativar",
            ativar);
        Assert.Equal(HttpStatusCode.Found, respostaAtivar.StatusCode);
        dbContext.ChangeTracker.Clear();
        Assert.Equal(StatusContratoFranquia.Ativo, (await dbContext.ContratosFranquia.SingleAsync()).Status);
        Assert.Equal(StatusVersaoContratoFranquia.Vigente, (await dbContext.ContratosFranquiaVersoes.SingleAsync()).Status);

        var rotaDownload = $"{contexto.RotaContrato}/{contrato.Id}/versoes/{versao.Id}/documentos/{documento.Id}/baixar";
        Assert.DoesNotContain(documento.ChaveArmazenamento, rotaDownload, StringComparison.Ordinal);
        using var respostaDownload = await client.GetAsync(rotaDownload);
        Assert.Equal(HttpStatusCode.OK, respostaDownload.StatusCode);
        Assert.Equal("application/pdf", respostaDownload.Content.Headers.ContentType?.MediaType);
        Assert.Equal(pdfBytes, await respostaDownload.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Metadata_sem_arquivo_retorna_503_controlado()
    {
        using var application = new ContratosFranquiaWebApplicationFactory();
        var organizacaoId = await application.InicializarAdministradorAsync();
        var contexto = await CriarContextoAsync(application, organizacaoId);
        var ids = await CriarContratoComDocumentoAsync(application, contexto.VinculoId);
        using var client = CriarCliente(application);
        await LoginAsync(client, application);

        using var resposta = await client.GetAsync(
            $"{contexto.RotaContrato}/{ids.ContratoId}/versoes/{ids.VersaoId}/documentos/{ids.DocumentoId}/visualizar");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, resposta.StatusCode);
        Assert.Equal("Documento indisponível no armazenamento.", await resposta.Content.ReadAsStringAsync());
    }

    private static HttpClient CriarCliente(ContratosFranquiaWebApplicationFactory application) =>
        application.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

    private static async Task LoginAsync(
        HttpClient client,
        ContratosFranquiaWebApplicationFactory application)
    {
        var token = await ObterAntiforgeryAsync(client, "/login");
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = application.AdministradorEmail,
            ["Senha"] = application.AdministradorSenha,
            ["LembrarMe"] = "false",
            ["ReturnUrl"] = string.Empty,
            ["__RequestVerificationToken"] = token
        });
        using var response = await client.PostAsync("/login", form);
        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
    }

    private static async Task<string> ObterAntiforgeryAsync(HttpClient client, string url) =>
        ExtrairAntiforgery(await client.GetStringAsync(url));

    private static string ExtrairAntiforgery(string html)
    {
        var match = AntiforgeryToken().Match(html);
        Assert.True(match.Success, "Token antiforgery não encontrado.");
        return WebUtility.HtmlDecode(match.Groups["token"].Value);
    }

    private static async Task<ContextoTeste> CriarContextoAsync(
        ContratosFranquiaWebApplicationFactory application,
        Guid organizacaoId)
    {
        var franqueado = new Franqueado(
            Guid.NewGuid(),
            organizacaoId,
            TipoPessoaFranqueado.PessoaFisica,
            $"Franqueado {Guid.NewGuid():N}",
            Random.Shared.NextInt64(10_000_000_000, 99_999_999_999).ToString(),
            $"franqueado-{Guid.NewGuid():N}@bfa.test",
            DateTime.UtcNow);
        var unidade = new Unidade(
            Guid.NewGuid(),
            organizacaoId,
            "BFA Cerquilho",
            $"unidade-{Guid.NewGuid():N}",
            DateTime.UtcNow);
        var vinculo = new FranqueadoUnidade(
            Guid.NewGuid(),
            franqueado.Id,
            organizacaoId,
            unidade.Id,
            DateTime.UtcNow);
        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        dbContext.Franqueados.Add(franqueado);
        dbContext.Unidades.Add(unidade);
        dbContext.FranqueadosUnidades.Add(vinculo);
        await dbContext.SaveChangesAsync();
        return new(
            franqueado.Id,
            unidade.Id,
            vinculo.Id,
            $"/franqueadora/franqueados/{franqueado.Id}/unidades/{unidade.Id}/contrato");
    }

    private static async Task<ContratoIds> CriarContratoComDocumentoAsync(
        ContratosFranquiaWebApplicationFactory application,
        Guid vinculoId)
    {
        var contrato = new ContratoFranquia(
            Guid.NewGuid(), vinculoId, "BFA-404", StatusContratoFranquia.Rascunho, DateTime.UtcNow);
        var versao = new ContratoFranquiaVersao(
            Guid.NewGuid(), contrato.Id, 1, DateOnly.FromDateTime(DateTime.Today), null,
            8m, 500m, null, 10, StatusVersaoContratoFranquia.Rascunho, null, null,
            DateTime.UtcNow, application.AdministradorId);
        var documento = new DocumentoContratoFranquia(
            Guid.NewGuid(), versao.Id, TipoDocumentoContratoFranquia.Contrato,
            "ausente.pdf", $"contratos/{contrato.Id:N}/versoes/{versao.Id:N}/{Guid.NewGuid():N}.pdf",
            "application/pdf", 100, new string('a', 64), DateTime.UtcNow, application.AdministradorId);
        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        dbContext.ContratosFranquia.Add(contrato);
        dbContext.ContratosFranquiaVersoes.Add(versao);
        dbContext.DocumentosContratoFranquia.Add(documento);
        await dbContext.SaveChangesAsync();
        return new(contrato.Id, versao.Id, documento.Id);
    }

    private sealed record ContextoTeste(
        Guid FranqueadoId,
        Guid UnidadeId,
        Guid VinculoId,
        string RotaContrato);

    private sealed record ContratoIds(Guid ContratoId, Guid VersaoId, Guid DocumentoId);

    [GeneratedRegex(
        "name=\"__RequestVerificationToken\"[^>]*value=\"(?<token>[^\"]+)\"",
        RegexOptions.CultureInvariant)]
    private static partial Regex AntiforgeryToken();
}
