using System.Net;
using System.Text.RegularExpressions;
using BFA.Domain.Acessos;
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
        Assert.Contains("Sua área de gestão está pronta.", html, StringComparison.Ordinal);
        Assert.Contains("class=\"bfa-admin-shell\"", html, StringComparison.Ordinal);
        Assert.Contains("class=\"bfa-admin-sidebar\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"bfaAdminMobileMenu\"", html, StringComparison.Ordinal);
        Assert.Contains("data-bs-toggle=\"offcanvas\"", html, StringComparison.Ordinal);
        Assert.Contains("action=\"/logout\"", html, StringComparison.Ordinal);
        Assert.Contains("/css/admin.css", html, StringComparison.Ordinal);
        Assert.Contains("/css/unidade.css", html, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"/franqueadora", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Trocar unidade", html, StringComparison.Ordinal);
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

    [GeneratedRegex(
        "name=\"__RequestVerificationToken\"[^>]*value=\"(?<token>[^\"]+)\"",
        RegexOptions.CultureInvariant)]
    private static partial Regex AntiforgeryToken();
}
