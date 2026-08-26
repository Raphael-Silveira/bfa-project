using System.Net;
using System.Text.RegularExpressions;
using BFA.Domain.Acessos;
using BFA.Application.Usuarios;
using BFA.Domain.Professores;
using BFA.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BFA.IntegrationTests;

public sealed partial class AreaProfessorEndpointTests : IClassFixture<AreaProfessorWebApplicationFactory>
{
    private readonly AreaProfessorWebApplicationFactory _application;

    public AreaProfessorEndpointTests(AreaProfessorWebApplicationFactory application)
    {
        _application = application;
    }

    [Fact]
    public async Task Professor_com_uma_unidade_entra_direto_e_ve_landing_inicial()
    {
        var (organizacaoId, unidadeId, _, _) = await ConfigurarUnidadeProfessorAsync(
            "BFA Cerquilho");
        using var client = CriarClient();
        using var login = await AutenticarAsync(client);

        Assert.Equal($"/professor/unidade/{unidadeId:D}",
            login.Headers.Location?.OriginalString);
        using var pagina = await client.GetAsync($"/professor/unidade/{unidadeId:D}");
        var html = WebUtility.HtmlDecode(await pagina.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, pagina.StatusCode);
        Assert.Contains("Área do Professor", html, StringComparison.Ordinal);
        Assert.Contains("BFA Cerquilho", html, StringComparison.Ordinal);
        Assert.Contains("Minha área", html, StringComparison.Ordinal);
        Assert.Contains("class=\"bfa-admin-nav-link is-active\"", html,
            StringComparison.Ordinal);
        Assert.Contains("<span>Visão Geral</span>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("bfa-admin-nav__link", html, StringComparison.Ordinal);
        Assert.DoesNotContain(">Contrato<", html, StringComparison.Ordinal);
        Assert.DoesNotContain(">Professores<", html, StringComparison.Ordinal);
        Assert.Contains("Minhas turmas", html, StringComparison.Ordinal);
        Assert.Contains("0 turmas ativas", html, StringComparison.Ordinal);
        Assert.Contains($"/professor/unidade/{unidadeId:D}/turmas", html,
            StringComparison.Ordinal);
        Assert.NotEqual(Guid.Empty, organizacaoId);
    }

    [Fact]
    public async Task Professor_com_multiplas_unidades_usa_selecao_propria()
    {
        await ConfigurarUnidadeProfessorAsync("BFA A");
        await ConfigurarUnidadeProfessorAsync("BFA B", limpar: false);
        using var client = CriarClient();
        using var login = await AutenticarAsync(client);

        Assert.Equal("/professor/selecionar-unidade",
            login.Headers.Location?.OriginalString);
        var html = await client.GetStringAsync("/professor/selecionar-unidade");
        Assert.Contains("BFA A", html, StringComparison.Ordinal);
        Assert.Contains("BFA B", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Professor_nao_acessa_unidade_fora_dos_vinculos_ativos()
    {
        await ConfigurarUnidadeProfessorAsync("BFA Autorizada");
        using var client = CriarClient();
        using var login = await AutenticarAsync(client);

        using var response = await client.GetAsync(
            $"/professor/unidade/{Guid.NewGuid():D}");

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Contains("/acesso-negado", response.Headers.Location?.OriginalString,
            StringComparison.Ordinal);
    }

    private async Task<(Guid OrganizacaoId, Guid UnidadeId, Professor Professor,
        ProfessorUnidade ProfessorUnidade)> ConfigurarUnidadeProfessorAsync(
        string nome,
        bool limpar = true,
        bool acessoAtivo = true,
        bool vinculoProfissionalAtivo = true)
    {
        if (limpar)
        {
            _application.AcessosLogin.Limpar();
            _application.UnidadesLogin.Limpar();
        }

        var organizacaoId = Guid.NewGuid();
        var unidadeId = Guid.NewGuid();
        var usuarioId = _application.UsuarioStore.Usuario.Id;
        _application.AcessosLogin.Adicionar(
            usuarioId, organizacaoId, unidadeId, PerfilAcesso.Professor,
            ativo: acessoAtivo);
        _application.UnidadesLogin.AdicionarProfessor(
            usuarioId, organizacaoId, unidadeId, nome, ativa: acessoAtivo);

        var agora = new DateTime(2026, 8, 25, 12, 0, 0, DateTimeKind.Utc);
        var professor = new Professor(
            Guid.NewGuid(), organizacaoId, $"Professor {nome}", agora,
            usuarioId: usuarioId);
        var professorUnidade = new ProfessorUnidade(
            Guid.NewGuid(), organizacaoId, professor.Id, unidadeId, agora);
        if (!vinculoProfissionalAtivo)
        {
            professorUnidade.Desativar(agora.AddMinutes(1));
        }

        await using var scope = _application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        dbContext.Professores.Add(professor);
        dbContext.ProfessoresUnidades.Add(professorUnidade);
        await dbContext.SaveChangesAsync();

        return (organizacaoId, unidadeId, professor, professorUnidade);
    }

    private HttpClient CriarClient() => _application.CreateClient(
        new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

    private async Task<HttpResponseMessage> AutenticarAsync(HttpClient client)
    {
        var html = await client.GetStringAsync("/login");
        var token = WebUtility.HtmlDecode(
            AntiforgeryToken().Match(html).Groups["token"].Value);
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = _application.UsuarioStore.Email,
            ["Senha"] = _application.UsuarioStore.Senha,
            ["LembrarMe"] = "false",
            ["ReturnUrl"] = string.Empty,
            ["__RequestVerificationToken"] = token
        });
        return await client.PostAsync("/login", form);
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\"[^>]*value=\"(?<token>[^\"]+)\"")]
    private static partial Regex AntiforgeryToken();
}

public sealed class AreaProfessorWebApplicationFactory : LoginWebApplicationFactory
{
    private readonly string _databaseName = $"bfa-area-professor-{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IDbContextOptionsConfiguration<BfaDbContext>>();
            services.RemoveAll<DbContextOptions<BfaDbContext>>();
            services.RemoveAll<BfaDbContext>();
            services.AddDbContext<BfaDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName)
                    .ConfigureWarnings(warnings => warnings.Ignore(
                        InMemoryEventId.TransactionIgnoredWarning)));

            services.RemoveAll<IUsuarioApresentacaoConsulta>();
            services.AddSingleton<IUsuarioApresentacaoConsulta,
                TestUsuarioApresentacaoConsulta>();
        });
    }
}
