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
    public async Task Formulario_exibe_datas_brasileiras_e_persiste_DateOnly()
    {
        using var application = new ContratosFranquiaWebApplicationFactory();
        var organizacaoId = await application.InicializarAdministradorAsync();
        var contexto = await CriarContextoAsync(application, organizacaoId);
        using var client = CriarCliente(application);
        await LoginAsync(client, application);
        var rota = $"{contexto.RotaContrato}/novo";
        var pagina = WebUtility.HtmlDecode(await client.GetStringAsync(rota));

        Assert.Contains("name=\"DataInicioTexto\"", pagina, StringComparison.Ordinal);
        Assert.Contains("name=\"DataFimTexto\"", pagina, StringComparison.Ordinal);
        Assert.Contains("placeholder=\"dd/mm/aaaa\"", pagina, StringComparison.Ordinal);
        Assert.Contains("data-bfa-date-trigger", pagina, StringComparison.Ordinal);
        Assert.Contains("aria-haspopup=\"dialog\"", pagina, StringComparison.Ordinal);
        Assert.DoesNotContain("type=\"date\"", pagina, StringComparison.Ordinal);
        Assert.Contains("data-bfa-number=\"percent\"", pagina, StringComparison.Ordinal);
        Assert.Contains("data-bfa-number=\"money\"", pagina, StringComparison.Ordinal);
        Assert.Contains("data-bfa-number=\"integer\"", pagina, StringComparison.Ordinal);
        Assert.Contains("/js/bfa-number-field.js", pagina, StringComparison.Ordinal);
        Assert.Contains("/js/bfa-date-field.js", pagina, StringComparison.Ordinal);

        var token = ExtrairAntiforgery(pagina);
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["NumeroContrato"] = "BFA-DATA-BR",
            ["DataInicioTexto"] = "22/08/2026",
            ["DataFimTexto"] = "30/09/2026",
            ["PercentualRoyalties"] = "8,50",
            ["MensalidadeFixa"] = "1.500,50",
            ["TaxaAdesao"] = "2.000,75",
            ["DiaVencimento"] = "10",
            ["__RequestVerificationToken"] = token
        });

        using var resposta = await client.PostAsync(rota, form);

        Assert.Equal(HttpStatusCode.Found, resposta.StatusCode);
        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        var versao = await dbContext.ContratosFranquiaVersoes.SingleAsync();
        Assert.Equal(new DateOnly(2026, 8, 22), versao.DataInicio);
        Assert.Equal(new DateOnly(2026, 9, 30), versao.DataFim);
        Assert.Equal(8.50m, versao.PercentualRoyalties);
        Assert.Equal(1_500.50m, versao.MensalidadeFixa);
        Assert.Equal(2_000.75m, versao.TaxaAdesao);
        Assert.Equal(10, versao.DiaVencimento);
    }

    [Fact]
    public async Task Post_com_data_brasileira_invalida_preserva_valor_e_exibe_validacao()
    {
        using var application = new ContratosFranquiaWebApplicationFactory();
        var organizacaoId = await application.InicializarAdministradorAsync();
        var contexto = await CriarContextoAsync(application, organizacaoId);
        using var client = CriarCliente(application);
        await LoginAsync(client, application);
        var rota = $"{contexto.RotaContrato}/novo";
        var token = await ObterAntiforgeryAsync(client, rota);
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["NumeroContrato"] = "BFA-DATA-INVALIDA",
            ["DataInicioTexto"] = "31/02/2026",
            ["PercentualRoyalties"] = "8,00",
            ["MensalidadeFixa"] = "500,00",
            ["__RequestVerificationToken"] = token
        });

        using var resposta = await client.PostAsync(rota, form);
        var pagina = WebUtility.HtmlDecode(await resposta.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Contains("Informe uma data de início válida no formato dd/mm/aaaa.", pagina, StringComparison.Ordinal);
        Assert.Contains("value=\"31/02/2026\"", pagina, StringComparison.Ordinal);
        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        Assert.Empty(await dbContext.ContratosFranquia.ToListAsync());
    }

    [Fact]
    public async Task Post_com_valores_numericos_invalidos_exibe_validacao_e_nao_cria_contrato()
    {
        using var application = new ContratosFranquiaWebApplicationFactory();
        var organizacaoId = await application.InicializarAdministradorAsync();
        var contexto = await CriarContextoAsync(application, organizacaoId);
        using var client = CriarCliente(application);
        await LoginAsync(client, application);
        var rota = $"{contexto.RotaContrato}/novo";
        var token = await ObterAntiforgeryAsync(client, rota);
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["NumeroContrato"] = "BFA-NUMERO-INVALIDO",
            ["DataInicioTexto"] = "22/08/2026",
            ["PercentualRoyalties"] = "oito",
            ["MensalidadeFixa"] = "quinhentos",
            ["DiaVencimento"] = "10",
            ["__RequestVerificationToken"] = token
        });

        using var resposta = await client.PostAsync(rota, form);
        var pagina = WebUtility.HtmlDecode(await resposta.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Contains("Informe um percentual entre 0 e 100.", pagina, StringComparison.Ordinal);
        Assert.Contains("Informe uma mensalidade válida.", pagina, StringComparison.Ordinal);
        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        Assert.Empty(await dbContext.ContratosFranquia.ToListAsync());
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
        Assert.Contains("Adicionar documento", pagina, StringComparison.Ordinal);
        Assert.Contains("Tipo de documento", pagina, StringComparison.Ordinal);
        Assert.Contains("data-bfa-file-upload", pagina, StringComparison.Ordinal);
        Assert.Contains("Selecione um arquivo PDF", pagina, StringComparison.Ordinal);
        Assert.Contains("PDF • máximo", pagina, StringComparison.Ordinal);
        Assert.Contains("/js/bfa-file-upload.js", pagina, StringComparison.Ordinal);
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
    public async Task Administrador_rede_cria_edita_documenta_e_formaliza_nova_versao_preservando_historico()
    {
        using var application = new ContratosFranquiaWebApplicationFactory();
        var organizacaoId = await application.InicializarAdministradorAsync();
        var contexto = await CriarContextoAsync(application, organizacaoId);
        var ids = await CriarContratoAtivoAsync(application, contexto.VinculoId);
        using var client = CriarCliente(application);
        await LoginAsync(client, application);

        var token = await ObterAntiforgeryAsync(client, contexto.RotaContrato);
        using var criarNovaVersao = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["MotivoAlteracao"] = "Revisão anual das condições comerciais",
            ["__RequestVerificationToken"] = token
        });
        using var respostaCriacao = await client.PostAsync(
            $"{contexto.RotaContrato}/{ids.ContratoId}/versoes/nova",
            criarNovaVersao);

        Assert.Equal(HttpStatusCode.Found, respostaCriacao.StatusCode);
        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        var versoes = await dbContext.ContratosFranquiaVersoes
            .OrderBy(item => item.NumeroVersao)
            .ToListAsync();
        Assert.Equal(2, versoes.Count);
        var versaoOriginal = versoes[0];
        var novaVersao = versoes[1];
        Assert.Equal(2, novaVersao.NumeroVersao);
        Assert.Equal(StatusVersaoContratoFranquia.Rascunho, novaVersao.Status);
        Assert.Equal(versaoOriginal.DataInicio, novaVersao.DataInicio);
        Assert.Equal(versaoOriginal.DataFim, novaVersao.DataFim);
        Assert.Equal(versaoOriginal.PercentualRoyalties, novaVersao.PercentualRoyalties);
        Assert.Equal(versaoOriginal.MensalidadeFixa, novaVersao.MensalidadeFixa);
        Assert.Equal(versaoOriginal.TaxaAdesao, novaVersao.TaxaAdesao);
        Assert.Equal(versaoOriginal.DiaVencimento, novaVersao.DiaVencimento);
        Assert.Equal(versaoOriginal.Observacoes, novaVersao.Observacoes);
        Assert.Equal("Revisão anual das condições comerciais", novaVersao.MotivoAlteracao);
        Assert.Equal(
            $"{contexto.RotaContrato}/{ids.ContratoId}/versoes/{novaVersao.Id}/editar",
            respostaCriacao.Headers.Location?.OriginalString);

        var rotaEdicao = $"{contexto.RotaContrato}/{ids.ContratoId}/versoes/{novaVersao.Id}/editar";
        token = await ObterAntiforgeryAsync(client, rotaEdicao);
        using var editar = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["NumeroContrato"] = "BFA-ATIVO-001",
            ["DataInicioTexto"] = "01/02/2027",
            ["DataFimTexto"] = "31/01/2028",
            ["PercentualRoyalties"] = "9,50",
            ["MensalidadeFixa"] = "750,25",
            ["TaxaAdesao"] = "1.250,75",
            ["DiaVencimento"] = "15",
            ["MotivoAlteracao"] = "Revisão anual das condições comerciais",
            ["Observacoes"] = "Termos revisados da versão 2",
            ["__RequestVerificationToken"] = token
        });
        using var respostaEdicao = await client.PostAsync(rotaEdicao, editar);

        Assert.Equal(HttpStatusCode.Found, respostaEdicao.StatusCode);
        dbContext.ChangeTracker.Clear();
        novaVersao = await dbContext.ContratosFranquiaVersoes.SingleAsync(item => item.Id == novaVersao.Id);
        Assert.Equal(new DateOnly(2027, 2, 1), novaVersao.DataInicio);
        Assert.Equal(new DateOnly(2028, 1, 31), novaVersao.DataFim);
        Assert.Equal(9.50m, novaVersao.PercentualRoyalties);
        Assert.Equal(750.25m, novaVersao.MensalidadeFixa);
        Assert.Equal(1_250.75m, novaVersao.TaxaAdesao);
        Assert.Equal(15, novaVersao.DiaVencimento);
        Assert.Equal("Termos revisados da versão 2", novaVersao.Observacoes);

        token = await ObterAntiforgeryAsync(client, contexto.RotaContrato);
        using var formalizarSemDocumento = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token
        });
        using var respostaSemDocumento = await client.PostAsync(
            $"{contexto.RotaContrato}/{ids.ContratoId}/versoes/{novaVersao.Id}/formalizar",
            formalizarSemDocumento);

        Assert.Equal(HttpStatusCode.Found, respostaSemDocumento.StatusCode);
        var paginaComErro = WebUtility.HtmlDecode(await client.GetStringAsync(contexto.RotaContrato));
        Assert.Contains(
            "Adicione um documento do tipo Contrato ou Aditivo antes de formalizar.",
            paginaComErro,
            StringComparison.Ordinal);
        dbContext.ChangeTracker.Clear();
        Assert.Equal(
            StatusVersaoContratoFranquia.Vigente,
            (await dbContext.ContratosFranquiaVersoes.SingleAsync(item => item.Id == ids.VersaoVigenteId)).Status);
        Assert.Equal(
            StatusVersaoContratoFranquia.Rascunho,
            (await dbContext.ContratosFranquiaVersoes.SingleAsync(item => item.Id == novaVersao.Id)).Status);

        token = await ObterAntiforgeryAsync(client, contexto.RotaContrato);
        using var upload = CriarUploadDocumento(TipoDocumentoContratoFranquia.Aditivo, token);
        using var respostaUpload = await client.PostAsync(
            $"{contexto.RotaContrato}/{ids.ContratoId}/versoes/{novaVersao.Id}/documentos",
            upload);
        Assert.Equal(HttpStatusCode.Found, respostaUpload.StatusCode);

        token = await ObterAntiforgeryAsync(client, contexto.RotaContrato);
        using var formalizar = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token
        });
        using var respostaFormalizacao = await client.PostAsync(
            $"{contexto.RotaContrato}/{ids.ContratoId}/versoes/{novaVersao.Id}/formalizar",
            formalizar);

        Assert.Equal(HttpStatusCode.Found, respostaFormalizacao.StatusCode);
        dbContext.ChangeTracker.Clear();
        var contrato = await dbContext.ContratosFranquia.SingleAsync(item => item.Id == ids.ContratoId);
        versaoOriginal = await dbContext.ContratosFranquiaVersoes.SingleAsync(
            item => item.Id == ids.VersaoVigenteId);
        novaVersao = await dbContext.ContratosFranquiaVersoes.SingleAsync(
            item => item.NumeroVersao == 2);
        var documento = await dbContext.DocumentosContratoFranquia.SingleAsync(
            item => item.ContratoFranquiaVersaoId == novaVersao.Id);

        Assert.Equal(StatusContratoFranquia.Ativo, contrato.Status);
        Assert.Equal(StatusVersaoContratoFranquia.Substituida, versaoOriginal.Status);
        Assert.Equal(StatusVersaoContratoFranquia.Vigente, novaVersao.Status);
        Assert.Equal(TipoDocumentoContratoFranquia.Aditivo, documento.TipoDocumento);
        Assert.Equal(new DateOnly(2026, 1, 1), versaoOriginal.DataInicio);
        Assert.Equal(new DateOnly(2026, 12, 31), versaoOriginal.DataFim);
        Assert.Equal(8m, versaoOriginal.PercentualRoyalties);
        Assert.Equal(500m, versaoOriginal.MensalidadeFixa);
        Assert.Equal(1_000m, versaoOriginal.TaxaAdesao);
        Assert.Equal(10, versaoOriginal.DiaVencimento);
        Assert.Equal("Condições originais da versão 1", versaoOriginal.Observacoes);
    }

    [Fact]
    public async Task Outro_tenant_nao_cria_edita_ou_formaliza_versao_contratual()
    {
        using var application = new ContratosFranquiaWebApplicationFactory();
        var organizacaoId = await application.InicializarAdministradorAsync();
        var contextoAutorizado = await CriarContextoAsync(application, organizacaoId);
        var contextoExterno = await CriarContextoAsync(application, Guid.NewGuid());
        var idsExternos = await CriarContratoAtivoAsync(application, contextoExterno.VinculoId);
        using var client = CriarCliente(application);
        await LoginAsync(client, application);
        var token = await ObterAntiforgeryAsync(client, contextoAutorizado.RotaContrato);

        using var criar = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["MotivoAlteracao"] = "Tentativa externa",
            ["__RequestVerificationToken"] = token
        });
        using var respostaCriar = await client.PostAsync(
            $"{contextoExterno.RotaContrato}/{idsExternos.ContratoId}/versoes/nova",
            criar);

        using var editar = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["NumeroContrato"] = "ALTERADO",
            ["DataInicioTexto"] = "01/01/2027",
            ["PercentualRoyalties"] = "99,00",
            ["MensalidadeFixa"] = "9.999,00",
            ["MotivoAlteracao"] = "Tentativa externa",
            ["__RequestVerificationToken"] = token
        });
        using var respostaEditar = await client.PostAsync(
            $"{contextoExterno.RotaContrato}/{idsExternos.ContratoId}/versoes/{idsExternos.VersaoVigenteId}/editar",
            editar);

        using var formalizar = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token
        });
        using var respostaFormalizar = await client.PostAsync(
            $"{contextoExterno.RotaContrato}/{idsExternos.ContratoId}/versoes/{idsExternos.VersaoVigenteId}/formalizar",
            formalizar);

        Assert.Equal(HttpStatusCode.NotFound, respostaCriar.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, respostaEditar.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, respostaFormalizar.StatusCode);
        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        var versao = await dbContext.ContratosFranquiaVersoes.SingleAsync(
            item => item.Id == idsExternos.VersaoVigenteId);
        Assert.Equal(StatusVersaoContratoFranquia.Vigente, versao.Status);
        Assert.Equal(8m, versao.PercentualRoyalties);
        Assert.Single(await dbContext.ContratosFranquiaVersoes
            .Where(item => item.ContratoFranquiaId == idsExternos.ContratoId)
            .ToListAsync());
    }

    [Fact]
    public async Task Usuario_sem_policy_administrador_rede_nao_executa_fluxo_de_nova_versao()
    {
        using var application = new ContratosFranquiaWebApplicationFactory();
        var organizacaoId = await application.InicializarAdministradorAsync(PerfilAcesso.AdministradorUnidade);
        var contexto = await CriarContextoAsync(application, organizacaoId);
        var ids = await CriarContratoAtivoAsync(application, contexto.VinculoId);
        using var client = CriarCliente(application);
        await LoginAsync(client, application);

        using var criar = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["MotivoAlteracao"] = "Sem policy"
        });
        using var respostaCriar = await client.PostAsync(
            $"{contexto.RotaContrato}/{ids.ContratoId}/versoes/nova",
            criar);
        using var editar = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["DataInicioTexto"] = "01/01/2027",
            ["PercentualRoyalties"] = "9,00",
            ["MensalidadeFixa"] = "600,00"
        });
        using var respostaEditar = await client.PostAsync(
            $"{contexto.RotaContrato}/{ids.ContratoId}/versoes/{ids.VersaoVigenteId}/editar",
            editar);
        using var upload = CriarUploadDocumento(TipoDocumentoContratoFranquia.Aditivo);
        using var respostaUpload = await client.PostAsync(
            $"{contexto.RotaContrato}/{ids.ContratoId}/versoes/{ids.VersaoVigenteId}/documentos",
            upload);
        using var formalizar = new FormUrlEncodedContent([]);
        using var respostaFormalizar = await client.PostAsync(
            $"{contexto.RotaContrato}/{ids.ContratoId}/versoes/{ids.VersaoVigenteId}/formalizar",
            formalizar);

        Assert.Equal(HttpStatusCode.Found, respostaCriar.StatusCode);
        Assert.Equal(HttpStatusCode.Found, respostaEditar.StatusCode);
        Assert.Equal(HttpStatusCode.Found, respostaUpload.StatusCode);
        Assert.Equal(HttpStatusCode.Found, respostaFormalizar.StatusCode);
        Assert.StartsWith("/acesso-negado?", respostaCriar.Headers.Location?.PathAndQuery, StringComparison.Ordinal);
        Assert.StartsWith("/acesso-negado?", respostaEditar.Headers.Location?.PathAndQuery, StringComparison.Ordinal);
        Assert.StartsWith("/acesso-negado?", respostaUpload.Headers.Location?.PathAndQuery, StringComparison.Ordinal);
        Assert.StartsWith("/acesso-negado?", respostaFormalizar.Headers.Location?.PathAndQuery, StringComparison.Ordinal);
        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        Assert.Single(await dbContext.ContratosFranquiaVersoes
            .Where(item => item.ContratoFranquiaId == ids.ContratoId)
            .ToListAsync());
        Assert.Empty(await dbContext.DocumentosContratoFranquia.ToListAsync());
    }

    [Fact]
    public async Task Posts_do_fluxo_de_nova_versao_exigem_antiforgery()
    {
        using var application = new ContratosFranquiaWebApplicationFactory();
        var organizacaoId = await application.InicializarAdministradorAsync();
        var contexto = await CriarContextoAsync(application, organizacaoId);
        var ids = await CriarContratoAtivoAsync(application, contexto.VinculoId);
        using var client = CriarCliente(application);
        await LoginAsync(client, application);

        using var criar = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["MotivoAlteracao"] = "Sem token"
        });
        using var respostaCriar = await client.PostAsync(
            $"{contexto.RotaContrato}/{ids.ContratoId}/versoes/nova",
            criar);
        using var editar = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["DataInicioTexto"] = "01/01/2027",
            ["PercentualRoyalties"] = "9,00",
            ["MensalidadeFixa"] = "600,00"
        });
        using var respostaEditar = await client.PostAsync(
            $"{contexto.RotaContrato}/{ids.ContratoId}/versoes/{ids.VersaoVigenteId}/editar",
            editar);
        using var upload = CriarUploadDocumento(TipoDocumentoContratoFranquia.Aditivo);
        using var respostaUpload = await client.PostAsync(
            $"{contexto.RotaContrato}/{ids.ContratoId}/versoes/{ids.VersaoVigenteId}/documentos",
            upload);
        using var formalizar = new FormUrlEncodedContent([]);
        using var respostaFormalizar = await client.PostAsync(
            $"{contexto.RotaContrato}/{ids.ContratoId}/versoes/{ids.VersaoVigenteId}/formalizar",
            formalizar);

        Assert.Equal(HttpStatusCode.BadRequest, respostaCriar.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, respostaEditar.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, respostaUpload.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, respostaFormalizar.StatusCode);
        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        Assert.Single(await dbContext.ContratosFranquiaVersoes
            .Where(item => item.ContratoFranquiaId == ids.ContratoId)
            .ToListAsync());
        Assert.Empty(await dbContext.DocumentosContratoFranquia.ToListAsync());
    }

    [Fact]
    public async Task Administrador_rede_cancela_contrato_ativo_e_versao_vigente_via_http()
    {
        using var application = new ContratosFranquiaWebApplicationFactory();
        var organizacaoId = await application.InicializarAdministradorAsync();
        var contexto = await CriarContextoAsync(application, organizacaoId);
        var ids = await CriarContratoParaCicloFinalAsync(
            application,
            contexto.VinculoId,
            StatusContratoFranquia.Ativo,
            StatusVersaoContratoFranquia.Vigente);
        using var client = CriarCliente(application);
        await LoginAsync(client, application);

        using var resposta = await PostOperacaoAsync(
            client,
            contexto.RotaContrato,
            $"{contexto.RotaContrato}/{ids.ContratoId}/cancelar");

        Assert.Equal(HttpStatusCode.Found, resposta.StatusCode);
        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        var contrato = await dbContext.ContratosFranquia.SingleAsync(item => item.Id == ids.ContratoId);
        var versao = await dbContext.ContratosFranquiaVersoes.SingleAsync(item => item.Id == ids.VersoesIds[0]);
        Assert.Equal(StatusContratoFranquia.Cancelado, contrato.Status);
        Assert.Equal(StatusVersaoContratoFranquia.Cancelada, versao.Status);
    }

    [Fact]
    public async Task Cancelamento_ativo_cancela_vigente_e_rascunho_e_preserva_historico_e_documentos()
    {
        using var application = new ContratosFranquiaWebApplicationFactory();
        var organizacaoId = await application.InicializarAdministradorAsync();
        var contexto = await CriarContextoAsync(application, organizacaoId);
        var ids = await CriarContratoParaCicloFinalAsync(
            application,
            contexto.VinculoId,
            StatusContratoFranquia.Ativo,
            StatusVersaoContratoFranquia.Substituida,
            StatusVersaoContratoFranquia.Vigente,
            StatusVersaoContratoFranquia.Rascunho);
        using var client = CriarCliente(application);
        await LoginAsync(client, application);

        using var resposta = await PostOperacaoAsync(
            client,
            contexto.RotaContrato,
            $"{contexto.RotaContrato}/{ids.ContratoId}/cancelar");

        Assert.Equal(HttpStatusCode.Found, resposta.StatusCode);
        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        var contrato = await dbContext.ContratosFranquia.SingleAsync(item => item.Id == ids.ContratoId);
        var versoes = await dbContext.ContratosFranquiaVersoes
            .Where(item => item.ContratoFranquiaId == ids.ContratoId)
            .OrderBy(item => item.NumeroVersao)
            .ToListAsync();
        var documentos = await dbContext.DocumentosContratoFranquia
            .Where(item => ids.VersoesIds.Contains(item.ContratoFranquiaVersaoId))
            .ToListAsync();
        Assert.Equal(StatusContratoFranquia.Cancelado, contrato.Status);
        Assert.Equal(StatusVersaoContratoFranquia.Substituida, versoes[0].Status);
        Assert.Equal(StatusVersaoContratoFranquia.Cancelada, versoes[1].Status);
        Assert.Equal(StatusVersaoContratoFranquia.Cancelada, versoes[2].Status);
        Assert.Equal(3, versoes.Count);
        Assert.Single(documentos);
        Assert.Equal(ids.DocumentoId, documentos[0].Id);

        using var pagina = await client.GetAsync(contexto.RotaContrato);
        var html = WebUtility.HtmlDecode(await pagina.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, pagina.StatusCode);
        Assert.Contains("Cancelado", html, StringComparison.Ordinal);
        Assert.Contains("Versão 1", html, StringComparison.Ordinal);
        Assert.Contains("Versão 2", html, StringComparison.Ordinal);
        Assert.Contains("Versão 3", html, StringComparison.Ordinal);
        Assert.Contains("historico-contratual.pdf", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cancelamento_de_contrato_rascunho_preserva_registros_e_cancela_versao()
    {
        using var application = new ContratosFranquiaWebApplicationFactory();
        var organizacaoId = await application.InicializarAdministradorAsync();
        var contexto = await CriarContextoAsync(application, organizacaoId);
        var ids = await CriarContratoParaCicloFinalAsync(
            application,
            contexto.VinculoId,
            StatusContratoFranquia.Rascunho,
            StatusVersaoContratoFranquia.Rascunho);
        using var client = CriarCliente(application);
        await LoginAsync(client, application);

        using var resposta = await PostOperacaoAsync(
            client,
            contexto.RotaContrato,
            $"{contexto.RotaContrato}/{ids.ContratoId}/cancelar");

        Assert.Equal(HttpStatusCode.Found, resposta.StatusCode);
        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        Assert.Equal(
            StatusContratoFranquia.Cancelado,
            (await dbContext.ContratosFranquia.SingleAsync(item => item.Id == ids.ContratoId)).Status);
        Assert.Equal(
            StatusVersaoContratoFranquia.Cancelada,
            (await dbContext.ContratosFranquiaVersoes.SingleAsync(item => item.Id == ids.VersoesIds[0])).Status);
        Assert.Single(await dbContext.ContratosFranquia
            .Where(item => item.Id == ids.ContratoId)
            .ToListAsync());
        Assert.Single(await dbContext.ContratosFranquiaVersoes
            .Where(item => item.ContratoFranquiaId == ids.ContratoId)
            .ToListAsync());
        Assert.Single(await dbContext.DocumentosContratoFranquia
            .Where(item => item.ContratoFranquiaVersaoId == ids.VersoesIds[0])
            .ToListAsync());
    }

    [Fact]
    public async Task Administrador_rede_encerra_contrato_ativo_mantendo_versao_vigente_e_historico()
    {
        using var application = new ContratosFranquiaWebApplicationFactory();
        var organizacaoId = await application.InicializarAdministradorAsync();
        var contexto = await CriarContextoAsync(application, organizacaoId);
        var ids = await CriarContratoParaCicloFinalAsync(
            application,
            contexto.VinculoId,
            StatusContratoFranquia.Ativo,
            StatusVersaoContratoFranquia.Vigente);
        using var client = CriarCliente(application);
        await LoginAsync(client, application);

        using var resposta = await PostOperacaoAsync(
            client,
            contexto.RotaContrato,
            $"{contexto.RotaContrato}/{ids.ContratoId}/encerrar");

        Assert.Equal(HttpStatusCode.Found, resposta.StatusCode);
        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        Assert.Equal(
            StatusContratoFranquia.Encerrado,
            (await dbContext.ContratosFranquia.SingleAsync(item => item.Id == ids.ContratoId)).Status);
        Assert.Equal(
            StatusVersaoContratoFranquia.Vigente,
            (await dbContext.ContratosFranquiaVersoes.SingleAsync(item => item.Id == ids.VersoesIds[0])).Status);
        Assert.Equal(
            ids.DocumentoId,
            (await dbContext.DocumentosContratoFranquia.SingleAsync(
                item => item.ContratoFranquiaVersaoId == ids.VersoesIds[0])).Id);

        using var pagina = await client.GetAsync(contexto.RotaContrato);
        var html = WebUtility.HtmlDecode(await pagina.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, pagina.StatusCode);
        Assert.Contains("Encerrado", html, StringComparison.Ordinal);
        Assert.Contains("Versão 1", html, StringComparison.Ordinal);
        Assert.Contains("Vigente", html, StringComparison.Ordinal);
        Assert.Contains("historico-contratual.pdf", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Outro_tenant_nao_cancela_nem_encerra_contrato()
    {
        using var application = new ContratosFranquiaWebApplicationFactory();
        var organizacaoId = await application.InicializarAdministradorAsync();
        var contextoAutorizado = await CriarContextoAsync(application, organizacaoId);
        var contextoExterno = await CriarContextoAsync(application, Guid.NewGuid());
        var idsExternos = await CriarContratoParaCicloFinalAsync(
            application,
            contextoExterno.VinculoId,
            StatusContratoFranquia.Ativo,
            StatusVersaoContratoFranquia.Vigente);
        using var client = CriarCliente(application);
        await LoginAsync(client, application);

        using var respostaCancelar = await PostOperacaoAsync(
            client,
            contextoAutorizado.RotaContrato,
            $"{contextoExterno.RotaContrato}/{idsExternos.ContratoId}/cancelar");
        using var respostaEncerrar = await PostOperacaoAsync(
            client,
            contextoAutorizado.RotaContrato,
            $"{contextoExterno.RotaContrato}/{idsExternos.ContratoId}/encerrar");

        Assert.Equal(HttpStatusCode.NotFound, respostaCancelar.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, respostaEncerrar.StatusCode);
        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        Assert.Equal(
            StatusContratoFranquia.Ativo,
            (await dbContext.ContratosFranquia.SingleAsync(item => item.Id == idsExternos.ContratoId)).Status);
        Assert.Equal(
            StatusVersaoContratoFranquia.Vigente,
            (await dbContext.ContratosFranquiaVersoes.SingleAsync(item => item.Id == idsExternos.VersoesIds[0])).Status);
    }

    [Fact]
    public async Task Usuario_sem_policy_administrador_rede_nao_cancela_nem_encerra_contrato()
    {
        using var application = new ContratosFranquiaWebApplicationFactory();
        var organizacaoId = await application.InicializarAdministradorAsync(PerfilAcesso.AdministradorUnidade);
        var contexto = await CriarContextoAsync(application, organizacaoId);
        var ids = await CriarContratoParaCicloFinalAsync(
            application,
            contexto.VinculoId,
            StatusContratoFranquia.Ativo,
            StatusVersaoContratoFranquia.Vigente);
        using var client = CriarCliente(application);
        await LoginAsync(client, application);

        using var cancelar = new FormUrlEncodedContent([]);
        using var respostaCancelar = await client.PostAsync(
            $"{contexto.RotaContrato}/{ids.ContratoId}/cancelar",
            cancelar);
        using var encerrar = new FormUrlEncodedContent([]);
        using var respostaEncerrar = await client.PostAsync(
            $"{contexto.RotaContrato}/{ids.ContratoId}/encerrar",
            encerrar);

        Assert.Equal(HttpStatusCode.Found, respostaCancelar.StatusCode);
        Assert.Equal(HttpStatusCode.Found, respostaEncerrar.StatusCode);
        Assert.StartsWith("/acesso-negado?", respostaCancelar.Headers.Location?.PathAndQuery, StringComparison.Ordinal);
        Assert.StartsWith("/acesso-negado?", respostaEncerrar.Headers.Location?.PathAndQuery, StringComparison.Ordinal);
        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        Assert.Equal(
            StatusContratoFranquia.Ativo,
            (await dbContext.ContratosFranquia.SingleAsync(item => item.Id == ids.ContratoId)).Status);
    }

    [Fact]
    public async Task Posts_de_cancelamento_e_encerramento_exigem_antiforgery()
    {
        using var application = new ContratosFranquiaWebApplicationFactory();
        var organizacaoId = await application.InicializarAdministradorAsync();
        var contexto = await CriarContextoAsync(application, organizacaoId);
        var ids = await CriarContratoParaCicloFinalAsync(
            application,
            contexto.VinculoId,
            StatusContratoFranquia.Ativo,
            StatusVersaoContratoFranquia.Vigente);
        using var client = CriarCliente(application);
        await LoginAsync(client, application);

        using var cancelar = new FormUrlEncodedContent([]);
        using var respostaCancelar = await client.PostAsync(
            $"{contexto.RotaContrato}/{ids.ContratoId}/cancelar",
            cancelar);
        using var encerrar = new FormUrlEncodedContent([]);
        using var respostaEncerrar = await client.PostAsync(
            $"{contexto.RotaContrato}/{ids.ContratoId}/encerrar",
            encerrar);

        Assert.Equal(HttpStatusCode.BadRequest, respostaCancelar.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, respostaEncerrar.StatusCode);
        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        Assert.Equal(
            StatusContratoFranquia.Ativo,
            (await dbContext.ContratosFranquia.SingleAsync(item => item.Id == ids.ContratoId)).Status);
        Assert.Equal(
            StatusVersaoContratoFranquia.Vigente,
            (await dbContext.ContratosFranquiaVersoes.SingleAsync(item => item.Id == ids.VersoesIds[0])).Status);
    }

    [Fact]
    public async Task Estados_terminais_rejeitam_transicoes_invalidas_com_resposta_http_controlada()
    {
        using var application = new ContratosFranquiaWebApplicationFactory();
        var organizacaoId = await application.InicializarAdministradorAsync();
        var contextoCancelado = await CriarContextoAsync(application, organizacaoId);
        var cancelado = await CriarContratoParaCicloFinalAsync(
            application,
            contextoCancelado.VinculoId,
            StatusContratoFranquia.Ativo,
            StatusVersaoContratoFranquia.Vigente);
        var contextoEncerrado = await CriarContextoAsync(application, organizacaoId);
        var encerrado = await CriarContratoParaCicloFinalAsync(
            application,
            contextoEncerrado.VinculoId,
            StatusContratoFranquia.Ativo,
            StatusVersaoContratoFranquia.Vigente);
        using var client = CriarCliente(application);
        await LoginAsync(client, application);

        using (var respostaCancelar = await PostOperacaoAsync(
            client,
            contextoCancelado.RotaContrato,
            $"{contextoCancelado.RotaContrato}/{cancelado.ContratoId}/cancelar"))
        {
            Assert.Equal(HttpStatusCode.Found, respostaCancelar.StatusCode);
        }

        using var encerrarCancelado = await PostOperacaoAsync(
            client,
            contextoCancelado.RotaContrato,
            $"{contextoCancelado.RotaContrato}/{cancelado.ContratoId}/encerrar");
        Assert.Equal(HttpStatusCode.Found, encerrarCancelado.StatusCode);
        var erroEncerramento = WebUtility.HtmlDecode(await client.GetStringAsync(contextoCancelado.RotaContrato));
        Assert.Contains("Somente um contrato ativo pode ser encerrado.", erroEncerramento, StringComparison.Ordinal);

        using (var respostaEncerrar = await PostOperacaoAsync(
            client,
            contextoEncerrado.RotaContrato,
            $"{contextoEncerrado.RotaContrato}/{encerrado.ContratoId}/encerrar"))
        {
            Assert.Equal(HttpStatusCode.Found, respostaEncerrar.StatusCode);
        }

        using var cancelarEncerrado = await PostOperacaoAsync(
            client,
            contextoEncerrado.RotaContrato,
            $"{contextoEncerrado.RotaContrato}/{encerrado.ContratoId}/cancelar");
        Assert.Equal(HttpStatusCode.Found, cancelarEncerrado.StatusCode);
        var erroCancelamento = WebUtility.HtmlDecode(await client.GetStringAsync(contextoEncerrado.RotaContrato));
        Assert.Contains("Este contrato não pode ser cancelado no estado atual.", erroCancelamento, StringComparison.Ordinal);

        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        Assert.Equal(
            StatusContratoFranquia.Cancelado,
            (await dbContext.ContratosFranquia.SingleAsync(item => item.Id == cancelado.ContratoId)).Status);
        Assert.Equal(
            StatusContratoFranquia.Encerrado,
            (await dbContext.ContratosFranquia.SingleAsync(item => item.Id == encerrado.ContratoId)).Status);
        Assert.Equal(
            StatusVersaoContratoFranquia.Cancelada,
            (await dbContext.ContratosFranquiaVersoes.SingleAsync(item => item.Id == cancelado.VersoesIds[0])).Status);
        Assert.Equal(
            StatusVersaoContratoFranquia.Vigente,
            (await dbContext.ContratosFranquiaVersoes.SingleAsync(item => item.Id == encerrado.VersoesIds[0])).Status);
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

    private static async Task<ContratoAtivoIds> CriarContratoAtivoAsync(
        ContratosFranquiaWebApplicationFactory application,
        Guid vinculoId)
    {
        var agoraUtc = DateTime.UtcNow;
        var contrato = new ContratoFranquia(
            Guid.NewGuid(),
            vinculoId,
            "BFA-ATIVO-001",
            StatusContratoFranquia.Ativo,
            agoraUtc);
        var versao = new ContratoFranquiaVersao(
            Guid.NewGuid(),
            contrato.Id,
            1,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 12, 31),
            8m,
            500m,
            1_000m,
            10,
            StatusVersaoContratoFranquia.Vigente,
            null,
            "Condições originais da versão 1",
            agoraUtc,
            application.AdministradorId);
        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        dbContext.ContratosFranquia.Add(contrato);
        dbContext.ContratosFranquiaVersoes.Add(versao);
        await dbContext.SaveChangesAsync();
        return new(contrato.Id, versao.Id);
    }

    private static MultipartFormDataContent CriarUploadDocumento(
        TipoDocumentoContratoFranquia tipoDocumento,
        string? token = null)
    {
        var upload = new MultipartFormDataContent();
        upload.Add(new StringContent(tipoDocumento.ToString()), "TipoDocumento");

        if (token is not null)
        {
            upload.Add(new StringContent(token), "__RequestVerificationToken");
        }

        var arquivo = new ByteArrayContent("%PDF-1.7\nAditivo contratual BFA"u8.ToArray());
        arquivo.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        upload.Add(arquivo, "Arquivo", "aditivo-contratual.pdf");
        return upload;
    }

    private static async Task<HttpResponseMessage> PostOperacaoAsync(
        HttpClient client,
        string rotaToken,
        string rotaOperacao)
    {
        var token = await ObterAntiforgeryAsync(client, rotaToken);
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token
        });
        return await client.PostAsync(rotaOperacao, form);
    }

    private static async Task<ContratoCicloFinalIds> CriarContratoParaCicloFinalAsync(
        ContratosFranquiaWebApplicationFactory application,
        Guid vinculoId,
        StatusContratoFranquia statusContrato,
        params StatusVersaoContratoFranquia[] statusVersoes)
    {
        var agoraUtc = DateTime.UtcNow;
        var contrato = new ContratoFranquia(
            Guid.NewGuid(),
            vinculoId,
            $"BFA-CICLO-{Guid.NewGuid():N}"[..24],
            statusContrato,
            agoraUtc);
        var versoes = statusVersoes.Select((status, indice) => new ContratoFranquiaVersao(
            Guid.NewGuid(),
            contrato.Id,
            indice + 1,
            new DateOnly(2026 + indice, 1, 1),
            new DateOnly(2026 + indice, 12, 31),
            8m + indice,
            500m + indice,
            1_000m + indice,
            10 + indice,
            status,
            indice == 0 ? null : $"Alteração da versão {indice + 1}",
            $"Condições históricas da versão {indice + 1}",
            agoraUtc.AddMinutes(indice),
            application.AdministradorId)).ToArray();
        var documento = new DocumentoContratoFranquia(
            Guid.NewGuid(),
            versoes[0].Id,
            TipoDocumentoContratoFranquia.Contrato,
            "historico-contratual.pdf",
            $"contratos/{contrato.Id:N}/versoes/{versoes[0].Id:N}/{Guid.NewGuid():N}.pdf",
            "application/pdf",
            128,
            new string('b', 64),
            agoraUtc,
            application.AdministradorId);

        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        dbContext.ContratosFranquia.Add(contrato);
        dbContext.ContratosFranquiaVersoes.AddRange(versoes);
        dbContext.DocumentosContratoFranquia.Add(documento);
        await dbContext.SaveChangesAsync();
        return new(contrato.Id, versoes.Select(item => item.Id).ToArray(), documento.Id);
    }

    private sealed record ContextoTeste(
        Guid FranqueadoId,
        Guid UnidadeId,
        Guid VinculoId,
        string RotaContrato);

    private sealed record ContratoIds(Guid ContratoId, Guid VersaoId, Guid DocumentoId);

    private sealed record ContratoAtivoIds(Guid ContratoId, Guid VersaoVigenteId);

    private sealed record ContratoCicloFinalIds(
        Guid ContratoId,
        IReadOnlyList<Guid> VersoesIds,
        Guid DocumentoId);

    [GeneratedRegex(
        "name=\"__RequestVerificationToken\"[^>]*value=\"(?<token>[^\"]+)\"",
        RegexOptions.CultureInvariant)]
    private static partial Regex AntiforgeryToken();
}
