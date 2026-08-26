using System.Net;
using System.Text.RegularExpressions;
using BFA.Domain.Acessos;
using BFA.Domain.Franqueados;
using BFA.Domain.Unidades;
using BFA.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BFA.IntegrationTests;

public sealed partial class UnidadesFranqueadoraEndpointTests
{
    [Fact]
    public async Task Get_lista_anonimo_redireciona_para_login()
    {
        using var application = new UnidadesFranqueadoraWebApplicationFactory();
        using var client = CreateClient(application);

        using var response = await client.GetAsync("/franqueadora/unidades");

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.StartsWith(
            "/login?",
            response.Headers.Location?.PathAndQuery,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Administrador_unidade_recebe_acesso_negado()
    {
        using var application = new UnidadesFranqueadoraWebApplicationFactory();
        application.Acessos.Adicionar(
            application.UsuarioStore.Usuario.Id,
            Guid.NewGuid(),
            Guid.NewGuid(),
            PerfilAcesso.AdministradorUnidade);
        using var client = CreateClient(application);
        await LoginAsync(client, application);

        using var response = await client.GetAsync("/franqueadora/unidades");

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.StartsWith(
            "/acesso-negado?",
            response.Headers.Location?.PathAndQuery,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Administrador_rede_lista_somente_unidades_da_propria_organizacao()
    {
        using var application = new UnidadesFranqueadoraWebApplicationFactory();
        var organizacaoId = ConfigurarAdministradorRede(application);
        await AdicionarUnidadeAsync(application, organizacaoId, "BFA Tietê", "bfa-tiete");
        await AdicionarUnidadeAsync(
            application,
            organizacaoId,
            "BFA Inativa",
            "bfa-inativa",
            ativa: false);
        await AdicionarUnidadeAsync(application, Guid.NewGuid(), "Unidade Externa", "externa");
        using var client = CreateClient(application);
        await LoginAsync(client, application);

        var html = WebUtility.HtmlDecode(
            await client.GetStringAsync("/franqueadora/unidades"));

        Assert.Contains("BFA Tietê", html, StringComparison.Ordinal);
        Assert.Contains("BFA Inativa", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Unidade Externa", html, StringComparison.Ordinal);
        Assert.Contains("Nova unidade", html, StringComparison.Ordinal);
        Assert.Contains("bfa-admin-table-container", html, StringComparison.Ordinal);
        Assert.Contains("bfa-admin-desktop-list", html, StringComparison.Ordinal);
        Assert.Contains("bfa-admin-mobile-list", html, StringComparison.Ordinal);
        Assert.Contains("bfa-admin-badge is-active", html, StringComparison.Ordinal);
        Assert.Contains("bfa-admin-badge is-inactive", html, StringComparison.Ordinal);
        Assert.Contains("bfa-admin-actions", html, StringComparison.Ordinal);
        Assert.Contains("bfa-admin-icon-action", html, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Editar unidade BFA Tietê\"", html, StringComparison.Ordinal);
        Assert.Contains("title=\"Editar unidade BFA Tietê\"", html, StringComparison.Ordinal);
        Assert.Contains(
            "aria-label=\"Gerenciar acessos da unidade BFA Tietê\"",
            html,
            StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Desativar unidade BFA Tietê\"", html, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Ativar unidade BFA Inativa\"", html, StringComparison.Ordinal);
        Assert.Contains("<svg", html, StringComparison.Ordinal);
        Assert.DoesNotContain(">Editar<", html, StringComparison.Ordinal);
        Assert.DoesNotContain(">Ativar<", html, StringComparison.Ordinal);
        Assert.DoesNotContain(">Desativar<", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Excluir", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            $"href=\"/unidade/{(await ObterUnidadePorNomeAsync(application, "BFA Tietê")):D}\"",
            html,
            StringComparison.Ordinal);
        Assert.DoesNotContain("aria-label=\"Entrar na unidade BFA Inativa\"", html,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Administrador_rede_entra_na_unidade_quando_ela_possui_franqueado_ativo()
    {
        using var application = new UnidadesFranqueadoraWebApplicationFactory();
        var organizacaoId = ConfigurarAdministradorRede(application);
        var unidade = await AdicionarUnidadeAsync(
            application, organizacaoId, "BFA Franqueada", "bfa-franqueada");
        await AdicionarFranqueadoAtivoAsync(application, organizacaoId, unidade.Id);
        using var client = CreateClient(application);
        await LoginAsync(client, application);

        var html = WebUtility.HtmlDecode(
            await client.GetStringAsync("/franqueadora/unidades"));

        Assert.Contains("Franqueada", html, StringComparison.Ordinal);
        Assert.Contains($"href=\"/unidade/{unidade.Id:D}\"", html, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Entrar na unidade BFA Franqueada\"", html,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_nova_retorna_formulario_para_administrador_rede()
    {
        using var application = new UnidadesFranqueadoraWebApplicationFactory();
        ConfigurarAdministradorRede(application);
        using var client = CreateClient(application);
        await LoginAsync(client, application);

        var html = await client.GetStringAsync("/franqueadora/unidades/nova");

        Assert.Contains("action=\"/franqueadora/unidades/nova\"", html, StringComparison.Ordinal);
        Assert.Contains("bfa-admin-page-header", html, StringComparison.Ordinal);
        Assert.Contains("bfa-admin-form-card", html, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Salvar nova unidade\"", html, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Voltar para unidades\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"Nome\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"Slug\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"OrganizacaoId\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Post_nova_ignora_organizacao_enviada_e_usa_vinculo_atual()
    {
        using var application = new UnidadesFranqueadoraWebApplicationFactory();
        var organizacaoId = ConfigurarAdministradorRede(application);
        using var client = CreateClient(application);
        await LoginAsync(client, application);
        var token = await GetAntiforgeryTokenAsync(
            client,
            "/franqueadora/unidades/nova");
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Nome"] = "  BFA Sorocaba  ",
            ["Slug"] = "  BFA-SOROCABA  ",
            ["OrganizacaoId"] = Guid.NewGuid().ToString(),
            ["__RequestVerificationToken"] = token
        });

        using var response = await client.PostAsync("/franqueadora/unidades/nova", form);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal("/franqueadora/unidades", response.Headers.Location?.OriginalString);
        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        var unidade = await dbContext.Unidades.AsNoTracking().SingleAsync();
        Assert.Equal(organizacaoId, unidade.OrganizacaoId);
        Assert.Equal("BFA Sorocaba", unidade.Nome);
        Assert.Equal("bfa-sorocaba", unidade.Slug);
        Assert.True(unidade.Ativa);
    }

    [Fact]
    public async Task Post_nova_slug_duplicado_exibe_mensagem_amigavel()
    {
        using var application = new UnidadesFranqueadoraWebApplicationFactory();
        var organizacaoId = ConfigurarAdministradorRede(application);
        await AdicionarUnidadeAsync(application, organizacaoId, "Existente", "bfa-tiete");
        using var client = CreateClient(application);
        await LoginAsync(client, application);
        var token = await GetAntiforgeryTokenAsync(
            client,
            "/franqueadora/unidades/nova");
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Nome"] = "Duplicada",
            ["Slug"] = "BFA-TIETE",
            ["__RequestVerificationToken"] = token
        });

        using var response = await client.PostAsync("/franqueadora/unidades/nova", form);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(
            "Já existe uma unidade com este identificador.",
            html,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Edicao_de_unidade_de_outra_organizacao_retorna_nao_encontrada()
    {
        using var application = new UnidadesFranqueadoraWebApplicationFactory();
        ConfigurarAdministradorRede(application);
        var unidadeExterna = await AdicionarUnidadeAsync(
            application,
            Guid.NewGuid(),
            "Externa",
            "externa");
        using var client = CreateClient(application);
        await LoginAsync(client, application);

        using var response = await client.GetAsync(
            $"/franqueadora/unidades/{unidadeExterna.Id}/editar");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Post_editar_atualiza_unidade_da_propria_organizacao()
    {
        using var application = new UnidadesFranqueadoraWebApplicationFactory();
        var organizacaoId = ConfigurarAdministradorRede(application);
        var unidade = await AdicionarUnidadeAsync(
            application,
            organizacaoId,
            "Nome Antigo",
            "nome-antigo");
        using var client = CreateClient(application);
        await LoginAsync(client, application);
        var editarUrl = $"/franqueadora/unidades/{unidade.Id}/editar";
        var token = await GetAntiforgeryTokenAsync(client, editarUrl);
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Nome"] = "Nome Novo",
            ["Slug"] = "NOME-NOVO",
            ["__RequestVerificationToken"] = token
        });

        using var response = await client.PostAsync(editarUrl, form);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        var unidadeAtualizada = await dbContext.Unidades.AsNoTracking().SingleAsync();
        Assert.Equal("Nome Novo", unidadeAtualizada.Nome);
        Assert.Equal("nome-novo", unidadeAtualizada.Slug);
        Assert.True(unidadeAtualizada.AtualizadoEmUtc > unidadeAtualizada.CriadoEmUtc);
    }

    [Fact]
    public async Task Post_ativar_e_desativar_exige_antiforgery_e_altera_estado()
    {
        using var application = new UnidadesFranqueadoraWebApplicationFactory();
        var organizacaoId = ConfigurarAdministradorRede(application);
        var unidade = await AdicionarUnidadeAsync(
            application,
            organizacaoId,
            "BFA Tietê",
            "bfa-tiete");
        using var client = CreateClient(application);
        await LoginAsync(client, application);

        using var semToken = await client.PostAsync(
            $"/franqueadora/unidades/{unidade.Id}/desativar",
            new FormUrlEncodedContent([]));
        Assert.Equal(HttpStatusCode.BadRequest, semToken.StatusCode);

        var tokenDesativar = await GetAntiforgeryTokenAsync(
            client,
            "/franqueadora/unidades");
        using var desativarForm = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = tokenDesativar
        });
        using var desativar = await client.PostAsync(
            $"/franqueadora/unidades/{unidade.Id}/desativar",
            desativarForm);
        Assert.Equal(HttpStatusCode.Found, desativar.StatusCode);
        Assert.False(await ObterEstadoAsync(application, unidade.Id));

        var tokenAtivar = await GetAntiforgeryTokenAsync(
            client,
            "/franqueadora/unidades");
        using var ativarForm = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = tokenAtivar
        });
        using var ativar = await client.PostAsync(
            $"/franqueadora/unidades/{unidade.Id}/ativar",
            ativarForm);
        Assert.Equal(HttpStatusCode.Found, ativar.StatusCode);
        Assert.True(await ObterEstadoAsync(application, unidade.Id));
    }

    private static Guid ConfigurarAdministradorRede(
        UnidadesFranqueadoraWebApplicationFactory application)
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
        UnidadesFranqueadoraWebApplicationFactory application)
    {
        return application.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
    }

    private static async Task LoginAsync(
        HttpClient client,
        UnidadesFranqueadoraWebApplicationFactory application)
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

    private static async Task<Unidade> AdicionarUnidadeAsync(
        UnidadesFranqueadoraWebApplicationFactory application,
        Guid organizacaoId,
        string nome,
        string slug,
        bool ativa = true)
    {
        var unidade = new Unidade(
            Guid.NewGuid(),
            organizacaoId,
            nome,
            slug,
            DateTime.UtcNow);

        if (!ativa)
        {
            unidade.Desativar(DateTime.UtcNow);
        }

        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        dbContext.Unidades.Add(unidade);
        await dbContext.SaveChangesAsync();
        return unidade;
    }

    private static async Task<bool> ObterEstadoAsync(
        UnidadesFranqueadoraWebApplicationFactory application,
        Guid unidadeId)
    {
        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        return await dbContext.Unidades
            .AsNoTracking()
            .Where(unidade => unidade.Id == unidadeId)
            .Select(unidade => unidade.Ativa)
            .SingleAsync();
    }

    private static async Task<Guid> ObterUnidadePorNomeAsync(
        UnidadesFranqueadoraWebApplicationFactory application,
        string nome)
    {
        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        return await dbContext.Unidades.AsNoTracking()
            .Where(unidade => unidade.Nome == nome)
            .Select(unidade => unidade.Id)
            .SingleAsync();
    }

    private static async Task AdicionarFranqueadoAtivoAsync(
        UnidadesFranqueadoraWebApplicationFactory application,
        Guid organizacaoId,
        Guid unidadeId)
    {
        var criadoEmUtc = DateTime.UtcNow;
        var franqueado = new Franqueado(
            Guid.NewGuid(), organizacaoId, TipoPessoaFranqueado.PessoaJuridica,
            "Franqueado da unidade", $"{unidadeId:N}"[..12] + "99",
            $"franqueado-{unidadeId:N}@bfa.test", criadoEmUtc);
        var vinculo = new FranqueadoUnidade(
            Guid.NewGuid(), franqueado.Id, organizacaoId, unidadeId, criadoEmUtc);
        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        dbContext.Franqueados.Add(franqueado);
        dbContext.FranqueadosUnidades.Add(vinculo);
        await dbContext.SaveChangesAsync();
    }

    [GeneratedRegex(
        "name=\"__RequestVerificationToken\"[^>]*value=\"(?<token>[^\"]+)\"",
        RegexOptions.CultureInvariant)]
    private static partial Regex AntiforgeryToken();
}
