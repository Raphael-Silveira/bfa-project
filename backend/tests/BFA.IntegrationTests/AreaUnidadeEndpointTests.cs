using System.Net;
using System.Text.RegularExpressions;
using BFA.Application.Contratos;
using BFA.Domain.Acessos;
using BFA.Domain.Contratos;
using BFA.Domain.Franqueados;
using BFA.Domain.Organizacoes;
using BFA.Domain.Unidades;
using BFA.Domain.Usuarios;
using BFA.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BFA.IntegrationTests;

public sealed partial class AreaUnidadeEndpointTests
{
    private static readonly DateTime CriadoEmUtc = new(
        2026,
        8,
        20,
        10,
        0,
        0,
        DateTimeKind.Utc);

    [Fact]
    public async Task Usuario_anonimo_e_redirecionado_para_login()
    {
        using var application = new AreaUnidadeWebApplicationFactory();
        using var client = CreateClient(application);

        using var response = await client.GetAsync($"/unidade/{Guid.NewGuid():D}");

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.StartsWith(
            "/login?",
            response.Headers.Location?.PathAndQuery,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Administrador_unidade_acessa_dashboard_com_contexto_e_shell_compartilhado()
    {
        using var application = new AreaUnidadeWebApplicationFactory();
        var organizacao = await AdicionarOrganizacaoAsync(application, "BFA", "bfa");
        var unidade = await AdicionarUnidadeAsync(application, organizacao.Id, "BFA Tietê");
        await AdicionarVinculoAsync(
            application,
            application.UsuarioStore.Usuario.Id,
            organizacao.Id,
            unidade.Id,
            PerfilAcesso.AdministradorUnidade);
        using var client = CreateClient(application);
        await LoginAsync(client, application);

        using var response = await client.GetAsync($"/unidade/{unidade.Id:D}");
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("BFA Tietê", html, StringComparison.Ordinal);
        Assert.Contains("Visão Geral", html, StringComparison.Ordinal);
        Assert.Contains(
            "Visão geral da operação da Unidade.",
            html,
            StringComparison.Ordinal);
        Assert.Contains("Contrato da franquia", html, StringComparison.Ordinal);
        Assert.Contains(
            "Nenhum contrato ativo disponível para esta unidade.",
            html,
            StringComparison.Ordinal);
        Assert.Contains("class=\"bfa-admin-shell\"", html, StringComparison.Ordinal);
        Assert.Contains("class=\"bfa-admin-sidebar\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"bfaAdminMobileMenu\"", html, StringComparison.Ordinal);
        Assert.Contains("data-bs-toggle=\"offcanvas\"", html, StringComparison.Ordinal);
        Assert.Contains("action=\"/logout\"", html, StringComparison.Ordinal);
        Assert.Contains("/css/admin.css", html, StringComparison.Ordinal);
        Assert.Contains("/css/unidade.css", html, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"/franqueadora", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Voltar à rede", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Trocar unidade", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Administrador_rede_entra_na_unidade_propria_com_retorno_e_nao_acessa_outro_tenant()
    {
        using var application = new AreaUnidadeWebApplicationFactory();
        var organizacao = await AdicionarOrganizacaoAsync(
            application, "BFA", $"bfa-rede-{Guid.NewGuid():N}");
        var externa = await AdicionarOrganizacaoAsync(
            application, "Outra", $"outra-rede-{Guid.NewGuid():N}");
        var unidade = await AdicionarUnidadeAsync(application, organizacao.Id, "BFA Tatuí");
        var unidadeExterna = await AdicionarUnidadeAsync(
            application, externa.Id, "Unidade externa");
        await AdicionarVinculoAsync(application, application.UsuarioStore.Usuario.Id,
            organizacao.Id, null, PerfilAcesso.AdministradorRede);
        using var client = CreateClient(application);
        await LoginAsync(client, application);

        using var permitida = await client.GetAsync($"/unidade/{unidade.Id:D}");
        var html = WebUtility.HtmlDecode(await permitida.Content.ReadAsStringAsync());
        using var proibida = await client.GetAsync($"/unidade/{unidadeExterna.Id:D}");

        Assert.Equal(HttpStatusCode.OK, permitida.StatusCode);
        Assert.Contains("Operação administrada pela Rede", html, StringComparison.Ordinal);
        Assert.Contains("href=\"/franqueadora\"", html, StringComparison.Ordinal);
        Assert.Contains("Voltar à rede", html, StringComparison.Ordinal);
        AssertAcessoNegado(proibida);
    }

    [Fact]
    public async Task Multiplas_unidades_exibem_troca_de_contexto()
    {
        using var application = new AreaUnidadeWebApplicationFactory();
        var organizacao = await AdicionarOrganizacaoAsync(application, "BFA", "bfa");
        var unidadeA = await AdicionarUnidadeAsync(application, organizacao.Id, "BFA A");
        var unidadeB = await AdicionarUnidadeAsync(application, organizacao.Id, "BFA B");
        await AdicionarVinculoAsync(
            application,
            application.UsuarioStore.Usuario.Id,
            organizacao.Id,
            unidadeA.Id,
            PerfilAcesso.AdministradorUnidade);
        await AdicionarVinculoAsync(
            application,
            application.UsuarioStore.Usuario.Id,
            organizacao.Id,
            unidadeB.Id,
            PerfilAcesso.AdministradorUnidade);
        using var client = CreateClient(application);
        await LoginAsync(client, application);

        var html = WebUtility.HtmlDecode(await client.GetStringAsync(
            $"/unidade/{unidadeA.Id:D}"));

        Assert.Contains("href=\"/selecionar-unidade\"", html, StringComparison.Ordinal);
        Assert.Contains("Trocar unidade", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Usuario_nao_acessa_unidade_de_outro_usuario_ou_outro_tenant()
    {
        using var application = new AreaUnidadeWebApplicationFactory();
        var organizacao = await AdicionarOrganizacaoAsync(application, "BFA", "bfa");
        var outraOrganizacao = await AdicionarOrganizacaoAsync(
            application,
            "Outra",
            "outra");
        var propria = await AdicionarUnidadeAsync(application, organizacao.Id, "BFA Própria");
        var mesmoTenant = await AdicionarUnidadeAsync(application, organizacao.Id, "BFA Outra");
        var outroTenant = await AdicionarUnidadeAsync(
            application,
            outraOrganizacao.Id,
            "Externa");
        await AdicionarVinculoAsync(
            application,
            application.UsuarioStore.Usuario.Id,
            organizacao.Id,
            propria.Id,
            PerfilAcesso.AdministradorUnidade);
        await AdicionarVinculoAsync(
            application,
            Guid.NewGuid(),
            organizacao.Id,
            mesmoTenant.Id,
            PerfilAcesso.AdministradorUnidade);
        await AdicionarVinculoAsync(
            application,
            Guid.NewGuid(),
            outraOrganizacao.Id,
            outroTenant.Id,
            PerfilAcesso.AdministradorUnidade);
        using var client = CreateClient(application);
        await LoginAsync(client, application);

        using var mesmaOrganizacao = await client.GetAsync($"/unidade/{mesmoTenant.Id:D}");
        using var outra = await client.GetAsync($"/unidade/{outroTenant.Id:D}");

        AssertAcessoNegado(mesmaOrganizacao);
        AssertAcessoNegado(outra);
    }

    [Fact]
    public async Task Desativar_vinculo_invalida_proxima_requisicao_da_sessao()
    {
        using var application = new AreaUnidadeWebApplicationFactory();
        var organizacao = await AdicionarOrganizacaoAsync(application, "BFA", "bfa");
        var unidade = await AdicionarUnidadeAsync(application, organizacao.Id, "BFA Tietê");
        var vinculo = await AdicionarVinculoAsync(
            application,
            application.UsuarioStore.Usuario.Id,
            organizacao.Id,
            unidade.Id,
            PerfilAcesso.AdministradorUnidade);
        using var client = CreateClient(application);
        await LoginAsync(client, application);
        Assert.Equal(
            HttpStatusCode.OK,
            (await client.GetAsync($"/unidade/{unidade.Id:D}")).StatusCode);
        await DesativarVinculoAsync(application, vinculo.Id);

        using var response = await client.GetAsync($"/unidade/{unidade.Id:D}");

        AssertAcessoNegado(response);
    }

    [Fact]
    public async Task Unidade_inativa_nao_define_destino_e_url_direta_nao_expoe_contexto()
    {
        using var application = new AreaUnidadeWebApplicationFactory();
        var organizacao = await AdicionarOrganizacaoAsync(application, "BFA", "bfa");
        var unidade = await AdicionarUnidadeAsync(
            application,
            organizacao.Id,
            "BFA Inativa",
            ativa: false);
        await AdicionarVinculoAsync(
            application,
            application.UsuarioStore.Usuario.Id,
            organizacao.Id,
            unidade.Id,
            PerfilAcesso.AdministradorUnidade);
        using var client = CreateClient(application);

        using var login = await LoginAsync(client, application);
        using var direta = await client.GetAsync($"/unidade/{unidade.Id:D}");

        Assert.Equal("/acesso-negado", login.Headers.Location?.OriginalString);
        Assert.Equal(HttpStatusCode.NotFound, direta.StatusCode);
    }

    [Fact]
    public async Task Selecao_lista_apenas_unidades_permitidas_e_revalida_post()
    {
        using var application = new AreaUnidadeWebApplicationFactory();
        var organizacao = await AdicionarOrganizacaoAsync(application, "BFA", "bfa");
        var permitidaA = await AdicionarUnidadeAsync(application, organizacao.Id, "BFA A");
        var permitidaB = await AdicionarUnidadeAsync(application, organizacao.Id, "BFA B");
        var proibida = await AdicionarUnidadeAsync(application, organizacao.Id, "BFA Restrita");
        await AdicionarVinculoAsync(
            application,
            application.UsuarioStore.Usuario.Id,
            organizacao.Id,
            permitidaA.Id,
            PerfilAcesso.AdministradorUnidade);
        await AdicionarVinculoAsync(
            application,
            application.UsuarioStore.Usuario.Id,
            organizacao.Id,
            permitidaB.Id,
            PerfilAcesso.AdministradorUnidade);
        await AdicionarVinculoAsync(
            application,
            Guid.NewGuid(),
            organizacao.Id,
            proibida.Id,
            PerfilAcesso.AdministradorUnidade);
        await AdicionarPerfilUsuarioAsync(
            application,
            application.UsuarioStore.Usuario.Id,
            "Carolina Almeida");
        using var client = CreateClient(application);
        await LoginAsync(client, application);

        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/selecionar-unidade"));
        var token = ObterAntiforgery(html);
        Assert.Contains("BFA A", html, StringComparison.Ordinal);
        Assert.Contains("BFA B", html, StringComparison.Ordinal);
        Assert.Contains("Bem-vindo, Carolina", html, StringComparison.Ordinal);
        Assert.Contains("Selecione uma unidade", html, StringComparison.Ordinal);
        Assert.Contains(
            "Escolha a unidade que deseja administrar.",
            html,
            StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Acessar unidade BFA A\"", html, StringComparison.Ordinal);
        Assert.Contains("Acessar unidade", html, StringComparison.Ordinal);
        Assert.Contains("action=\"/logout\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Área administrativa", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("contexto que deseja administrar", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("BFA Restrita", html, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"OrganizacaoId\"", html, StringComparison.Ordinal);

        using var permitido = await client.PostAsync(
            "/selecionar-unidade",
            FormSelecao(token, permitidaB.Id));
        Assert.Equal(HttpStatusCode.Found, permitido.StatusCode);
        Assert.Equal(
            $"/unidade/{permitidaB.Id:D}",
            permitido.Headers.Location?.OriginalString);

        using var proibido = await client.PostAsync(
            "/selecionar-unidade",
            FormSelecao(token, proibida.Id));
        AssertAcessoNegado(proibido);

        using var logout = await client.PostAsync(
            "/logout",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token
            }));
        Assert.Equal(HttpStatusCode.Found, logout.StatusCode);
        Assert.Equal("/", logout.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Selecao_sem_perfil_usuario_oculta_saudacao_sem_quebrar_pagina()
    {
        using var application = new AreaUnidadeWebApplicationFactory();
        var organizacao = await AdicionarOrganizacaoAsync(application, "BFA", "bfa");
        var unidadeA = await AdicionarUnidadeAsync(application, organizacao.Id, "BFA A");
        var unidadeB = await AdicionarUnidadeAsync(application, organizacao.Id, "BFA B");
        await AdicionarVinculoAsync(
            application,
            application.UsuarioStore.Usuario.Id,
            organizacao.Id,
            unidadeA.Id,
            PerfilAcesso.AdministradorUnidade);
        await AdicionarVinculoAsync(
            application,
            application.UsuarioStore.Usuario.Id,
            organizacao.Id,
            unidadeB.Id,
            PerfilAcesso.AdministradorUnidade);
        using var client = CreateClient(application);
        await LoginAsync(client, application);

        using var response = await client.GetAsync("/selecionar-unidade");
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Selecione uma unidade", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Bem-vindo,", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Selecao_post_exige_antiforgery()
    {
        using var application = new AreaUnidadeWebApplicationFactory();
        var organizacao = await AdicionarOrganizacaoAsync(application, "BFA", "bfa");
        var unidade = await AdicionarUnidadeAsync(application, organizacao.Id, "BFA A");
        await AdicionarVinculoAsync(
            application,
            application.UsuarioStore.Usuario.Id,
            organizacao.Id,
            unidade.Id,
            PerfilAcesso.AdministradorUnidade);
        using var client = CreateClient(application);
        await LoginAsync(client, application);

        using var response = await client.PostAsync(
            "/selecionar-unidade",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["unidadeId"] = unidade.Id.ToString()
            }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Administrador_rede_tem_superacesso_somente_na_propria_organizacao()
    {
        using var application = new AreaUnidadeWebApplicationFactory();
        var organizacao = await AdicionarOrganizacaoAsync(application, "BFA", "bfa");
        var outraOrganizacao = await AdicionarOrganizacaoAsync(
            application,
            "Outra",
            "outra");
        var unidadePropria = await AdicionarUnidadeAsync(
            application,
            organizacao.Id,
            "BFA Tietê");
        var unidadeExterna = await AdicionarUnidadeAsync(
            application,
            outraOrganizacao.Id,
            "Externa");
        await AdicionarVinculoAsync(
            application,
            application.UsuarioStore.Usuario.Id,
            organizacao.Id,
            unidadeId: null,
            PerfilAcesso.AdministradorRede);
        using var client = CreateClient(application);
        await LoginAsync(client, application);

        using var propria = await client.GetAsync($"/unidade/{unidadePropria.Id:D}");
        using var externa = await client.GetAsync($"/unidade/{unidadeExterna.Id:D}");

        Assert.Equal(HttpStatusCode.OK, propria.StatusCode);
        AssertAcessoNegado(externa);
    }

    [Fact]
    public async Task Administrador_unidade_visualiza_resumo_detalhe_e_documento_vigente()
    {
        using var application = new AreaUnidadeWebApplicationFactory();
        var organizacao = await AdicionarOrganizacaoAsync(application, "BFA", "bfa");
        var unidade = await AdicionarUnidadeAsync(application, organizacao.Id, "BFA Tietê");
        await AdicionarVinculoAsync(
            application,
            application.UsuarioStore.Usuario.Id,
            organizacao.Id,
            unidade.Id,
            PerfilAcesso.AdministradorUnidade);
        var contrato = await AdicionarContratoAtivoAsync(
            application,
            organizacao.Id,
            unidade.Id);
        using var client = CreateClient(application);
        await LoginAsync(client, application);

        var painel = WebUtility.HtmlDecode(await client.GetStringAsync(
            $"/unidade/{unidade.Id:D}"));
        Assert.Contains("Contrato da franquia", painel, StringComparison.Ordinal);
        Assert.Contains("Contrato nº BFA-UN-123", painel, StringComparison.Ordinal);
        Assert.Contains("Versão 2 · Vigente", painel, StringComparison.Ordinal);
        Assert.Contains("22/08/2026 a 22/08/2027", painel, StringComparison.Ordinal);
        Assert.Contains("8,00%", painel, StringComparison.Ordinal);
        Assert.Contains("R$ 500,00", painel, StringComparison.Ordinal);
        Assert.Contains("R$ 10.000,00", painel, StringComparison.Ordinal);
        Assert.Contains("Dia 20", painel, StringComparison.Ordinal);
        Assert.Contains(
            $"href=\"/unidade/{unidade.Id:D}/contrato\"",
            painel,
            StringComparison.Ordinal);

        using var detalheResponse = await client.GetAsync(
            $"/unidade/{unidade.Id:D}/contrato");
        var detalhe = WebUtility.HtmlDecode(await detalheResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, detalheResponse.StatusCode);
        Assert.Contains("Consulte as condições vigentes", detalhe, StringComparison.Ordinal);
        Assert.Contains("Taxa de franquia", detalhe, StringComparison.Ordinal);
        Assert.Contains("Operação regular da unidade.", detalhe, StringComparison.Ordinal);
        Assert.Contains("contrato-unidade.pdf", detalhe, StringComparison.Ordinal);
        Assert.DoesNotContain(contrato.ChaveArmazenamento, detalhe, StringComparison.Ordinal);
        Assert.DoesNotContain("/storage", detalhe, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(">Editar<", detalhe, StringComparison.Ordinal);
        Assert.DoesNotContain("Nova versão", detalhe, StringComparison.Ordinal);
        Assert.DoesNotContain("Formalizar", detalhe, StringComparison.Ordinal);
        Assert.DoesNotContain("Cancelar contrato", detalhe, StringComparison.Ordinal);
        Assert.DoesNotContain("Encerrar contrato", detalhe, StringComparison.Ordinal);
        Assert.DoesNotContain("Enviar documento", detalhe, StringComparison.Ordinal);

        using var visualizacao = await client.GetAsync(
            $"/unidade/{unidade.Id:D}/contrato/documentos/{contrato.DocumentoId:D}/visualizar");
        using var download = await client.GetAsync(
            $"/unidade/{unidade.Id:D}/contrato/documentos/{contrato.DocumentoId:D}/baixar");
        Assert.Equal(HttpStatusCode.OK, visualizacao.StatusCode);
        Assert.Equal("application/pdf", visualizacao.Content.Headers.ContentType?.MediaType);
        Assert.Equal(contrato.Conteudo, await visualizacao.Content.ReadAsByteArrayAsync());
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("attachment", download.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal(contrato.Conteudo, await download.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Ausencia_de_contrato_ativo_exibe_estado_controlado_sem_acao_de_criacao()
    {
        using var application = new AreaUnidadeWebApplicationFactory();
        var organizacao = await AdicionarOrganizacaoAsync(application, "BFA", "bfa");
        var unidade = await AdicionarUnidadeAsync(application, organizacao.Id, "BFA Sem Contrato");
        await AdicionarVinculoAsync(
            application,
            application.UsuarioStore.Usuario.Id,
            organizacao.Id,
            unidade.Id,
            PerfilAcesso.AdministradorUnidade);
        using var client = CreateClient(application);
        await LoginAsync(client, application);

        var painel = await client.GetStringAsync($"/unidade/{unidade.Id:D}");
        using var detalheResponse = await client.GetAsync($"/unidade/{unidade.Id:D}/contrato");
        var detalhe = await detalheResponse.Content.ReadAsStringAsync();

        Assert.Contains(
            "Nenhum contrato ativo disponível para esta unidade.",
            painel,
            StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, detalheResponse.StatusCode);
        Assert.Contains(
            "Nenhum contrato ativo disponível para esta unidade.",
            detalhe,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Criar contrato", painel, StringComparison.Ordinal);
        Assert.DoesNotContain("Criar contrato", detalhe, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Administrador_unidade_nao_acessa_contrato_ou_documento_de_outra_unidade_ou_tenant()
    {
        using var application = new AreaUnidadeWebApplicationFactory();
        var organizacao = await AdicionarOrganizacaoAsync(application, "BFA", "bfa");
        var outroTenant = await AdicionarOrganizacaoAsync(application, "Outra", "outra");
        var propria = await AdicionarUnidadeAsync(application, organizacao.Id, "BFA Própria");
        var restrita = await AdicionarUnidadeAsync(application, organizacao.Id, "BFA Restrita");
        var externa = await AdicionarUnidadeAsync(application, outroTenant.Id, "BFA Externa");
        await AdicionarVinculoAsync(
            application,
            application.UsuarioStore.Usuario.Id,
            organizacao.Id,
            propria.Id,
            PerfilAcesso.AdministradorUnidade);
        var contratoRestrito = await AdicionarContratoAtivoAsync(
            application,
            organizacao.Id,
            restrita.Id);
        var contratoExterno = await AdicionarContratoAtivoAsync(
            application,
            outroTenant.Id,
            externa.Id);
        using var client = CreateClient(application);
        await LoginAsync(client, application);

        using var detalheRestrito = await client.GetAsync($"/unidade/{restrita.Id:D}/contrato");
        using var documentoRestrito = await client.GetAsync(
            $"/unidade/{restrita.Id:D}/contrato/documentos/{contratoRestrito.DocumentoId:D}/baixar");
        using var detalheExterno = await client.GetAsync($"/unidade/{externa.Id:D}/contrato");
        using var documentoExterno = await client.GetAsync(
            $"/unidade/{externa.Id:D}/contrato/documentos/{contratoExterno.DocumentoId:D}/visualizar");

        AssertAcessoNegado(detalheRestrito);
        AssertAcessoNegado(documentoRestrito);
        AssertAcessoNegado(detalheExterno);
        AssertAcessoNegado(documentoExterno);
    }

    private static HttpClient CreateClient(AreaUnidadeWebApplicationFactory application)
    {
        return application.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
    }

    private static async Task<HttpResponseMessage> LoginAsync(
        HttpClient client,
        AreaUnidadeWebApplicationFactory application)
    {
        using var loginPage = await client.GetAsync("/login");
        var html = await loginPage.Content.ReadAsStringAsync();
        Assert.True(
            loginPage.IsSuccessStatusCode,
            $"A página de login retornou {(int)loginPage.StatusCode}: {html}");
        var token = ObterAntiforgery(html);
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = application.UsuarioStore.Email,
            ["Senha"] = application.UsuarioStore.Senha,
            ["LembrarMe"] = "false",
            ["ReturnUrl"] = string.Empty,
            ["__RequestVerificationToken"] = token
        });
        var response = await client.PostAsync("/login", form);
        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        return response;
    }

    private static FormUrlEncodedContent FormSelecao(string token, Guid unidadeId)
    {
        return new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["unidadeId"] = unidadeId.ToString(),
            ["__RequestVerificationToken"] = token
        });
    }

    private static string ObterAntiforgery(string html)
    {
        var match = AntiforgeryToken().Match(html);
        Assert.True(match.Success, "Token antiforgery não encontrado.");
        return WebUtility.HtmlDecode(match.Groups["token"].Value);
    }

    private static void AssertAcessoNegado(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.StartsWith(
            "/acesso-negado?",
            response.Headers.Location?.PathAndQuery,
            StringComparison.Ordinal);
    }

    private static async Task<Organizacao> AdicionarOrganizacaoAsync(
        AreaUnidadeWebApplicationFactory application,
        string nome,
        string slug)
    {
        var organizacao = new Organizacao(Guid.NewGuid(), nome, slug, CriadoEmUtc);
        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        dbContext.Organizacoes.Add(organizacao);
        await dbContext.SaveChangesAsync();
        return organizacao;
    }

    private static async Task<Unidade> AdicionarUnidadeAsync(
        AreaUnidadeWebApplicationFactory application,
        Guid organizacaoId,
        string nome,
        bool ativa = true)
    {
        var unidade = new Unidade(
            Guid.NewGuid(),
            organizacaoId,
            nome,
            $"unidade-{Guid.NewGuid():N}",
            CriadoEmUtc);

        if (!ativa)
        {
            unidade.Desativar(CriadoEmUtc.AddHours(1));
        }

        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        dbContext.Unidades.Add(unidade);
        await dbContext.SaveChangesAsync();
        return unidade;
    }

    private static async Task<VinculoAcesso> AdicionarVinculoAsync(
        AreaUnidadeWebApplicationFactory application,
        Guid usuarioId,
        Guid organizacaoId,
        Guid? unidadeId,
        PerfilAcesso perfil)
    {
        var vinculo = new VinculoAcesso(
            Guid.NewGuid(),
            usuarioId,
            organizacaoId,
            unidadeId,
            perfil,
            CriadoEmUtc);
        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        dbContext.VinculosAcesso.Add(vinculo);
        await dbContext.SaveChangesAsync();
        return vinculo;
    }

    private static async Task AdicionarPerfilUsuarioAsync(
        AreaUnidadeWebApplicationFactory application,
        Guid usuarioId,
        string nomeCompleto)
    {
        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        dbContext.PerfisUsuario.Add(new PerfilUsuario(
            Guid.NewGuid(),
            usuarioId,
            nomeCompleto,
            telefone: null,
            CriadoEmUtc));
        await dbContext.SaveChangesAsync();
    }

    private static async Task<ContratoUnidadeTeste> AdicionarContratoAtivoAsync(
        AreaUnidadeWebApplicationFactory application,
        Guid organizacaoId,
        Guid unidadeId)
    {
        var franqueado = new Franqueado(
            Guid.NewGuid(),
            organizacaoId,
            TipoPessoaFranqueado.PessoaJuridica,
            $"Franqueado {unidadeId:N}",
            $"{unidadeId:N}"[..12] + "99",
            $"franqueado-{unidadeId:N}@bfa.test",
            CriadoEmUtc);
        var vinculo = new FranqueadoUnidade(
            Guid.NewGuid(),
            franqueado.Id,
            organizacaoId,
            unidadeId,
            CriadoEmUtc);
        var contrato = new ContratoFranquia(
            Guid.NewGuid(),
            vinculo.Id,
            "BFA-UN-123",
            StatusContratoFranquia.Ativo,
            CriadoEmUtc);
        var versao = new ContratoFranquiaVersao(
            Guid.NewGuid(),
            contrato.Id,
            2,
            new DateOnly(2026, 8, 22),
            new DateOnly(2027, 8, 22),
            8m,
            500m,
            10_000m,
            20,
            StatusVersaoContratoFranquia.Vigente,
            "Renovação contratual",
            "Operação regular da unidade.",
            CriadoEmUtc,
            application.UsuarioStore.Usuario.Id);
        var conteudo = "%PDF-1.7\nContrato da unidade BFA"u8.ToArray();
        var documentoId = Guid.NewGuid();
        var chave = $"contratos/{contrato.Id:N}/versoes/{versao.Id:N}/{documentoId:N}.pdf";
        var documento = new DocumentoContratoFranquia(
            documentoId,
            versao.Id,
            TipoDocumentoContratoFranquia.Contrato,
            "contrato-unidade.pdf",
            chave,
            "application/pdf",
            conteudo.Length,
            new string('a', 64),
            CriadoEmUtc,
            application.UsuarioStore.Usuario.Id);

        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        dbContext.Franqueados.Add(franqueado);
        dbContext.FranqueadosUnidades.Add(vinculo);
        dbContext.ContratosFranquia.Add(contrato);
        dbContext.ContratosFranquiaVersoes.Add(versao);
        dbContext.DocumentosContratoFranquia.Add(documento);
        await dbContext.SaveChangesAsync();
        var armazenamento = scope.ServiceProvider
            .GetRequiredService<IArmazenamentoDocumentosContrato>();
        await using var stream = new MemoryStream(conteudo);
        await armazenamento.SalvarAsync(chave, stream);
        return new(documentoId, chave, conteudo);
    }

    private static async Task DesativarVinculoAsync(
        AreaUnidadeWebApplicationFactory application,
        Guid vinculoId)
    {
        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        var vinculo = await dbContext.VinculosAcesso.SingleAsync(
            item => item.Id == vinculoId);
        vinculo.Desativar(CriadoEmUtc.AddHours(2));
        await dbContext.SaveChangesAsync();
    }

    private sealed record ContratoUnidadeTeste(
        Guid DocumentoId,
        string ChaveArmazenamento,
        byte[] Conteudo);

    [GeneratedRegex(
        "name=\"__RequestVerificationToken\"[^>]*value=\"(?<token>[^\"]+)\"",
        RegexOptions.CultureInvariant)]
    private static partial Regex AntiforgeryToken();
}
