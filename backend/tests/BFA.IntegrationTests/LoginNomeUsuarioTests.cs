using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BFA.IntegrationTests;

public sealed partial class LoginNomeUsuarioTests : IClassFixture<LoginWebApplicationFactory>
{
    private readonly LoginWebApplicationFactory _application;

    public LoginNomeUsuarioTests(LoginWebApplicationFactory application)
    {
        _application = application;
    }

    [Fact]
    public async Task Login_aceita_nome_usuario_que_nao_e_email()
    {
        const string nomeUsuario = "professor.cerquilho";
        _application.UsuarioStore.Usuario.UserName = nomeUsuario;
        _application.UsuarioStore.Usuario.NormalizedUserName = nomeUsuario.ToUpperInvariant();
        using var client = _application.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
        var html = await client.GetStringAsync("/login");
        var token = WebUtility.HtmlDecode(
            AntiforgeryToken().Match(html).Groups["token"].Value);
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = nomeUsuario,
            ["Senha"] = _application.UsuarioStore.Senha,
            ["LembrarMe"] = "false",
            ["ReturnUrl"] = "/",
            ["__RequestVerificationToken"] = token
        });

        using var response = await client.PostAsync("/login", form);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal("/", response.Headers.Location?.OriginalString);
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\"[^>]*value=\"(?<token>[^\"]+)\"")]
    private static partial Regex AntiforgeryToken();
}
