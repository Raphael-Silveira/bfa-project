using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BFA.Domain.Acessos;
using BFA.Domain.Localidades;
using BFA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BFA.IntegrationTests;

public sealed partial class UsuariosFranqueadoraEndpointTests
{
    [Fact]
    public async Task Endpoint_municipios_exige_administrador_rede()
    {
        using var anonima = new UsuariosFranqueadoraWebApplicationFactory();
        using var clienteAnonimo = CriarCliente(anonima);

        using var respostaAnonima = await clienteAnonimo.GetAsync(
            "/franqueadora/localidades/municipios?estadoCodigoIbge=35");
        Assert.Equal(HttpStatusCode.Found, respostaAnonima.StatusCode);
        Assert.StartsWith("/login?", respostaAnonima.Headers.Location?.PathAndQuery);

        using var semPerfil = new UsuariosFranqueadoraWebApplicationFactory();
        await semPerfil.InicializarAdministradorAsync(PerfilAcesso.AdministradorUnidade);
        using var clienteSemPerfil = CriarCliente(semPerfil);
        await LoginAsync(clienteSemPerfil, semPerfil);

        using var respostaSemPerfil = await clienteSemPerfil.GetAsync(
            "/franqueadora/localidades/municipios?estadoCodigoIbge=35");
        Assert.Equal(HttpStatusCode.Found, respostaSemPerfil.StatusCode);
        Assert.StartsWith("/acesso-negado?", respostaSemPerfil.Headers.Location?.PathAndQuery);
    }

    [Fact]
    public async Task Endpoint_retorna_json_minimo_ativo_ordenado_e_filtrado_por_estado()
    {
        using var application = new UsuariosFranqueadoraWebApplicationFactory();
        await application.InicializarAdministradorAsync();
        await AdicionarLocalidadesParaConsultaAsync(application);
        using var client = CriarCliente(application);
        await LoginAsync(client, application);

        using var response = await client.GetAsync(
            "/franqueadora/localidades/municipios?estadoCodigoIbge=35");
        var json = await response.Content.ReadAsStringAsync();
        var municipios = await response.Content
            .ReadFromJsonAsync<MunicipioEndpointResponse[]>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            ["Campinas", "Tietê"],
            Assert.IsType<MunicipioEndpointResponse[]>(municipios)
                .Select(item => item.Nome));
        Assert.DoesNotContain("Município inativo", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Niterói", json, StringComparison.Ordinal);
        using var document = JsonDocument.Parse(json);
        Assert.All(document.RootElement.EnumerateArray(), item =>
            Assert.Equal(
                ["codigoIbge", "nome"],
                item.EnumerateObject().Select(property => property.Name)));
        using var estadoInativo = await client.GetAsync(
            "/franqueadora/localidades/municipios?estadoCodigoIbge=33");
        var municipiosEstadoInativo = await estadoInativo.Content
            .ReadFromJsonAsync<MunicipioEndpointResponse[]>();
        Assert.Empty(Assert.IsType<MunicipioEndpointResponse[]>(municipiosEstadoInativo));
        Assert.Equal(0, application.IbgeClient.Execucoes);
    }

    [Fact]
    public async Task Formulario_lista_estados_ativos_ordenados_e_nao_carrega_todos_municipios()
    {
        using var application = new UsuariosFranqueadoraWebApplicationFactory();
        await application.InicializarAdministradorAsync();
        await AdicionarEstadosParaFormularioAsync(application);
        using var client = CriarCliente(application);
        await LoginAsync(client, application);

        var html = WebUtility.HtmlDecode(
            await client.GetStringAsync("/franqueadora/usuarios/novo"));

        Assert.True(
            html.IndexOf("Minas Gerais - MG", StringComparison.Ordinal)
            < html.IndexOf("São Paulo - SP", StringComparison.Ordinal));
        Assert.DoesNotContain("Rio de Janeiro - RJ", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Tietê</option>", html, StringComparison.Ordinal);
        Assert.Contains("Selecione primeiro o Estado", html, StringComparison.Ordinal);
        Assert.Equal(0, application.IbgeClient.Execucoes);
    }

    [Fact]
    public async Task Catalogo_vazio_exibe_estado_controlado_e_rejeita_post()
    {
        using var application = new UsuariosFranqueadoraWebApplicationFactory();
        var organizacaoId = await application.InicializarAdministradorAsync(
            incluirCatalogoLocalidades: false);
        var unidade = await AdicionarUnidadeAsync(
            application,
            organizacaoId,
            "BFA Sem Catálogo");
        using var client = CriarCliente(application);
        await LoginAsync(client, application);

        var getHtml = WebUtility.HtmlDecode(
            await client.GetStringAsync("/franqueadora/usuarios/novo"));
        using var post = await CadastrarFranqueadoAsync(
            client,
            $"sem-catalogo-{Guid.NewGuid():N}@bfa.test",
            [unidade.Id]);
        var postHtml = WebUtility.HtmlDecode(await post.Content.ReadAsStringAsync());

        Assert.Contains("Catálogo de localidades não carregado.", getHtml, StringComparison.Ordinal);
        Assert.Contains("Catálogo de localidades não carregado.", postHtml, StringComparison.Ordinal);
        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        Assert.Empty(dbContext.Franqueados);
        Assert.Equal(0, application.IbgeClient.Execucoes);
    }

    [Theory]
    [InlineData(99, 3554508, "Estado ativo")]
    [InlineData(35, 9999999, "Município ativo")]
    public async Task Codigo_de_localidade_inexistente_rejeita_cadastro_sem_residuo(
        int estadoCodigoIbge,
        int municipioCodigoIbge,
        string mensagemEsperada)
    {
        using var application = new UsuariosFranqueadoraWebApplicationFactory();
        var organizacaoId = await application.InicializarAdministradorAsync();
        var unidade = await AdicionarUnidadeAsync(application, organizacaoId, "BFA Localidade");
        using var client = CriarCliente(application);
        await LoginAsync(client, application);
        var email = $"localidade-invalida-{Guid.NewGuid():N}@bfa.test";

        using var response = await CadastrarFranqueadoAsync(
            client,
            email,
            [unidade.Id],
            estadoCodigoIbge: estadoCodigoIbge,
            municipioCodigoIbge: municipioCodigoIbge);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(mensagemEsperada, html, StringComparison.Ordinal);
        await AssertSemCadastroParcialAsync(application, email);
        Assert.Equal(0, application.IbgeClient.Execucoes);
    }

    [Fact]
    public async Task Municipio_de_outro_estado_e_rejeitado_sem_chamar_ibge()
    {
        using var application = new UsuariosFranqueadoraWebApplicationFactory();
        var organizacaoId = await application.InicializarAdministradorAsync();
        var unidade = await AdicionarUnidadeAsync(application, organizacaoId, "BFA Paraná");
        await AdicionarParanaAsync(application);
        using var client = CriarCliente(application);
        await LoginAsync(client, application);
        var email = $"municipio-outra-uf-{Guid.NewGuid():N}@bfa.test";

        using var response = await CadastrarFranqueadoAsync(
            client,
            email,
            [unidade.Id],
            estadoCodigoIbge: 41,
            municipioCodigoIbge:
                UsuariosFranqueadoraWebApplicationFactory.MunicipioPadraoCodigoIbge);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Contains(
            "Município ativo pertencente ao Estado informado",
            html,
            StringComparison.Ordinal);
        await AssertSemCadastroParcialAsync(application, email);
        Assert.Equal(0, application.IbgeClient.Execucoes);
    }

    [Fact]
    public async Task Scripts_filtram_no_cliente_e_endpoint_so_e_chamado_na_troca_de_estado()
    {
        using var application = new UsuariosFranqueadoraWebApplicationFactory();
        using var client = CriarCliente(application);

        var combobox = await client.GetStringAsync("/js/bfa-searchable-combobox.js");
        var cascata = await client.GetStringAsync("/js/bfa-localidades-cascade.js");

        Assert.Contains("normalizar", combobox, StringComparison.Ordinal);
        Assert.Contains("ArrowDown", combobox, StringComparison.Ordinal);
        Assert.Contains("ArrowUp", combobox, StringComparison.Ordinal);
        Assert.Contains("Enter", combobox, StringComparison.Ordinal);
        Assert.Contains("Escape", combobox, StringComparison.Ordinal);
        Assert.Contains("aria-autocomplete", combobox, StringComparison.Ordinal);
        Assert.Contains("estado.addEventListener(\"change\"", cascata, StringComparison.Ordinal);
        Assert.DoesNotContain("addEventListener(\"input\"", cascata, StringComparison.Ordinal);
        Assert.Contains("const controle = new AbortController()", cascata, StringComparison.Ordinal);
        Assert.Contains("requisicaoAtual?.abort()", cascata, StringComparison.Ordinal);
        Assert.Contains("controle.signal.aborted", cascata, StringComparison.Ordinal);
        Assert.Contains("requisicaoAtual === controle", cascata, StringComparison.Ordinal);
        Assert.Contains("estado.value === estadoCodigoIbge", cascata, StringComparison.Ordinal);
        Assert.Contains("if (!resposta.ok)", cascata, StringComparison.Ordinal);
        Assert.Contains(
            "Não foi possível carregar os municípios. Tente novamente.",
            cascata,
            StringComparison.Ordinal);
    }

    private static async Task AdicionarLocalidadesParaConsultaAsync(
        UsuariosFranqueadoraWebApplicationFactory application)
    {
        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        var agoraUtc = DateTime.UtcNow;
        var rio = new Estado(33, "RJ", "Rio de Janeiro", agoraUtc);
        rio.Desativar(agoraUtc.AddMinutes(1));
        var inativo = new Municipio(3500000, 35, "Município inativo", agoraUtc);
        inativo.Desativar(agoraUtc.AddMinutes(1));
        dbContext.Estados.Add(rio);
        dbContext.Municipios.AddRange(
            new Municipio(3509502, 35, "Campinas", agoraUtc),
            inativo,
            new Municipio(3303302, 33, "Niterói", agoraUtc));
        await dbContext.SaveChangesAsync();
    }

    private static async Task AdicionarEstadosParaFormularioAsync(
        UsuariosFranqueadoraWebApplicationFactory application)
    {
        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        var agoraUtc = DateTime.UtcNow;
        var rio = new Estado(33, "RJ", "Rio de Janeiro", agoraUtc);
        rio.Desativar(agoraUtc.AddMinutes(1));
        dbContext.Estados.AddRange(
            new Estado(31, "MG", "Minas Gerais", agoraUtc),
            rio);
        await dbContext.SaveChangesAsync();
    }

    private static async Task AdicionarParanaAsync(
        UsuariosFranqueadoraWebApplicationFactory application)
    {
        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        var agoraUtc = DateTime.UtcNow;
        dbContext.Estados.Add(new Estado(41, "PR", "Paraná", agoraUtc));
        dbContext.Municipios.Add(new Municipio(4106902, 41, "Curitiba", agoraUtc));
        await dbContext.SaveChangesAsync();
    }

    private sealed record MunicipioEndpointResponse(int CodigoIbge, string Nome);
}
