using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using BFA.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BFA.IntegrationTests;

public sealed partial class AuthenticationTests : IClassFixture<LoginWebApplicationFactory>
{
    private readonly LoginWebApplicationFactory _application;

    public AuthenticationTests(LoginWebApplicationFactory application)
    {
        _application = application;
    }

    [Fact]
    public async Task Get_entrar_retorna_ok()
    {
        using var client = CreateClient();

        using var response = await client.GetAsync("/conta/entrar");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task View_entrar_utiliza_auth_layout_e_logo_oficial()
    {
        using var client = CreateClient();

        var html = await client.GetStringAsync("/conta/entrar");

        Assert.Contains("<body class=\"bfa-auth-page\">", html, StringComparison.Ordinal);
        Assert.Contains(
            "/images/brand/bfa-logo-principal-dark.png",
            html,
            StringComparison.Ordinal);
        Assert.DoesNotContain("<nav", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Usuario_anonimo_e_redirecionado_com_return_url_local()
    {
        using var client = CreateClient();

        using var response = await client.GetAsync("/conta/autenticado");

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        var location = Assert.IsType<Uri>(response.Headers.Location).PathAndQuery;
        Assert.StartsWith("/conta/entrar?", location, StringComparison.Ordinal);
        Assert.Contains(
            "ReturnUrl=%2Fconta%2Fautenticado",
            location,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Login_redireciona_para_return_url_local()
    {
        using var client = CreateClient();
        const string returnUrl = "/conta/autenticado";
        var token = await GetAntiforgeryTokenAsync(client, returnUrl);

        using var response = await PostLoginAsync(client, token, returnUrl);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal(returnUrl, response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Login_nao_redireciona_para_return_url_externa()
    {
        using var client = CreateClient();
        const string returnUrl = "https://example.invalid/destino";
        var token = await GetAntiforgeryTokenAsync(client, returnUrl);

        using var response = await PostLoginAsync(client, token, returnUrl);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal("/", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Falha_de_login_exibe_mensagem_generica()
    {
        using var client = CreateClient();
        var token = await GetAntiforgeryTokenAsync(client);
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = $"ausente-{Guid.NewGuid():N}@example.invalid",
            ["Senha"] = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),
            ["LembrarMe"] = "false",
            ["ReturnUrl"] = string.Empty,
            ["__RequestVerificationToken"] = token
        });

        using var response = await client.PostAsync("/conta/entrar", form);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Email ou senha inválidos.", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Application_cookie_possui_configuracao_segura()
    {
        var authentication = _application.Services
            .GetRequiredService<IOptions<AuthenticationOptions>>()
            .Value;
        var cookie = _application.Services
            .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(IdentityConstants.ApplicationScheme);

        Assert.Equal(IdentityConstants.ApplicationScheme, authentication.DefaultScheme);
        Assert.Equal("BFA.Auth", cookie.Cookie.Name);
        Assert.True(cookie.Cookie.HttpOnly);
        Assert.Equal(SameSiteMode.Lax, cookie.Cookie.SameSite);
        Assert.Equal(CookieSecurePolicy.Always, cookie.Cookie.SecurePolicy);
        Assert.Equal("/conta/entrar", cookie.LoginPath);
        Assert.Equal("/conta/acesso-negado", cookie.AccessDeniedPath);
        Assert.True(cookie.SlidingExpiration);
        Assert.Equal(TimeSpan.FromHours(8), cookie.ExpireTimeSpan);
    }

    [Fact]
    public async Task Logout_nao_aceita_get_e_post_exige_antiforgery()
    {
        using var client = CreateClient();

        using var getResponse = await client.GetAsync("/conta/sair");
        using var postResponse = await client.PostAsync(
            "/conta/sair",
            new FormUrlEncodedContent([]));

        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, postResponse.StatusCode);
    }

    [Fact]
    public async Task Logout_aceita_post_com_antiforgery_e_redireciona_para_inicio()
    {
        using var client = CreateClient();
        var token = await GetAntiforgeryTokenAsync(client);
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token
        });

        using var response = await client.PostAsync("/conta/sair", form);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal("/", response.Headers.Location?.OriginalString);
    }

    [Theory]
    [InlineData(
        "V001__criar_organizacoes_e_unidades.sql",
        "8D458CEFD177E176D4FFB1C3D6D07AB2FFE2C6BB0C6FA97E568470DB54D697B3")]
    [InlineData(
        "V002__criar_identidade.sql",
        "3819E7472B1E75B711EBA36900BE816B7F8B527354DB7BECD0FB11685A8D4B15")]
    public async Task Migration_executada_permanece_inalterada(
        string fileName,
        string expectedHash)
    {
        var migrationPath = Path.Combine(
            FindRepositoryRoot(),
            "database",
            "migrations",
            fileName);

        await using var migration = File.OpenRead(migrationPath);
        var hash = Convert.ToHexString(await SHA256.HashDataAsync(migration));

        Assert.Equal(expectedHash, hash);
    }

    private HttpClient CreateClient()
    {
        return _application.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
    }

    private async Task<HttpResponseMessage> PostLoginAsync(
        HttpClient client,
        string token,
        string returnUrl)
    {
        var usuarioStore = _application.UsuarioStore;
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = usuarioStore.Email,
            ["Senha"] = usuarioStore.Senha,
            ["LembrarMe"] = "false",
            ["ReturnUrl"] = returnUrl,
            ["__RequestVerificationToken"] = token
        });

        return await client.PostAsync("/conta/entrar", form);
    }

    private static async Task<string> GetAntiforgeryTokenAsync(
        HttpClient client,
        string? returnUrl = null)
    {
        var requestUri = "/conta/entrar";

        if (returnUrl is not null)
        {
            requestUri += $"?returnUrl={Uri.EscapeDataString(returnUrl)}";
        }

        var html = await client.GetStringAsync(requestUri);
        var match = AntiforgeryToken().Match(html);

        Assert.True(match.Success, "Token antiforgery não encontrado no formulário de login.");
        return WebUtility.HtmlDecode(match.Groups["token"].Value);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
        {
            directory = directory.Parent;
        }

        return Assert.IsType<DirectoryInfo>(directory).FullName;
    }

    [GeneratedRegex(
        "name=\"__RequestVerificationToken\"[^>]*value=\"(?<token>[^\"]+)\"",
        RegexOptions.CultureInvariant)]
    private static partial Regex AntiforgeryToken();
}
