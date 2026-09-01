using System.Net;
using System.Text.RegularExpressions;
using BFA.Application.Planos;
using BFA.Application.Unidades;
using BFA.Domain.Acessos;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BFA.IntegrationTests;

public sealed partial class PlanosEndpointTests
{
    [Fact]
    public async Task AdministradorRede_lista_planos_e_menu_da_rede()
    {
        using var application = new PlanosWebApplicationFactory();
        AutorizarRede(application);
        using var client = CriarClient(application);
        await LoginAsync(client, application);

        using var response = await client.GetAsync("/franqueadora/planos");
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Planos da Rede", html, StringComparison.Ordinal);
        Assert.Contains("Plano BFA 3x", html, StringComparison.Ordinal);
        Assert.Contains("R$ 280,00", html, StringComparison.Ordinal);
        Assert.Contains("/franqueadora/planos/novo", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Criacao_da_rede_aceita_valores_pt_br_e_antiforgery()
    {
        using var application = new PlanosWebApplicationFactory();
        AutorizarRede(application);
        using var client = CriarClient(application);
        await LoginAsync(client, application);
        var token = await ObterTokenAsync(client, "/franqueadora/planos/novo");

        using var response = await client.PostAsync(
            "/franqueadora/planos/novo",
            Formulario(token, "280,50", cobraMatricula: true, "100,25"));

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal(280.50m, application.Planos.UltimaCriacao!.Termos.ValorMensal);
        Assert.Equal(100.25m, application.Planos.UltimaCriacao.Termos.ValorMatricula);
        Assert.Equal("/franqueadora/planos/" + application.Planos.PlanoId,
            response.Headers.Location!.OriginalString);

        using var semToken = await client.PostAsync(
            "/franqueadora/planos/novo",
            Formulario(string.Empty, "280,50", false, null, incluirToken: false));
        Assert.Equal(HttpStatusCode.BadRequest, semToken.StatusCode);
    }

    [Fact]
    public async Task Nova_versao_da_rede_e_enviada_pelo_formulario_renderizado()
    {
        using var application = new PlanosWebApplicationFactory();
        AutorizarRede(application);
        using var client = CriarClient(application);
        await LoginAsync(client, application);
        var url = $"/franqueadora/planos/{application.Planos.PlanoId:D}/nova-versao";

        using var getResponse = await client.GetAsync(url);
        var html = WebUtility.HtmlDecode(await getResponse.Content.ReadAsStringAsync());
        var form = FormularioNovaVersao().Match(html);

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.True(form.Success, "Formulário POST de nova versão não encontrado no HTML.");
        Assert.Equal(url, form.Groups["action"].Value);
        Assert.Contains("method=\"post\"", form.Value, StringComparison.Ordinal);
        Assert.Contains("name=\"__RequestVerificationToken\"", form.Value,
            StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"Nome\"", form.Value, StringComparison.Ordinal);
        Assert.Contains("<button class=\"bfa-btn-primary bfa-admin-button\" type=\"submit\">Criar nova versão</button>",
            form.Value, StringComparison.Ordinal);
        Assert.Contains("<option value=\"7\">7× por semana</option>",
            form.Value, StringComparison.Ordinal);

        var token = WebUtility.HtmlDecode(
            AntiforgeryToken().Match(form.Value).Groups["token"].Value);
        using var postResponse = await client.PostAsync(
            form.Groups["action"].Value,
            Formulario(token, "290,00", cobraMatricula: true, "200,00",
                vigenciaInicio: "09/09/2026", duracaoMeses: "1",
                incluirNome: false));

        Assert.Equal(HttpStatusCode.Found, postResponse.StatusCode);
        Assert.Equal($"/franqueadora/planos/{application.Planos.PlanoId:D}",
            postResponse.Headers.Location!.OriginalString);
        Assert.NotNull(application.Planos.UltimaNovaVersao);
        Assert.Equal(1, application.Planos.UltimaNovaVersao.DuracaoMeses);
        Assert.Equal(7, application.Planos.UltimaNovaVersao.FrequenciaSemanal);
        Assert.Equal(290.00m, application.Planos.UltimaNovaVersao.ValorMensal);
        Assert.Equal(200.00m, application.Planos.UltimaNovaVersao.ValorMatricula);
        Assert.Equal(new DateOnly(2026, 9, 9),
            application.Planos.UltimaNovaVersao.VigenciaInicio);

        using var detailsResponse = await client.GetAsync(
            postResponse.Headers.Location.OriginalString);
        var detailsHtml = WebUtility.HtmlDecode(
            await detailsResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);
        Assert.Contains("Nova versão comercial criada com sucesso.", detailsHtml,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Nova_versao_da_rede_exibe_erros_de_modelstate()
    {
        using var application = new PlanosWebApplicationFactory();
        AutorizarRede(application);
        using var client = CriarClient(application);
        await LoginAsync(client, application);
        var url = $"/franqueadora/planos/{application.Planos.PlanoId:D}/nova-versao";
        var token = await ObterTokenAsync(client, url);

        using var response = await client.PostAsync(
            url,
            Formulario(token, "290,00", cobraMatricula: true, "200,00",
                vigenciaInicio: "09/09/2026", duracaoMeses: "1",
                incluirNome: false, frequenciaSemanal: "8"));
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Selecione uma frequência entre 1 e 7 vezes por semana.", html,
            StringComparison.Ordinal);
        Assert.Null(application.Planos.UltimaNovaVersao);
    }

    [Fact]
    public async Task Nova_versao_da_rede_exibe_rejeicao_da_application()
    {
        using var application = new PlanosWebApplicationFactory();
        application.Planos.EstadoNovaVersaoRede = EstadoPlanos.VigenciaInvalida;
        AutorizarRede(application);
        using var client = CriarClient(application);
        await LoginAsync(client, application);
        var url = $"/franqueadora/planos/{application.Planos.PlanoId:D}/nova-versao";
        var token = await ObterTokenAsync(client, url);

        using var response = await client.PostAsync(
            url,
            Formulario(token, "290,00", cobraMatricula: true, "200,00",
                vigenciaInicio: "09/09/2026", duracaoMeses: "1",
                incluirNome: false));
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("A nova vigência deve iniciar depois do início da versão atual.",
            html, StringComparison.Ordinal);
        Assert.NotNull(application.Planos.UltimaNovaVersao);
    }

    [Fact]
    public async Task Nova_versao_da_rede_exige_antiforgery()
    {
        using var application = new PlanosWebApplicationFactory();
        AutorizarRede(application);
        using var client = CriarClient(application);
        await LoginAsync(client, application);
        var url = $"/franqueadora/planos/{application.Planos.PlanoId:D}/nova-versao";

        using var response = await client.PostAsync(
            url,
            Formulario(string.Empty, "290,00", cobraMatricula: true, "200,00",
                incluirToken: false, vigenciaInicio: "09/09/2026",
                duracaoMeses: "1", incluirNome: false));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(application.Planos.UltimaNovaVersao);
    }

    [Fact]
    public async Task Rede_na_unidade_franqueada_visualiza_sem_acoes_de_gestao()
    {
        using var application = new PlanosWebApplicationFactory();
        application.Planos.PodeGerenciarLocal = false;
        application.Planos.PossuiFranqueadoAtivo = true;
        application.Governanca.Valor = new(true, false, true);
        using var client = CriarClient(application);
        await LoginAsync(client, application);

        using var response = await client.GetAsync(
            $"/unidade/{application.UnidadeId}/planos");
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("disponíveis para consulta", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain($"/unidade/{application.UnidadeId}/planos/novo", html,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Post_direto_local_e_bloqueado_quando_governanca_e_somente_leitura()
    {
        using var application = new PlanosWebApplicationFactory();
        application.Planos.PodeGerenciarLocal = false;
        application.Planos.PossuiFranqueadoAtivo = true;
        using var client = CriarClient(application);
        await LoginAsync(client, application);
        var url = $"/unidade/{application.UnidadeId}/planos/novo";
        var token = await ObterTokenAsync(client, $"/unidade/{application.UnidadeId}/planos");

        using var response = await client.PostAsync(
            url, Formulario(token, "200,00", false, null));

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.StartsWith("/acesso-negado", response.Headers.Location!.PathAndQuery,
            StringComparison.Ordinal);
        Assert.Null(application.Planos.UltimaCriacaoLocal);
    }

    [Fact]
    public async Task AdministradorUnidade_cria_plano_local_da_unidade_autorizada()
    {
        using var application = new PlanosWebApplicationFactory();
        application.Planos.PodeGerenciarLocal = true;
        application.Planos.PossuiFranqueadoAtivo = true;
        application.Governanca.Valor = new(false, true, true);
        using var client = CriarClient(application);
        await LoginAsync(client, application);
        var url = $"/unidade/{application.UnidadeId}/planos/novo";
        var token = await ObterTokenAsync(client, url);

        using var response = await client.PostAsync(
            url, Formulario(token, "199,90", false, null));

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal(application.UnidadeId, application.Planos.UltimaUnidadeId);
        Assert.Null(application.Planos.UltimaCriacaoLocal!.Termos.ValorMatricula);
    }

    [Fact]
    public async Task Professor_e_usuario_de_outra_unidade_nao_acessam_planos_locais()
    {
        using var application = new PlanosWebApplicationFactory();
        application.Planos.PermitirLeituraLocal = false;
        application.Governanca.Valor = new(false, false, false);
        using var client = CriarClient(application);
        await LoginAsync(client, application);

        using var response = await client.GetAsync(
            $"/unidade/{application.UnidadeId}/planos");

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.StartsWith("/acesso-negado", response.Headers.Location!.PathAndQuery,
            StringComparison.Ordinal);
    }

    private static void AutorizarRede(PlanosWebApplicationFactory application) =>
        application.Acessos.Adicionar(
            application.UsuarioStore.Usuario.Id,
            application.OrganizacaoId,
            null,
            PerfilAcesso.AdministradorRede);

    private static HttpClient CriarClient(PlanosWebApplicationFactory application) =>
        application.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

    private static async Task LoginAsync(
        HttpClient client, PlanosWebApplicationFactory application)
    {
        var token = await ObterTokenAsync(client, "/login");
        using var response = await client.PostAsync("/login", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["Email"] = application.UsuarioStore.Email,
                ["Senha"] = application.UsuarioStore.Senha,
                ["LembrarMe"] = "false",
                ["ReturnUrl"] = string.Empty,
                ["__RequestVerificationToken"] = token
            }));
        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
    }

    private static async Task<string> ObterTokenAsync(HttpClient client, string url)
    {
        var html = await client.GetStringAsync(url);
        var match = AntiforgeryToken().Match(html);
        Assert.True(match.Success, $"Token antiforgery não encontrado em {url}.");
        return WebUtility.HtmlDecode(match.Groups["token"].Value);
    }

    private static FormUrlEncodedContent Formulario(
        string token, string valorMensal, bool cobraMatricula,
        string? valorMatricula, bool incluirToken = true,
        string vigenciaInicio = "01/09/2026", string duracaoMeses = "9",
        bool incluirNome = true, string frequenciaSemanal = "7")
    {
        var values = new Dictionary<string, string>
        {
            ["DuracaoMeses"] = duracaoMeses,
            ["FrequenciaSemanal"] = frequenciaSemanal,
            ["ValorMensal"] = valorMensal,
            ["CobraMatricula"] = cobraMatricula.ToString().ToLowerInvariant(),
            ["ValorMatricula"] = valorMatricula ?? string.Empty,
            ["VigenciaInicioTexto"] = vigenciaInicio
        };
        if (incluirNome) values["Nome"] = "Plano Teste";
        if (incluirToken) values["__RequestVerificationToken"] = token;
        return new(values);
    }

    [GeneratedRegex(
        "name=\"__RequestVerificationToken\"[^>]*value=\"(?<token>[^\"]+)\"",
        RegexOptions.CultureInvariant)]
    private static partial Regex AntiforgeryToken();

    [GeneratedRegex(
        "<form\\b(?=[^>]*data-bfa-plan-form)[^>]*action=\\\"(?<action>[^\\\"]+)\\\"[^>]*method=\\\"post\\\"[^>]*>.*?</form>",
        RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex FormularioNovaVersao();
}

public sealed class PlanosWebApplicationFactory : FranqueadoraWebApplicationFactory
{
    public Guid OrganizacaoId { get; } = Guid.NewGuid();
    public Guid UnidadeId { get; } = Guid.NewGuid();
    public TestPlanosServico Planos => Services.GetRequiredService<TestPlanosServico>();
    public TestGovernancaPlanos Governanca =>
        Services.GetRequiredService<TestGovernancaPlanos>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IPlanosServico>();
            services.RemoveAll<IUnidadeContextoConsulta>();
            services.RemoveAll<IGovernancaOperacionalUnidade>();
            services.AddSingleton(provider => new TestPlanosServico(
                OrganizacaoId, UnidadeId));
            services.AddSingleton<IPlanosServico>(provider =>
                provider.GetRequiredService<TestPlanosServico>());
            services.AddSingleton<IUnidadeContextoConsulta>(
                new TestUnidadeContextoPlanos(OrganizacaoId, UnidadeId));
            services.AddSingleton<TestGovernancaPlanos>();
            services.AddSingleton<IGovernancaOperacionalUnidade>(provider =>
                provider.GetRequiredService<TestGovernancaPlanos>());
        });
    }
}

public sealed class TestPlanosServico(Guid organizacaoId, Guid unidadeId)
    : IPlanosServico
{
    public Guid PlanoId { get; } = Guid.NewGuid();
    public bool PodeGerenciarLocal { get; set; } = true;
    public bool PossuiFranqueadoAtivo { get; set; }
    public bool PermitirLeituraLocal { get; set; } = true;
    public CriarPlanoSolicitacao? UltimaCriacao { get; private set; }
    public CriarPlanoSolicitacao? UltimaCriacaoLocal { get; private set; }
    public PlanoTermosSolicitacao? UltimaNovaVersao { get; private set; }
    public EstadoPlanos EstadoNovaVersaoRede { get; set; } = EstadoPlanos.Sucesso;
    public Guid? UltimaUnidadeId { get; private set; }

    private PlanoResumo Resumo => new(
        PlanoId, "Plano BFA 3x", true,
        new(Guid.NewGuid(), 1, 12, 3, 280m, true, 100m,
            new DateOnly(2026, 9, 1), null));

    private ContextoPlanosResumo ContextoRede =>
        new(organizacaoId, null, null, true, false);

    private ContextoPlanosResumo ContextoLocal =>
        new(organizacaoId, unidadeId, "BFA Cerquilho",
            PodeGerenciarLocal, PossuiFranqueadoAtivo);

    public Task<ResultadoPlanos<ListaPlanosResultado>> ListarRedeAsync(
        Guid usuarioId, FiltroPlanos filtro, CancellationToken cancellationToken) =>
        Task.FromResult(new ResultadoPlanos<ListaPlanosResultado>(
            EstadoPlanos.Sucesso, new(ContextoRede, [Resumo])));

    public Task<ResultadoPlanos<DetalhePlanoResultado>> ObterRedeAsync(
        Guid usuarioId, Guid planoId, CancellationToken cancellationToken) =>
        Task.FromResult(Detalhe(ContextoRede));

    public Task<ResultadoPlanos<Guid>> CriarRedeAsync(
        Guid usuarioId, CriarPlanoSolicitacao solicitacao,
        CancellationToken cancellationToken)
    {
        UltimaCriacao = solicitacao;
        return Task.FromResult(new ResultadoPlanos<Guid>(EstadoPlanos.Sucesso, PlanoId));
    }

    public Task<ResultadoPlanos<Guid>> CriarNovaVersaoRedeAsync(
        Guid usuarioId, Guid planoId, PlanoTermosSolicitacao solicitacao,
        CancellationToken cancellationToken)
    {
        UltimaNovaVersao = solicitacao;
        return EstadoNovaVersaoRede == EstadoPlanos.Sucesso
            ? Sucesso(planoId)
            : Task.FromResult(new ResultadoPlanos<Guid>(EstadoNovaVersaoRede));
    }

    public Task<ResultadoPlanos<Guid>> AlterarEstadoRedeAsync(
        Guid usuarioId, Guid planoId, bool ativar,
        CancellationToken cancellationToken) => Sucesso(planoId);

    public Task<ResultadoPlanos<ListaPlanosResultado>> ListarLocalAsync(
        Guid usuarioId, Guid idUnidade, FiltroPlanos filtro,
        CancellationToken cancellationToken) => Task.FromResult(
            PermitirLeituraLocal && idUnidade == unidadeId
                ? new ResultadoPlanos<ListaPlanosResultado>(
                    EstadoPlanos.Sucesso, new(ContextoLocal, [Resumo]))
                : new ResultadoPlanos<ListaPlanosResultado>(EstadoPlanos.SemAcesso));

    public Task<ResultadoPlanos<DetalhePlanoResultado>> ObterLocalAsync(
        Guid usuarioId, Guid idUnidade, Guid planoId,
        CancellationToken cancellationToken) => Task.FromResult(
            PermitirLeituraLocal && idUnidade == unidadeId
                ? Detalhe(ContextoLocal)
                : new ResultadoPlanos<DetalhePlanoResultado>(EstadoPlanos.SemAcesso));

    public Task<ResultadoPlanos<Guid>> CriarLocalAsync(
        Guid usuarioId, Guid idUnidade, CriarPlanoSolicitacao solicitacao,
        CancellationToken cancellationToken)
    {
        UltimaUnidadeId = idUnidade;
        UltimaCriacaoLocal = solicitacao;
        return Sucesso(PlanoId);
    }

    public Task<ResultadoPlanos<Guid>> CriarNovaVersaoLocalAsync(
        Guid usuarioId, Guid idUnidade, Guid planoId,
        PlanoTermosSolicitacao solicitacao, CancellationToken cancellationToken) =>
        Sucesso(planoId);

    public Task<ResultadoPlanos<Guid>> AlterarEstadoLocalAsync(
        Guid usuarioId, Guid idUnidade, Guid planoId, bool ativar,
        CancellationToken cancellationToken) => Sucesso(planoId);

    private ResultadoPlanos<DetalhePlanoResultado> Detalhe(
        ContextoPlanosResumo contexto)
    {
        var versao = Resumo.VersaoAtual!;
        return new(EstadoPlanos.Sucesso, new(contexto,
            new(PlanoId, organizacaoId, contexto.UnidadeId, Resumo.Nome, true, [versao])));
    }

    private static Task<ResultadoPlanos<Guid>> Sucesso(Guid planoId) =>
        Task.FromResult(new ResultadoPlanos<Guid>(EstadoPlanos.Sucesso, planoId));
}

public sealed class TestUnidadeContextoPlanos(Guid organizacaoId, Guid unidadeId)
    : IUnidadeContextoConsulta
{
    public Task<UnidadeContextoResumo?> ObterAtivaAsync(
        Guid id, CancellationToken cancellationToken) => Task.FromResult(
            id == unidadeId
                ? new UnidadeContextoResumo(organizacaoId, unidadeId, "BFA Cerquilho")
                : null);
}

public sealed class TestGovernancaPlanos : IGovernancaOperacionalUnidade
{
    public GovernancaOperacionalUnidade Valor { get; set; } = new(false, true, false);

    public Task<GovernancaOperacionalUnidade> ObterAsync(
        Guid usuarioId, Guid organizacaoId, Guid unidadeId,
        CancellationToken cancellationToken) => Task.FromResult(Valor);
}
