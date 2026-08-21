using System.Net;
using System.Text.RegularExpressions;
using BFA.Domain.Acessos;
using BFA.Domain.Franqueados;
using BFA.Domain.Unidades;
using BFA.Infrastructure.Identity;
using BFA.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BFA.IntegrationTests;

public sealed partial class FranqueadosEndpointTests
{
    [Fact]
    public async Task Anonimo_e_administrador_unidade_nao_acessam_modulo()
    {
        using var anonima = new UsuariosFranqueadoraWebApplicationFactory();
        using var clienteAnonimo = CriarCliente(anonima);

        using var respostaAnonima = await clienteAnonimo.GetAsync("/franqueadora/franqueados");
        Assert.Equal(HttpStatusCode.Found, respostaAnonima.StatusCode);
        Assert.StartsWith("/login?", respostaAnonima.Headers.Location?.PathAndQuery, StringComparison.Ordinal);

        using var unidade = new UsuariosFranqueadoraWebApplicationFactory();
        await unidade.InicializarAdministradorAsync(PerfilAcesso.AdministradorUnidade);
        using var clienteUnidade = CriarCliente(unidade);
        await LoginAsync(clienteUnidade, unidade);

        using var respostaUnidade = await clienteUnidade.GetAsync("/franqueadora/franqueados");
        Assert.Equal(HttpStatusCode.Found, respostaUnidade.StatusCode);
        Assert.StartsWith("/acesso-negado?", respostaUnidade.Headers.Location?.PathAndQuery, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Lista_somente_tenant_atual_e_renderiza_menu_desktop_mobile_e_layout_responsivo()
    {
        using var application = new UsuariosFranqueadoraWebApplicationFactory();
        var organizacaoId = await application.InicializarAdministradorAsync();
        await AdicionarFranqueadoAsync(application, organizacaoId, "Melissa e Carlos Buffet Ltda");
        await AdicionarFranqueadoAsync(application, Guid.NewGuid(), "Franqueado de outra rede");
        using var client = CriarCliente(application);
        await LoginAsync(client, application);

        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/franqueadora/franqueados"));

        Assert.Contains("Melissa e Carlos Buffet Ltda", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Franqueado de outra rede", html, StringComparison.Ordinal);
        Assert.Contains("bfa-admin-desktop-list", html, StringComparison.Ordinal);
        Assert.Contains("bfa-admin-mobile-list", html, StringComparison.Ordinal);
        Assert.Contains("CPF / CNPJ", html, StringComparison.Ordinal);
        Assert.Contains("Unidades ativas", html, StringComparison.Ordinal);
        Assert.Contains("href=\"/franqueadora/franqueados/", html, StringComparison.Ordinal);
        var visaoGeral = html.IndexOf("href=\"/franqueadora\"", StringComparison.Ordinal);
        var usuarios = html.IndexOf("href=\"/franqueadora/usuarios\"", StringComparison.Ordinal);
        var unidades = html.IndexOf("href=\"/franqueadora/unidades\"", StringComparison.Ordinal);
        var franqueados = html.IndexOf("href=\"/franqueadora/franqueados\"", StringComparison.Ordinal);
        Assert.True(visaoGeral >= 0 && visaoGeral < usuarios);
        Assert.True(usuarios < unidades);
        Assert.True(unidades < franqueados);
    }

    [Fact]
    public async Task Url_adulterada_nao_expoe_detalhe_nem_edicao_de_outro_tenant()
    {
        using var application = new UsuariosFranqueadoraWebApplicationFactory();
        await application.InicializarAdministradorAsync();
        var externo = await AdicionarFranqueadoAsync(
            application,
            Guid.NewGuid(),
            "Franqueado externo");
        using var client = CriarCliente(application);
        await LoginAsync(client, application);

        using var detalhe = await client.GetAsync($"/franqueadora/franqueados/{externo.Id}");
        using var editar = await client.GetAsync($"/franqueadora/franqueados/{externo.Id}/editar");

        Assert.Equal(HttpStatusCode.NotFound, detalhe.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, editar.StatusCode);
    }

    [Fact]
    public async Task Detalhe_exibe_usuarios_unidades_e_link_na_edicao_do_usuario_quando_aplicavel()
    {
        using var application = new UsuariosFranqueadoraWebApplicationFactory();
        var organizacaoId = await application.InicializarAdministradorAsync();
        var franqueado = await AdicionarFranqueadoAsync(
            application,
            organizacaoId,
            "Melissa e Carlos Buffet Ltda",
            application.AdministradorId);
        var unidade = await AdicionarUnidadeAsync(application, organizacaoId, "BFA Cerquilho");
        var segundaUnidade = await AdicionarUnidadeAsync(
            application,
            organizacaoId,
            "BFA Tietê");
        await AdicionarVinculoUnidadeAsync(application, franqueado.Id, organizacaoId, unidade.Id);
        await AdicionarVinculoUnidadeAsync(
            application,
            franqueado.Id,
            organizacaoId,
            segundaUnidade.Id);
        using var client = CriarCliente(application);
        await LoginAsync(client, application);

        var detalhe = WebUtility.HtmlDecode(
            await client.GetStringAsync($"/franqueadora/franqueados/{franqueado.Id}"));
        var usuario = WebUtility.HtmlDecode(
            await client.GetStringAsync($"/franqueadora/usuarios/{application.AdministradorId}/editar"));

        Assert.Contains("Usuários vinculados", detalhe, StringComparison.Ordinal);
        Assert.Contains(application.AdministradorEmail, detalhe, StringComparison.Ordinal);
        Assert.Contains("Principal", detalhe, StringComparison.Ordinal);
        Assert.Contains("BFA Cerquilho", detalhe, StringComparison.Ordinal);
        Assert.Contains("BFA Tietê", detalhe, StringComparison.Ordinal);
        Assert.Contains("Vínculo ativo", detalhe, StringComparison.Ordinal);
        Assert.Contains("Unidade ativa", detalhe, StringComparison.Ordinal);
        Assert.Contains("Vínculo de Franqueado", usuario, StringComparison.Ordinal);
        Assert.Contains("Gerenciar franqueado", usuario, StringComparison.Ordinal);
        Assert.Contains($"/franqueadora/franqueados/{franqueado.Id}", usuario, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Editar_usuario_sem_relacao_comercial_nao_exibe_card_de_franqueado()
    {
        using var application = new UsuariosFranqueadoraWebApplicationFactory();
        await application.InicializarAdministradorAsync();
        using var client = CriarCliente(application);
        await LoginAsync(client, application);

        var html = WebUtility.HtmlDecode(
            await client.GetStringAsync($"/franqueadora/usuarios/{application.AdministradorId}/editar"));

        Assert.DoesNotContain("Vínculo de Franqueado", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Gerenciar franqueado", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Post_editar_aceita_cnpj_alfanumerico_e_persiste_localidade_oficial()
    {
        using var application = new UsuariosFranqueadoraWebApplicationFactory();
        var organizacaoId = await application.InicializarAdministradorAsync();
        var franqueado = await AdicionarFranqueadoAsync(
            application,
            organizacaoId,
            "Empresa anterior");
        using var client = CriarCliente(application);
        await LoginAsync(client, application);
        var url = $"/franqueadora/franqueados/{franqueado.Id}/editar";
        var token = await ObterAntiforgeryAsync(client, url);
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["TipoPessoa"] = nameof(TipoPessoaFranqueado.PessoaJuridica),
            ["NomeRazaoSocial"] = "Melissa e Carlos Buffet Ltda",
            ["NomeFantasia"] = "Melissa e Carlos",
            ["Documento"] = "AB.CDE.F12/3456-78",
            ["Telefone"] = "(15) 99999-9999",
            ["Email"] = "comercial@bfa.test",
            ["EmailFinanceiro"] = "financeiro@bfa.test",
            ["ResponsavelLegal"] = "Melissa Souza",
            ["Logradouro"] = "Rua do Esporte",
            ["Numero"] = "10",
            ["Bairro"] = "Centro",
            ["EstadoCodigoIbge"] = UsuariosFranqueadoraWebApplicationFactory.EstadoPadraoCodigoIbge.ToString(),
            ["MunicipioCodigoIbge"] = UsuariosFranqueadoraWebApplicationFactory.MunicipioPadraoCodigoIbge.ToString(),
            ["Cep"] = "18530-000",
            ["__RequestVerificationToken"] = token
        });

        using var response = await client.PostAsync(url, form);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal($"/franqueadora/franqueados/{franqueado.Id}", response.Headers.Location?.OriginalString);
        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        var atualizado = await dbContext.Franqueados.AsNoTracking().SingleAsync();
        Assert.Equal(TipoPessoaFranqueado.PessoaJuridica, atualizado.TipoPessoa);
        Assert.Equal("ABCDEF12345678", atualizado.Documento);
        Assert.Equal("SP", atualizado.Estado);
        Assert.Equal("Tietê", atualizado.Cidade);
    }

    [Fact]
    public async Task Vincular_unidade_reativa_relacoes_sem_duplicar_e_exige_antiforgery()
    {
        using var application = new UsuariosFranqueadoraWebApplicationFactory();
        var organizacaoId = await application.InicializarAdministradorAsync();
        var franqueado = await AdicionarFranqueadoAsync(
            application,
            organizacaoId,
            "Franqueado múltiplas unidades",
            application.AdministradorId);
        var unidade = await AdicionarUnidadeAsync(application, organizacaoId, "BFA Tietê");
        var relacao = await AdicionarVinculoUnidadeAsync(
            application,
            franqueado.Id,
            organizacaoId,
            unidade.Id,
            ativa: false);
        var acesso = await AdicionarAcessoUnidadeAsync(
            application,
            application.AdministradorId,
            organizacaoId,
            unidade.Id,
            ativo: false);
        using var client = CriarCliente(application);
        await LoginAsync(client, application);

        using var semToken = await client.PostAsync(
            $"/franqueadora/franqueados/{franqueado.Id}/unidades/adicionar",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["UnidadeId"] = unidade.Id.ToString()
            }));
        Assert.Equal(HttpStatusCode.BadRequest, semToken.StatusCode);

        var detalheUrl = $"/franqueadora/franqueados/{franqueado.Id}";
        var token = await ObterAntiforgeryAsync(client, detalheUrl);
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["UnidadeId"] = unidade.Id.ToString(),
            ["__RequestVerificationToken"] = token
        });
        using var response = await client.PostAsync(
            $"/franqueadora/franqueados/{franqueado.Id}/unidades/adicionar",
            form);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        Assert.Equal(1, await dbContext.FranqueadosUnidades.CountAsync());
        Assert.True((await dbContext.FranqueadosUnidades.FindAsync(relacao.Id))?.Ativo);
        Assert.Equal(2, await dbContext.VinculosAcesso.CountAsync());
        Assert.True((await dbContext.VinculosAcesso.FindAsync(acesso.Id))?.Ativo);
    }

    [Fact]
    public async Task Unidade_ocupada_e_bloqueada_com_mensagem_amigavel()
    {
        using var application = new UsuariosFranqueadoraWebApplicationFactory();
        var organizacaoId = await application.InicializarAdministradorAsync();
        var desejado = await AdicionarFranqueadoAsync(
            application,
            organizacaoId,
            "Franqueado desejado",
            application.AdministradorId);
        var ocupante = await AdicionarFranqueadoAsync(
            application,
            organizacaoId,
            "Franqueado ocupante");
        var unidade = await AdicionarUnidadeAsync(application, organizacaoId, "BFA Ocupada");
        await AdicionarVinculoUnidadeAsync(application, ocupante.Id, organizacaoId, unidade.Id);
        using var client = CriarCliente(application);
        await LoginAsync(client, application);
        var detalheUrl = $"/franqueadora/franqueados/{desejado.Id}";
        var token = await ObterAntiforgeryAsync(client, detalheUrl);
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["UnidadeId"] = unidade.Id.ToString(),
            ["__RequestVerificationToken"] = token
        });

        using var response = await client.PostAsync(
            $"/franqueadora/franqueados/{desejado.Id}/unidades/adicionar",
            form);
        var html = WebUtility.HtmlDecode(await client.GetStringAsync(detalheUrl));

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Contains("Esta unidade já possui um franqueado ativo.", html, StringComparison.Ordinal);
        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        Assert.Equal(1, await dbContext.FranqueadosUnidades.CountAsync());
    }

    [Fact]
    public async Task Desvincular_preserva_historico_unidade_e_outros_administradores()
    {
        using var application = new UsuariosFranqueadoraWebApplicationFactory();
        var organizacaoId = await application.InicializarAdministradorAsync();
        var franqueado = await AdicionarFranqueadoAsync(
            application,
            organizacaoId,
            "Franqueado com histórico",
            application.AdministradorId);
        var unidade = await AdicionarUnidadeAsync(application, organizacaoId, "BFA Sorocaba");
        var relacao = await AdicionarVinculoUnidadeAsync(application, franqueado.Id, organizacaoId, unidade.Id);
        var principal = await AdicionarAcessoUnidadeAsync(
            application,
            application.AdministradorId,
            organizacaoId,
            unidade.Id);
        var outroUsuarioId = await AdicionarUsuarioAsync(application);
        var outroAcesso = await AdicionarAcessoUnidadeAsync(
            application,
            outroUsuarioId,
            organizacaoId,
            unidade.Id);
        using var client = CriarCliente(application);
        await LoginAsync(client, application);
        var detalheUrl = $"/franqueadora/franqueados/{franqueado.Id}";
        var token = await ObterAntiforgeryAsync(client, detalheUrl);
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token
        });

        using var response = await client.PostAsync(
            $"/franqueadora/franqueados/{franqueado.Id}/unidades/{unidade.Id}/desativar",
            form);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        Assert.Equal(1, await dbContext.FranqueadosUnidades.CountAsync());
        Assert.False((await dbContext.FranqueadosUnidades.FindAsync(relacao.Id))?.Ativo);
        Assert.True((await dbContext.Unidades.FindAsync(unidade.Id))?.Ativa);
        Assert.False((await dbContext.VinculosAcesso.FindAsync(principal.Id))?.Ativo);
        Assert.True((await dbContext.VinculosAcesso.FindAsync(outroAcesso.Id))?.Ativo);
    }

    private static HttpClient CriarCliente(UsuariosFranqueadoraWebApplicationFactory application) =>
        application.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

    private static async Task LoginAsync(
        HttpClient client,
        UsuariosFranqueadoraWebApplicationFactory application)
    {
        var token = await ObterAntiforgeryAsync(client, "/login");
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = application.AdministradorEmail,
            ["Senha"] = application.AdministradorSenha,
            ["LembrarMe"] = "false",
            ["ReturnUrl"] = string.Empty,
            ["__RequestVerificationToken"] = token
        });
        using var response = await client.PostAsync("/login", form);
        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
    }

    private static async Task<string> ObterAntiforgeryAsync(HttpClient client, string url)
    {
        var html = await client.GetStringAsync(url);
        var match = AntiforgeryToken().Match(html);
        Assert.True(match.Success, "Token antiforgery não encontrado.");
        return WebUtility.HtmlDecode(match.Groups["token"].Value);
    }

    private static async Task<Franqueado> AdicionarFranqueadoAsync(
        UsuariosFranqueadoraWebApplicationFactory application,
        Guid organizacaoId,
        string nome,
        Guid? usuarioPrincipalId = null)
    {
        var franqueado = new Franqueado(
            Guid.NewGuid(),
            organizacaoId,
            TipoPessoaFranqueado.PessoaFisica,
            nome,
            GerarCpf(),
            $"franqueado-{Guid.NewGuid():N}@bfa.test",
            DateTime.UtcNow);
        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        dbContext.Franqueados.Add(franqueado);

        if (usuarioPrincipalId is { } usuarioId)
        {
            dbContext.FranqueadosUsuarios.Add(new FranqueadoUsuario(
                Guid.NewGuid(),
                franqueado.Id,
                usuarioId,
                principal: true,
                DateTime.UtcNow));
        }

        await dbContext.SaveChangesAsync();
        return franqueado;
    }

    private static string GerarCpf() =>
        Random.Shared.NextInt64(10_000_000_000, 99_999_999_999).ToString();

    private static async Task<Unidade> AdicionarUnidadeAsync(
        UsuariosFranqueadoraWebApplicationFactory application,
        Guid organizacaoId,
        string nome)
    {
        var unidade = new Unidade(
            Guid.NewGuid(),
            organizacaoId,
            nome,
            $"unidade-{Guid.NewGuid():N}",
            DateTime.UtcNow);
        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        dbContext.Unidades.Add(unidade);
        await dbContext.SaveChangesAsync();
        return unidade;
    }

    private static async Task<FranqueadoUnidade> AdicionarVinculoUnidadeAsync(
        UsuariosFranqueadoraWebApplicationFactory application,
        Guid franqueadoId,
        Guid organizacaoId,
        Guid unidadeId,
        bool ativa = true)
    {
        var relacao = new FranqueadoUnidade(
            Guid.NewGuid(),
            franqueadoId,
            organizacaoId,
            unidadeId,
            DateTime.UtcNow);

        if (!ativa)
        {
            relacao.Desativar(DateTime.UtcNow);
        }

        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        dbContext.FranqueadosUnidades.Add(relacao);
        await dbContext.SaveChangesAsync();
        return relacao;
    }

    private static async Task<VinculoAcesso> AdicionarAcessoUnidadeAsync(
        UsuariosFranqueadoraWebApplicationFactory application,
        Guid usuarioId,
        Guid organizacaoId,
        Guid unidadeId,
        bool ativo = true)
    {
        var acesso = new VinculoAcesso(
            Guid.NewGuid(),
            usuarioId,
            organizacaoId,
            unidadeId,
            PerfilAcesso.AdministradorUnidade,
            DateTime.UtcNow);

        if (!ativo)
        {
            acesso.Desativar(DateTime.UtcNow);
        }

        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        dbContext.VinculosAcesso.Add(acesso);
        await dbContext.SaveChangesAsync();
        return acesso;
    }

    private static async Task<Guid> AdicionarUsuarioAsync(
        UsuariosFranqueadoraWebApplicationFactory application)
    {
        var usuarioId = Guid.NewGuid();
        await using var scope = application.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<UsuarioIdentity>>();
        var usuario = new UsuarioIdentity
        {
            Id = usuarioId,
            Email = $"outro-{usuarioId:N}@bfa.test",
            UserName = $"outro-{usuarioId:N}@bfa.test"
        };
        var resultado = await userManager.CreateAsync(usuario);
        Assert.True(resultado.Succeeded);
        return usuarioId;
    }

    [GeneratedRegex(
        "name=\"__RequestVerificationToken\"[^>]*value=\"(?<token>[^\"]+)\"",
        RegexOptions.CultureInvariant)]
    private static partial Regex AntiforgeryToken();
}
