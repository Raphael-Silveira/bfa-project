using System.Net;
using System.Text.RegularExpressions;
using BFA.Application.Franqueadora;
using BFA.Domain.Acessos;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BFA.IntegrationTests;

public sealed partial class FranqueadoraEndpointTests
{
    [Fact]
    public async Task Usuario_anonimo_e_redirecionado_para_login()
    {
        using var application = new FranqueadoraWebApplicationFactory();
        using var client = CreateClient(application);

        using var response = await client.GetAsync("/franqueadora");

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        var location = Assert.IsType<Uri>(response.Headers.Location).PathAndQuery;
        Assert.StartsWith("/login?", location, StringComparison.Ordinal);
        Assert.Contains("ReturnUrl=%2Ffranqueadora", location, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(PerfilAcesso.AdministradorUnidade)]
    [InlineData(PerfilAcesso.Professor)]
    public async Task Perfil_sem_administrador_rede_recebe_acesso_negado(
        PerfilAcesso perfil)
    {
        using var application = new FranqueadoraWebApplicationFactory();
        application.Acessos.Adicionar(
            application.UsuarioStore.Usuario.Id,
            Guid.NewGuid(),
            Guid.NewGuid(),
            perfil);
        using var client = CreateClient(application);
        await LoginAsync(client, application);

        using var response = await client.GetAsync("/franqueadora");

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        var location = Assert.IsType<Uri>(response.Headers.Location).PathAndQuery;
        Assert.StartsWith("/acesso-negado?", location, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Administrador_rede_recebe_painel_com_dados_da_organizacao()
    {
        using var application = new FranqueadoraWebApplicationFactory();
        var organizacaoId = Guid.NewGuid();
        application.Acessos.Adicionar(
            application.UsuarioStore.Usuario.Id,
            organizacaoId,
            null,
            PerfilAcesso.AdministradorRede);
        application.Painel.Resultado = PainelFranqueadoraResultado.Disponivel(
            new PainelFranqueadoraResumo(
                organizacaoId,
                "Brazilian Footvolley Academy",
                0,
                0,
                2,
                0));
        using var client = CreateClient(application);
        await LoginAsync(client, application);

        using var response = await client.GetAsync("/franqueadora");
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Brazilian Footvolley Academy", html, StringComparison.Ordinal);
        Assert.Contains("Visão Geral", html, StringComparison.Ordinal);
        Assert.Contains("Total de Unidades", html, StringComparison.Ordinal);
        Assert.Contains("Nenhuma unidade cadastrada ainda.", html, StringComparison.Ordinal);
        Assert.Contains("/images/brand/bfa-logo-horizontal-dark.png", html, StringComparison.Ordinal);
        Assert.Contains("/css/admin.css", html, StringComparison.Ordinal);
        Assert.Contains("/css/franqueadora.css", html, StringComparison.Ordinal);
        Assert.Contains("class=\"bfa-admin-shell\"", html, StringComparison.Ordinal);
        Assert.Contains("class=\"bfa-admin-sidebar\"", html, StringComparison.Ordinal);
        Assert.Contains("data-bs-toggle=\"offcanvas\"", html, StringComparison.Ordinal);
        Assert.Contains("aria-controls=\"bfaAdminMobileMenu\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"bfaAdminMobileMenu\"", html, StringComparison.Ordinal);
        Assert.Contains("data-bs-backdrop=\"true\"", html, StringComparison.Ordinal);
        Assert.Contains("data-bs-scroll=\"false\"", html, StringComparison.Ordinal);
        Assert.Contains("data-bs-dismiss=\"offcanvas\"", html, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Abrir menu administrativo\"", html, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Fechar menu administrativo\"", html, StringComparison.Ordinal);
        Assert.Contains("aria-current=\"page\"", html, StringComparison.Ordinal);
        Assert.Contains("class=\"bfa-admin-drawer__logout\"", html, StringComparison.Ordinal);
        Assert.Contains("action=\"/logout\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"__RequestVerificationToken\"", html, StringComparison.Ordinal);
        Assert.Equal(application.UsuarioStore.Usuario.Id, application.Painel.UltimoUsuarioId);
    }

    [Fact]
    public async Task Multiplas_organizacoes_exibem_estado_controlado()
    {
        using var application = new FranqueadoraWebApplicationFactory();
        application.Acessos.Adicionar(
            application.UsuarioStore.Usuario.Id,
            Guid.NewGuid(),
            null,
            PerfilAcesso.AdministradorRede);
        application.Painel.Resultado =
            PainelFranqueadoraResultado.SelecaoOrganizacaoNecessaria();
        using var client = CreateClient(application);
        await LoginAsync(client, application);

        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/franqueadora"));

        Assert.Contains("Seleção de Organização necessária", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Total de Unidades", html, StringComparison.Ordinal);
    }

    private static HttpClient CreateClient(FranqueadoraWebApplicationFactory application)
    {
        return application.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
    }

    private static async Task LoginAsync(
        HttpClient client,
        FranqueadoraWebApplicationFactory application)
    {
        var html = await client.GetStringAsync("/login");
        var match = AntiforgeryToken().Match(html);
        Assert.True(match.Success, "Token antiforgery não encontrado no formulário de login.");

        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = application.UsuarioStore.Email,
            ["Senha"] = application.UsuarioStore.Senha,
            ["LembrarMe"] = "false",
            ["ReturnUrl"] = string.Empty,
            ["__RequestVerificationToken"] = WebUtility.HtmlDecode(match.Groups["token"].Value)
        });
        using var response = await client.PostAsync("/login", form);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
    }

    [GeneratedRegex(
        "name=\"__RequestVerificationToken\"[^>]*value=\"(?<token>[^\"]+)\"",
        RegexOptions.CultureInvariant)]
    private static partial Regex AntiforgeryToken();
}
