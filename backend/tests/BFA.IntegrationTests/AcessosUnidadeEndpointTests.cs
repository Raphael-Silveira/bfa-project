using System.Net;
using System.Text.RegularExpressions;
using BFA.Domain.Acessos;
using BFA.Domain.Unidades;
using BFA.Infrastructure.Identity;
using BFA.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BFA.IntegrationTests;

public sealed partial class AcessosUnidadeEndpointTests
{
    [Fact]
    public async Task Get_anonimo_redireciona_para_login()
    {
        using var application = new AcessosUnidadeWebApplicationFactory();
        using var client = CreateClient(application);

        using var response = await client.GetAsync(
            $"/franqueadora/unidades/{Guid.NewGuid()}/acessos");

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.StartsWith(
            "/login?",
            response.Headers.Location?.PathAndQuery,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Administrador_unidade_recebe_acesso_negado_no_get_e_post()
    {
        using var application = new AcessosUnidadeWebApplicationFactory();
        var unidadeId = Guid.NewGuid();
        application.Acessos.Adicionar(
            application.UsuarioStore.Usuario.Id,
            Guid.NewGuid(),
            unidadeId,
            PerfilAcesso.AdministradorUnidade);
        using var client = CreateClient(application);
        await LoginAsync(client, application);

        using var get = await client.GetAsync(
            $"/franqueadora/unidades/{unidadeId}/acessos");
        using var post = await client.PostAsync(
            $"/franqueadora/unidades/{unidadeId}/acessos/adicionar",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Email"] = "admin@bfa.test"
            }));

        Assert.Equal(HttpStatusCode.Found, get.StatusCode);
        Assert.StartsWith(
            "/acesso-negado?",
            get.Headers.Location?.PathAndQuery,
            StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Found, post.StatusCode);
        Assert.StartsWith(
            "/acesso-negado?",
            post.Headers.Location?.PathAndQuery,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Administrador_rede_lista_apenas_administradores_da_unidade_e_organizacao()
    {
        using var application = new AcessosUnidadeWebApplicationFactory();
        var organizacaoId = ConfigurarAdministradorRede(application);
        var unidade = await AdicionarUnidadeAsync(application, organizacaoId, "BFA Tietê");
        var outraUnidade = await AdicionarUnidadeAsync(application, organizacaoId, "BFA Sorocaba");
        var outraOrganizacaoId = Guid.NewGuid();
        var unidadeExterna = await AdicionarUnidadeAsync(
            application,
            outraOrganizacaoId,
            "Externa");
        var adminZeta = await AdicionarUsuarioAsync(application, "zeta@bfa.test");
        var adminAlfa = await AdicionarUsuarioAsync(application, "alfa@bfa.test");
        var outroAdmin = await AdicionarUsuarioAsync(application, "outra-unidade@bfa.test");
        var externo = await AdicionarUsuarioAsync(application, "externo@bfa.test");
        var professor = await AdicionarUsuarioAsync(application, "professor@bfa.test");
        var aluno = await AdicionarUsuarioAsync(application, "aluno@bfa.test");
        await AdicionarVinculoAsync(
            application,
            adminZeta.Id,
            organizacaoId,
            unidade.Id,
            PerfilAcesso.AdministradorUnidade);
        await AdicionarVinculoAsync(
            application,
            adminAlfa.Id,
            organizacaoId,
            unidade.Id,
            PerfilAcesso.AdministradorUnidade,
            ativo: false);
        await AdicionarVinculoAsync(
            application,
            outroAdmin.Id,
            organizacaoId,
            outraUnidade.Id,
            PerfilAcesso.AdministradorUnidade);
        await AdicionarVinculoAsync(
            application,
            externo.Id,
            outraOrganizacaoId,
            unidadeExterna.Id,
            PerfilAcesso.AdministradorUnidade);
        await AdicionarVinculoAsync(
            application,
            professor.Id,
            organizacaoId,
            unidade.Id,
            PerfilAcesso.Professor);
        await AdicionarVinculoAsync(
            application,
            aluno.Id,
            organizacaoId,
            unidade.Id,
            PerfilAcesso.Aluno);
        using var client = CreateClient(application);
        await LoginAsync(client, application);

        var html = WebUtility.HtmlDecode(await client.GetStringAsync(
            $"/franqueadora/unidades/{unidade.Id}/acessos"));

        Assert.Contains("Acessos administrativos", html, StringComparison.Ordinal);
        Assert.Contains("BFA Tietê", html, StringComparison.Ordinal);
        Assert.Contains("zeta@bfa.test", html, StringComparison.Ordinal);
        Assert.Contains("alfa@bfa.test", html, StringComparison.Ordinal);
        Assert.True(
            html.IndexOf("alfa@bfa.test", StringComparison.Ordinal)
            < html.IndexOf("zeta@bfa.test", StringComparison.Ordinal));
        Assert.DoesNotContain("outra-unidade@bfa.test", html, StringComparison.Ordinal);
        Assert.DoesNotContain("externo@bfa.test", html, StringComparison.Ordinal);
        Assert.DoesNotContain("professor@bfa.test", html, StringComparison.Ordinal);
        Assert.DoesNotContain("aluno@bfa.test", html, StringComparison.Ordinal);
        Assert.Contains("bfa-admin-table-container", html, StringComparison.Ordinal);
        Assert.Contains("bfa-admin-desktop-list", html, StringComparison.Ordinal);
        Assert.Contains("bfa-admin-mobile-list", html, StringComparison.Ordinal);
        Assert.Contains("bfa-acesso-card", html, StringComparison.Ordinal);
        Assert.Contains("bfa-admin-badge is-active", html, StringComparison.Ordinal);
        Assert.Contains("bfa-admin-badge is-inactive", html, StringComparison.Ordinal);
        Assert.Contains("Adicionar administrador", html, StringComparison.Ordinal);
        Assert.Contains("name=\"Email\"", html, StringComparison.Ordinal);
        Assert.Contains("type=\"email\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"__RequestVerificationToken\"", html, StringComparison.Ordinal);
        Assert.Contains("/lib/jquery/dist/jquery.min.js", html, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Desativar acesso de zeta@bfa.test\"", html, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Ativar acesso de alfa@bfa.test\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"OrganizacaoId\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"UsuarioId\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"Perfil\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Excluir", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Url_de_unidade_de_outra_organizacao_retorna_nao_encontrada()
    {
        using var application = new AcessosUnidadeWebApplicationFactory();
        ConfigurarAdministradorRede(application);
        var unidadeExterna = await AdicionarUnidadeAsync(
            application,
            Guid.NewGuid(),
            "Externa");
        using var client = CreateClient(application);
        await LoginAsync(client, application);

        using var response = await client.GetAsync(
            $"/franqueadora/unidades/{unidadeExterna.Id}/acessos");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Post_adicionar_exige_antiforgery_e_usa_contexto_seguro_da_rota()
    {
        using var application = new AcessosUnidadeWebApplicationFactory();
        var organizacaoId = ConfigurarAdministradorRede(application);
        var unidade = await AdicionarUnidadeAsync(application, organizacaoId, "BFA Tietê");
        await AdicionarUsuarioAsync(application, application.UsuarioStore.Usuario);
        using var client = CreateClient(application);
        await LoginAsync(client, application);
        var url = $"/franqueadora/unidades/{unidade.Id}/acessos";

        using var semToken = await client.PostAsync(
            $"{url}/adicionar",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Email"] = application.UsuarioStore.Email
            }));
        Assert.Equal(HttpStatusCode.BadRequest, semToken.StatusCode);

        var token = await GetAntiforgeryTokenAsync(client, url);
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = application.UsuarioStore.Email.ToUpperInvariant(),
            ["OrganizacaoId"] = Guid.NewGuid().ToString(),
            ["UsuarioId"] = Guid.NewGuid().ToString(),
            ["Perfil"] = PerfilAcesso.Professor.ToString(),
            ["__RequestVerificationToken"] = token
        });

        using var response = await client.PostAsync($"{url}/adicionar", form);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal(url, response.Headers.Location?.OriginalString);
        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        var vinculo = await dbContext.VinculosAcesso.AsNoTracking().SingleAsync();
        Assert.Equal(application.UsuarioStore.Usuario.Id, vinculo.UsuarioId);
        Assert.Equal(organizacaoId, vinculo.OrganizacaoId);
        Assert.Equal(unidade.Id, vinculo.UnidadeId);
        Assert.Equal(PerfilAcesso.AdministradorUnidade, vinculo.Perfil);
        Assert.True(vinculo.Ativo);
    }

    [Fact]
    public async Task Usuario_inexistente_exibe_mensagem_controlada()
    {
        using var application = new AcessosUnidadeWebApplicationFactory();
        var organizacaoId = ConfigurarAdministradorRede(application);
        var unidade = await AdicionarUnidadeAsync(application, organizacaoId, "BFA Tietê");
        using var client = CreateClient(application);
        await LoginAsync(client, application);
        var url = $"/franqueadora/unidades/{unidade.Id}/acessos";
        var token = await GetAntiforgeryTokenAsync(client, url);
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = "inexistente@bfa.test",
            ["__RequestVerificationToken"] = token
        });

        using var response = await client.PostAsync($"{url}/adicionar", form);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(
            "Não encontramos um usuário cadastrado com este email.",
            html,
            StringComparison.Ordinal);
        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        Assert.Empty(await dbContext.VinculosAcesso.AsNoTracking().ToArrayAsync());
    }

    [Fact]
    public async Task Vinculo_ativo_duplicado_exibe_mensagem_e_nao_cria_outro()
    {
        using var application = new AcessosUnidadeWebApplicationFactory();
        var organizacaoId = ConfigurarAdministradorRede(application);
        var unidade = await AdicionarUnidadeAsync(application, organizacaoId, "BFA Tietê");
        await AdicionarUsuarioAsync(application, application.UsuarioStore.Usuario);
        var existente = await AdicionarVinculoAsync(
            application,
            application.UsuarioStore.Usuario.Id,
            organizacaoId,
            unidade.Id,
            PerfilAcesso.AdministradorUnidade);
        using var client = CreateClient(application);
        await LoginAsync(client, application);
        var url = $"/franqueadora/unidades/{unidade.Id}/acessos";
        var token = await GetAntiforgeryTokenAsync(client, url);
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = application.UsuarioStore.Email,
            ["__RequestVerificationToken"] = token
        });

        using var response = await client.PostAsync($"{url}/adicionar", form);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Este usuário já administra esta unidade.", html, StringComparison.Ordinal);
        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        var vinculo = Assert.Single(await dbContext.VinculosAcesso.AsNoTracking().ToArrayAsync());
        Assert.Equal(existente.Id, vinculo.Id);
    }

    [Fact]
    public async Task Vinculo_inativo_equivalente_e_reativado()
    {
        using var application = new AcessosUnidadeWebApplicationFactory();
        var organizacaoId = ConfigurarAdministradorRede(application);
        var unidade = await AdicionarUnidadeAsync(application, organizacaoId, "BFA Tietê");
        await AdicionarUsuarioAsync(application, application.UsuarioStore.Usuario);
        var existente = await AdicionarVinculoAsync(
            application,
            application.UsuarioStore.Usuario.Id,
            organizacaoId,
            unidade.Id,
            PerfilAcesso.AdministradorUnidade,
            ativo: false);
        using var client = CreateClient(application);
        await LoginAsync(client, application);
        var url = $"/franqueadora/unidades/{unidade.Id}/acessos";
        var token = await GetAntiforgeryTokenAsync(client, url);
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = application.UsuarioStore.Email,
            ["__RequestVerificationToken"] = token
        });

        using var response = await client.PostAsync($"{url}/adicionar", form);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        var vinculo = Assert.Single(await dbContext.VinculosAcesso.AsNoTracking().ToArrayAsync());
        Assert.Equal(existente.Id, vinculo.Id);
        Assert.True(vinculo.Ativo);
        Assert.True(vinculo.AtualizadoEmUtc > vinculo.CriadoEmUtc);
    }

    [Fact]
    public async Task Ativar_e_desativar_exige_antiforgery_e_nao_exclui_vinculo()
    {
        using var application = new AcessosUnidadeWebApplicationFactory();
        var organizacaoId = ConfigurarAdministradorRede(application);
        var unidade = await AdicionarUnidadeAsync(application, organizacaoId, "BFA Tietê");
        var usuario = await AdicionarUsuarioAsync(application, "admin@bfa.test");
        var vinculo = await AdicionarVinculoAsync(
            application,
            usuario.Id,
            organizacaoId,
            unidade.Id,
            PerfilAcesso.AdministradorUnidade);
        using var client = CreateClient(application);
        await LoginAsync(client, application);
        var url = $"/franqueadora/unidades/{unidade.Id}/acessos";

        using var semToken = await client.PostAsync(
            $"{url}/{vinculo.Id}/desativar",
            new FormUrlEncodedContent([]));
        Assert.Equal(HttpStatusCode.BadRequest, semToken.StatusCode);

        var tokenDesativar = await GetAntiforgeryTokenAsync(client, url);
        using var desativar = await client.PostAsync(
            $"{url}/{vinculo.Id}/desativar",
            FormComToken(tokenDesativar));
        Assert.Equal(HttpStatusCode.Found, desativar.StatusCode);
        var desativado = await ObterVinculoAsync(application, vinculo.Id);
        Assert.False(desativado.Ativo);

        var tokenAtivar = await GetAntiforgeryTokenAsync(client, url);
        using var ativar = await client.PostAsync(
            $"{url}/{vinculo.Id}/ativar",
            FormComToken(tokenAtivar));
        Assert.Equal(HttpStatusCode.Found, ativar.StatusCode);
        var reativado = await ObterVinculoAsync(application, vinculo.Id);
        Assert.True(reativado.Ativo);

        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        Assert.Equal(1, await dbContext.VinculosAcesso.CountAsync());
    }

    [Fact]
    public async Task Post_de_outro_tenant_nao_altera_vinculo()
    {
        using var application = new AcessosUnidadeWebApplicationFactory();
        var organizacaoId = ConfigurarAdministradorRede(application);
        var unidadePropria = await AdicionarUnidadeAsync(
            application,
            organizacaoId,
            "BFA Tietê");
        var outraOrganizacaoId = Guid.NewGuid();
        var unidadeExterna = await AdicionarUnidadeAsync(
            application,
            outraOrganizacaoId,
            "Externa");
        var usuarioExterno = await AdicionarUsuarioAsync(application, "externo@bfa.test");
        var vinculoExterno = await AdicionarVinculoAsync(
            application,
            usuarioExterno.Id,
            outraOrganizacaoId,
            unidadeExterna.Id,
            PerfilAcesso.AdministradorUnidade);
        using var client = CreateClient(application);
        await LoginAsync(client, application);
        var token = await GetAntiforgeryTokenAsync(
            client,
            $"/franqueadora/unidades/{unidadePropria.Id}/acessos");

        using var response = await client.PostAsync(
            $"/franqueadora/unidades/{unidadeExterna.Id}/acessos/{vinculoExterno.Id}/desativar",
            FormComToken(token));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.True((await ObterVinculoAsync(application, vinculoExterno.Id)).Ativo);
    }

    private static Guid ConfigurarAdministradorRede(
        AcessosUnidadeWebApplicationFactory application)
    {
        var organizacaoId = Guid.NewGuid();
        application.Acessos.Adicionar(
            application.UsuarioStore.Usuario.Id,
            organizacaoId,
            unidadeId: null,
            PerfilAcesso.AdministradorRede);
        return organizacaoId;
    }

    private static HttpClient CreateClient(
        AcessosUnidadeWebApplicationFactory application)
    {
        return application.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
    }

    private static async Task LoginAsync(
        HttpClient client,
        AcessosUnidadeWebApplicationFactory application)
    {
        var token = await GetAntiforgeryTokenAsync(client, "/login");
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = application.UsuarioStore.Email,
            ["Senha"] = application.UsuarioStore.Senha,
            ["LembrarMe"] = "false",
            ["ReturnUrl"] = string.Empty,
            ["__RequestVerificationToken"] = token
        });

        using var response = await client.PostAsync("/login", form);
        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
    }

    private static async Task<string> GetAntiforgeryTokenAsync(
        HttpClient client,
        string requestUri)
    {
        var html = await client.GetStringAsync(requestUri);
        var match = AntiforgeryToken().Match(html);
        Assert.True(match.Success, "Token antiforgery não encontrado.");
        return WebUtility.HtmlDecode(match.Groups["token"].Value);
    }

    private static FormUrlEncodedContent FormComToken(string token)
    {
        return new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token
        });
    }

    private static async Task<Unidade> AdicionarUnidadeAsync(
        AcessosUnidadeWebApplicationFactory application,
        Guid organizacaoId,
        string nome)
    {
        var unidade = new Unidade(
            Guid.NewGuid(),
            organizacaoId,
            nome,
            $"unidade-{Guid.NewGuid():N}",
            DateTime.UtcNow.AddDays(-2));
        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        dbContext.Unidades.Add(unidade);
        await dbContext.SaveChangesAsync();
        return unidade;
    }

    private static Task<UsuarioIdentity> AdicionarUsuarioAsync(
        AcessosUnidadeWebApplicationFactory application,
        string email)
    {
        return AdicionarUsuarioAsync(application, new UsuarioIdentity
        {
            Id = Guid.NewGuid(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            SecurityStamp = Guid.NewGuid().ToString(),
            ConcurrencyStamp = Guid.NewGuid().ToString()
        });
    }

    private static async Task<UsuarioIdentity> AdicionarUsuarioAsync(
        AcessosUnidadeWebApplicationFactory application,
        UsuarioIdentity usuario)
    {
        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();

        if (!await dbContext.Users.AnyAsync(item => item.Id == usuario.Id))
        {
            dbContext.Users.Add(usuario);
            await dbContext.SaveChangesAsync();
        }

        return usuario;
    }

    private static async Task<VinculoAcesso> AdicionarVinculoAsync(
        AcessosUnidadeWebApplicationFactory application,
        Guid usuarioId,
        Guid organizacaoId,
        Guid unidadeId,
        PerfilAcesso perfil,
        bool ativo = true)
    {
        var vinculo = new VinculoAcesso(
            Guid.NewGuid(),
            usuarioId,
            organizacaoId,
            unidadeId,
            perfil,
            DateTime.UtcNow.AddDays(-1));

        if (!ativo)
        {
            vinculo.Desativar(DateTime.UtcNow.AddHours(-1));
        }

        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        dbContext.VinculosAcesso.Add(vinculo);
        await dbContext.SaveChangesAsync();
        return vinculo;
    }

    private static async Task<VinculoAcesso> ObterVinculoAsync(
        AcessosUnidadeWebApplicationFactory application,
        Guid vinculoId)
    {
        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        return await dbContext.VinculosAcesso
            .AsNoTracking()
            .SingleAsync(vinculo => vinculo.Id == vinculoId);
    }

    [GeneratedRegex(
        "name=\"__RequestVerificationToken\"[^>]*value=\"(?<token>[^\"]+)\"",
        RegexOptions.CultureInvariant)]
    private static partial Regex AntiforgeryToken();
}
