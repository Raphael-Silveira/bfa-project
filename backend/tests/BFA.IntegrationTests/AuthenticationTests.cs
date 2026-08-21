using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using BFA.Domain.Acessos;
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
    public async Task Get_login_retorna_ok()
    {
        using var client = CreateClient();

        using var response = await client.GetAsync("/login");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Home_anonima_exibe_link_login()
    {
        using var client = CreateClient();

        var html = await client.GetStringAsync("/");

        Assert.Contains("href=\"/login\"", html, StringComparison.Ordinal);
        Assert.Contains(">Login</a>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Acessar sistema", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Home_autenticada_exibe_apenas_link_acessar_sistema()
    {
        using var client = CreateClient();
        ConfigurarAdministradorRede(ativo: true);
        await AutenticarAsync(client);

        var html = await client.GetStringAsync("/");

        Assert.Contains("href=\"/acessar\"", html, StringComparison.Ordinal);
        Assert.Contains("Acessar sistema", html, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"/login\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain(">Login</a>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_acessar_anonimo_redireciona_para_login()
    {
        using var client = CreateClient();

        using var response = await client.GetAsync("/acessar");

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal("/login", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Get_acessar_administrador_rede_redireciona_para_franqueadora()
    {
        using var client = CreateClient();
        ConfigurarAdministradorRede(ativo: true);
        await AutenticarAsync(client);

        using var response = await client.GetAsync("/acessar");

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal("/franqueadora", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Get_login_administrador_rede_autenticado_redireciona_para_franqueadora()
    {
        using var client = CreateClient();
        ConfigurarAdministradorRede(ativo: true);
        await AutenticarAsync(client);

        using var response = await client.GetAsync("/login");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal("/franqueadora", response.Headers.Location?.OriginalString);
        Assert.DoesNotContain("action=\"/login\"", content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_acesso_negado_retorna_ok()
    {
        using var client = CreateClient();

        var html = await client.GetStringAsync("/acesso-negado");

        Assert.Contains(
            "Você não tem permissão para acessar esta página.",
            WebUtility.HtmlDecode(html),
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/conta/entrar")]
    [InlineData("/conta/acesso-negado")]
    public async Task Rotas_get_antigas_nao_estao_disponiveis(string requestUri)
    {
        using var client = CreateClient();

        using var response = await client.GetAsync(requestUri);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("/conta/entrar")]
    [InlineData("/conta/sair")]
    public async Task Rotas_post_antigas_nao_estao_disponiveis(string requestUri)
    {
        using var client = CreateClient();

        using var response = await client.PostAsync(
            requestUri,
            new FormUrlEncodedContent([]));

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    [Fact]
    public async Task View_entrar_utiliza_auth_layout_e_logo_oficial()
    {
        using var client = CreateClient();

        var html = await client.GetStringAsync("/login");

        Assert.Contains("<body class=\"bfa-auth-page\">", html, StringComparison.Ordinal);
        Assert.Contains("action=\"/login\"", html, StringComparison.Ordinal);
        Assert.Contains("method=\"post\"", html, StringComparison.Ordinal);
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
        Assert.StartsWith("/login?", location, StringComparison.Ordinal);
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
        ConfigurarAdministradorRede(ativo: true);
        var token = await GetAntiforgeryTokenAsync(client, returnUrl);

        using var response = await PostLoginAsync(client, token, returnUrl);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal(returnUrl, response.Headers.Location?.OriginalString);
        Assert.Equal(0, _application.AcessosLogin.QuantidadeConsultasAdministradorRede);
    }

    [Fact]
    public async Task Login_nao_redireciona_para_return_url_externa()
    {
        using var client = CreateClient();
        const string returnUrl = "https://example.invalid/destino";
        ConfigurarAdministradorRede(ativo: true);
        var token = await GetAntiforgeryTokenAsync(client, returnUrl);

        using var response = await PostLoginAsync(client, token, returnUrl);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal("/franqueadora", response.Headers.Location?.OriginalString);
        Assert.Equal(1, _application.AcessosLogin.QuantidadeConsultasAdministradorRede);
    }

    [Fact]
    public async Task Administrador_rede_sem_return_url_vai_para_franqueadora()
    {
        using var client = CreateClient();
        ConfigurarAdministradorRede(ativo: true);
        var token = await GetAntiforgeryTokenAsync(client);

        using var response = await PostLoginAsync(client, token, string.Empty);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal("/franqueadora", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Usuario_sem_vinculo_ativo_sem_return_url_vai_para_acesso_negado()
    {
        using var client = CreateClient();
        _application.AcessosLogin.Limpar();
        _application.UnidadesLogin.Limpar();
        var token = await GetAntiforgeryTokenAsync(client);

        using var response = await PostLoginAsync(client, token, string.Empty);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal("/acesso-negado", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Vinculo_administrador_rede_inativo_sem_return_url_vai_para_acesso_negado()
    {
        using var client = CreateClient();
        ConfigurarAdministradorRede(ativo: false);
        var token = await GetAntiforgeryTokenAsync(client);

        using var response = await PostLoginAsync(client, token, string.Empty);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal("/acesso-negado", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Administrador_de_uma_unidade_sem_return_url_vai_direto_para_unidade()
    {
        using var client = CreateClient();
        var unidadeId = ConfigurarUnidadeAdministrada();
        var token = await GetAntiforgeryTokenAsync(client);

        using var response = await PostLoginAsync(client, token, string.Empty);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal(
            $"/unidade/{unidadeId:D}",
            response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Administrador_de_multiplas_unidades_sem_return_url_vai_para_selecao()
    {
        using var client = CreateClient();
        ConfigurarUnidadeAdministrada("BFA Tietê");
        ConfigurarUnidadeAdministrada("BFA Sorocaba", limpar: false);
        var token = await GetAntiforgeryTokenAsync(client);

        using var response = await PostLoginAsync(client, token, string.Empty);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal("/selecionar-unidade", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Administrador_de_unidade_inativa_sem_return_url_vai_para_acesso_negado()
    {
        using var client = CreateClient();
        ConfigurarUnidadeAdministrada(ativa: false);
        var token = await GetAntiforgeryTokenAsync(client);

        using var response = await PostLoginAsync(client, token, string.Empty);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal("/acesso-negado", response.Headers.Location?.OriginalString);
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

        using var response = await client.PostAsync("/login", form);
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
        Assert.Equal("/login", cookie.LoginPath);
        Assert.Equal("/acesso-negado", cookie.AccessDeniedPath);
        Assert.True(cookie.SlidingExpiration);
        Assert.Equal(TimeSpan.FromHours(8), cookie.ExpireTimeSpan);
    }

    [Fact]
    public async Task Logout_nao_aceita_get_e_post_exige_antiforgery()
    {
        using var client = CreateClient();

        using var getResponse = await client.GetAsync("/logout");
        using var postResponse = await client.PostAsync(
            "/logout",
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

        using var response = await client.PostAsync("/logout", form);

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
    [InlineData(
        "V003__criar_vinculos_acesso.sql",
        "4B347730B498F0A449CB8EE57BA1752A6350E9C884F485223858F51B1D5CACF9")]
    [InlineData(
        "V004__criar_usuarios_e_franqueados.sql",
        "AA42F834A90BA7777F27D1AB87E208C66DC6D205EA2AB3AACD888A4F4ED3AFA7")]
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

    private void ConfigurarAdministradorRede(bool ativo)
    {
        var acessos = _application.AcessosLogin;
        acessos.Limpar();
        _application.UnidadesLogin.Limpar();
        acessos.Adicionar(
            _application.UsuarioStore.Usuario.Id,
            Guid.NewGuid(),
            unidadeId: null,
            PerfilAcesso.AdministradorRede,
            ativo);
    }

    private Guid ConfigurarUnidadeAdministrada(
        string nome = "BFA Tietê",
        bool ativa = true,
        bool limpar = true)
    {
        if (limpar)
        {
            _application.AcessosLogin.Limpar();
            _application.UnidadesLogin.Limpar();
        }

        var unidadeId = Guid.NewGuid();
        _application.UnidadesLogin.Adicionar(
            _application.UsuarioStore.Usuario.Id,
            Guid.NewGuid(),
            unidadeId,
            nome,
            ativa);
        return unidadeId;
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

        return await client.PostAsync("/login", form);
    }

    private async Task AutenticarAsync(HttpClient client)
    {
        var token = await GetAntiforgeryTokenAsync(client);

        using var response = await PostLoginAsync(client, token, string.Empty);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
    }

    private static async Task<string> GetAntiforgeryTokenAsync(
        HttpClient client,
        string? returnUrl = null)
    {
        var requestUri = "/login";

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
