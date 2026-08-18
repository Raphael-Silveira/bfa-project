using System.Net;
using System.Text.RegularExpressions;
using BFA.Domain.Acessos;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BFA.IntegrationTests;

public sealed partial class AuthorizationEndpointTests
{
    [Fact]
    public async Task Usuario_anonimo_e_redirecionado_para_login()
    {
        using var application = new AuthorizationWebApplicationFactory();
        using var client = CreateClient(application);

        using var response = await client.GetAsync("/conta/admin-rede");

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        var location = Assert.IsType<Uri>(response.Headers.Location).PathAndQuery;
        Assert.StartsWith("/login?", location, StringComparison.Ordinal);
        Assert.Contains(
            "ReturnUrl=%2Fconta%2Fadmin-rede",
            location,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Usuario_autenticado_sem_administrador_rede_recebe_acesso_negado()
    {
        using var application = new AuthorizationWebApplicationFactory();
        using var client = CreateClient(application);
        await LoginAsync(client, application);

        using var response = await client.GetAsync("/conta/admin-rede");

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        var location = Assert.IsType<Uri>(response.Headers.Location).PathAndQuery;
        Assert.StartsWith("/acesso-negado?", location, StringComparison.Ordinal);
        Assert.Contains(
            "ReturnUrl=%2Fconta%2Fadmin-rede",
            location,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Usuario_com_administrador_rede_ativo_e_autorizado()
    {
        using var application = new AuthorizationWebApplicationFactory();
        application.Acessos.Adicionar(
            application.UsuarioStore.Usuario.Id,
            Guid.NewGuid(),
            null,
            PerfilAcesso.AdministradorRede);
        using var client = CreateClient(application);
        await LoginAsync(client, application);

        using var response = await client.GetAsync("/conta/admin-rede");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Administrador de rede autorizado.", content);
    }

    private static HttpClient CreateClient(AuthorizationWebApplicationFactory application)
    {
        return application.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
    }

    private static async Task LoginAsync(
        HttpClient client,
        AuthorizationWebApplicationFactory application)
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
