using System.Net;
using System.Text.RegularExpressions;
using BFA.Application.Franqueadora.Usuarios;
using BFA.Domain.Acessos;
using BFA.Domain.Franqueados;
using BFA.Domain.Unidades;
using BFA.Domain.Usuarios;
using BFA.Infrastructure.Identity;
using BFA.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BFA.IntegrationTests;

public sealed partial class UsuariosFranqueadoraEndpointTests
{
    [Fact]
    public async Task Rotas_administrativas_exigem_autenticacao_e_administrador_rede()
    {
        using var anonima = new UsuariosFranqueadoraWebApplicationFactory();
        using var clienteAnonimo = CriarCliente(anonima);

        using var respostaAnonima = await clienteAnonimo.GetAsync("/franqueadora/usuarios");
        Assert.Equal(HttpStatusCode.Found, respostaAnonima.StatusCode);
        Assert.StartsWith("/login?", respostaAnonima.Headers.Location?.PathAndQuery);
        using var edicaoAnonima = await clienteAnonimo.GetAsync(
            $"/franqueadora/usuarios/{Guid.NewGuid()}/editar");
        Assert.Equal(HttpStatusCode.Found, edicaoAnonima.StatusCode);
        Assert.StartsWith("/login?", edicaoAnonima.Headers.Location?.PathAndQuery);

        using var semPerfil = new UsuariosFranqueadoraWebApplicationFactory();
        await semPerfil.InicializarAdministradorAsync(PerfilAcesso.AdministradorUnidade);
        using var clienteSemPerfil = CriarCliente(semPerfil);
        await LoginAsync(clienteSemPerfil, semPerfil);

        using var respostaSemPerfil = await clienteSemPerfil.GetAsync("/franqueadora/usuarios");
        Assert.Equal(HttpStatusCode.Found, respostaSemPerfil.StatusCode);
        Assert.StartsWith("/acesso-negado?", respostaSemPerfil.Headers.Location?.PathAndQuery);
        using var edicaoSemPerfil = await clienteSemPerfil.GetAsync(
            $"/franqueadora/usuarios/{Guid.NewGuid()}/editar");
        Assert.Equal(HttpStatusCode.Found, edicaoSemPerfil.StatusCode);
        Assert.StartsWith("/acesso-negado?", edicaoSemPerfil.Headers.Location?.PathAndQuery);
    }

    [Fact]
    public async Task Listagem_isola_organizacao_remove_duplicidade_e_identifica_franqueado()
    {
        using var application = new UsuariosFranqueadoraWebApplicationFactory();
        var organizacaoId = await application.InicializarAdministradorAsync();
        var unidade = await AdicionarUnidadeAsync(application, organizacaoId, "BFA Tietê");
        var usuarioId = await AdicionarUsuarioRelacionadoAsync(
            application,
            organizacaoId,
            unidade,
            "Pessoa da Rede",
            "pessoa@bfa.test");
        var unidadeAcessoAdicional = await AdicionarUnidadeAsync(
            application,
            organizacaoId,
            "BFA Porto Feliz");
        await AdicionarAcessoUsuarioAsync(
            application,
            usuarioId,
            organizacaoId,
            unidadeAcessoAdicional.Id);
        var administradorUnidadeId = await AdicionarAdministradorUnidadeAsync(
            application,
            organizacaoId,
            unidade.Id);
        await AdicionarUsuarioExternoAsync(application);
        using var client = CriarCliente(application);
        await LoginAsync(client, application);

        await using var scope = application.Services.CreateAsyncScope();
        var consulta = scope.ServiceProvider.GetRequiredService<IUsuariosFranqueadoraConsulta>();
        var resultado = await consulta.ListarAsync(
            application.AdministradorId,
            CancellationToken.None);
        var usuarios = Assert.IsAssignableFrom<IReadOnlyList<UsuarioFranqueadoraResumo>>(
            resultado.Valor);

        Assert.Equal(3, usuarios.Count);
        var bootstrap = Assert.Single(usuarios, item => item.Id == application.AdministradorId);
        Assert.Equal(application.AdministradorEmail, bootstrap.Nome);
        Assert.True(bootstrap.AcessoTodaRede);
        var relacionado = Assert.Single(usuarios, item => item.Id == usuarioId);
        Assert.Equal("Pessoa da Rede", relacionado.Nome);
        Assert.Contains("Administrador de unidade", relacionado.Funcoes);
        Assert.Contains("Professor", relacionado.Funcoes);
        Assert.Contains("Franqueado", relacionado.Funcoes);
        Assert.Equal(["BFA Porto Feliz", "BFA Tietê"], relacionado.Unidades);
        var administradorUnidade = Assert.Single(
            usuarios,
            item => item.Id == administradorUnidadeId);
        Assert.Contains("Administrador de unidade", administradorUnidade.Funcoes);
        Assert.DoesNotContain("Franqueado", administradorUnidade.Funcoes);
        Assert.False(administradorUnidade.AcessoTodaRede);

        var html = WebUtility.HtmlDecode(
            await client.GetStringAsync("/franqueadora/usuarios"));
        Assert.Contains("bfa-admin-desktop-list", html, StringComparison.Ordinal);
        Assert.Contains("bfa-admin-mobile-list", html, StringComparison.Ordinal);
        Assert.Contains("Novo usuário", html, StringComparison.Ordinal);
        Assert.Contains("Acesso às unidades", html, StringComparison.Ordinal);
        Assert.Contains("Franqueado", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Usuário externo", html, StringComparison.Ordinal);
        Assert.Contains(
            $"href=\"/franqueadora/usuarios/{usuarioId}/editar\"",
            html,
            StringComparison.Ordinal);
        Assert.Equal(
            2,
            Regex.Matches(
                html,
                $"href=\"/franqueadora/usuarios/{usuarioId}/editar\"").Count);
        Assert.Equal(
            [
                "/franqueadora",
                "/franqueadora/usuarios",
                "/franqueadora/unidades",
                "/franqueadora",
                "/franqueadora/usuarios",
                "/franqueadora/unidades"
            ],
            LinksMenu().Matches(html)
                .Select(match => match.Groups["rota"].Value)
                .ToArray());
    }

    [Fact]
    public async Task Formulario_nao_expoe_organizacao_e_post_exige_antiforgery()
    {
        using var application = new UsuariosFranqueadoraWebApplicationFactory();
        await application.InicializarAdministradorAsync();
        using var client = CriarCliente(application);
        await LoginAsync(client, application);

        var html = await client.GetStringAsync("/franqueadora/usuarios/novo");

        Assert.Contains("action=\"/franqueadora/usuarios/novo\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"TipoCadastro\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"NomeCompleto\"", html, StringComparison.Ordinal);
        Assert.Contains("data-franqueado-section", html, StringComparison.Ordinal);
        Assert.Contains("data-pessoa-juridica", html, StringComparison.Ordinal);
        Assert.Contains("data-documento-help", html, StringComparison.Ordinal);
        Assert.Contains("name=\"EstadoCodigoIbge\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"MunicipioCodigoIbge\"", html, StringComparison.Ordinal);
        Assert.Contains("São Paulo - SP", WebUtility.HtmlDecode(html), StringComparison.Ordinal);
        Assert.Contains("data-bfa-combobox", html, StringComparison.Ordinal);
        Assert.Contains("data-bfa-localidades", html, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"Estado\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"Cidade\"", html, StringComparison.Ordinal);
        Assert.Contains("Dados da Empresa", html, StringComparison.Ordinal);
        Assert.Contains("Unidades da franquia", html, StringComparison.Ordinal);
        Assert.Contains(
            "Selecione as unidades que serão operadas por este franqueado. O usuário principal receberá acesso administrativo às unidades selecionadas.",
            WebUtility.HtmlDecode(html),
            StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"OrganizacaoId\"", html, StringComparison.Ordinal);
        var mascaras = await client.GetStringAsync("/js/bfa-input-masks.js");
        Assert.Contains("XX.XXX.XXX/XXXX-00", mascaras, StringComparison.Ordinal);
        Assert.Contains("[^A-Z0-9]", mascaras, StringComparison.Ordinal);

        using var semToken = await client.PostAsync(
            "/franqueadora/usuarios/novo",
            new FormUrlEncodedContent([]));
        Assert.Equal(HttpStatusCode.BadRequest, semToken.StatusCode);

        using var edicaoSemToken = await client.PostAsync(
            $"/franqueadora/usuarios/{application.AdministradorId}/editar",
            new FormUrlEncodedContent([]));
        Assert.Equal(HttpStatusCode.BadRequest, edicaoSemToken.StatusCode);
    }

    [Fact]
    public async Task Administrador_edita_dados_da_propria_organizacao_sem_alterar_relacoes()
    {
        using var application = new UsuariosFranqueadoraWebApplicationFactory();
        var organizacaoId = await application.InicializarAdministradorAsync();
        var unidade = await AdicionarUnidadeAsync(application, organizacaoId, "BFA Edição");
        var usuarioId = await AdicionarUsuarioRelacionadoAsync(
            application,
            organizacaoId,
            unidade,
            "Pessoa Original",
            "original@bfa.test");
        using var client = CriarCliente(application);
        await LoginAsync(client, application);

        Guid[] vinculosIds;
        Guid[] franqueadosUsuariosIds;
        Guid[] franqueadosUnidadesIds;
        await using (var scope = application.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
            vinculosIds = await dbContext.VinculosAcesso
                .Where(item => item.UsuarioId == usuarioId)
                .Select(item => item.Id)
                .OrderBy(id => id)
                .ToArrayAsync();
            franqueadosUsuariosIds = await dbContext.FranqueadosUsuarios
                .Where(item => item.UsuarioId == usuarioId)
                .Select(item => item.Id)
                .OrderBy(id => id)
                .ToArrayAsync();
            var franqueadosIds = await dbContext.FranqueadosUsuarios
                .Where(item => item.UsuarioId == usuarioId)
                .Select(item => item.FranqueadoId)
                .ToArrayAsync();
            franqueadosUnidadesIds = await dbContext.FranqueadosUnidades
                .Where(item => franqueadosIds.Contains(item.FranqueadoId))
                .Select(item => item.Id)
                .OrderBy(id => id)
                .ToArrayAsync();
        }

        using var get = await client.GetAsync(
            $"/franqueadora/usuarios/{usuarioId}/editar");
        var htmlGet = WebUtility.HtmlDecode(await get.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        Assert.Contains("Editar usuário", htmlGet, StringComparison.Ordinal);
        Assert.Contains("Dados do usuário", htmlGet, StringComparison.Ordinal);
        Assert.Contains("value=\"Pessoa Original\"", htmlGet, StringComparison.Ordinal);
        Assert.Contains("value=\"original@bfa.test\"", htmlGet, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"TipoCadastro\"", htmlGet, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"OrganizacaoId\"", htmlGet, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"PerfilAcesso\"", htmlGet, StringComparison.Ordinal);

        using var post = await EditarUsuarioAsync(
            client,
            usuarioId,
            "  Pessoa Atualizada  ",
            "atualizada@bfa.test",
            "(11) 98765-4321");
        Assert.Equal(HttpStatusCode.Found, post.StatusCode);
        Assert.Equal("/franqueadora/usuarios", post.Headers.Location?.OriginalString);

        await using (var scope = application.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
            var usuario = await dbContext.Users.SingleAsync(item => item.Id == usuarioId);
            var perfil = await dbContext.PerfisUsuario.SingleAsync(
                item => item.UsuarioId == usuarioId);
            Assert.Equal("atualizada@bfa.test", usuario.Email);
            Assert.Equal("atualizada@bfa.test", usuario.UserName);
            Assert.Equal("ATUALIZADA@BFA.TEST", usuario.NormalizedEmail);
            Assert.Equal("ATUALIZADA@BFA.TEST", usuario.NormalizedUserName);
            Assert.Equal("Pessoa Atualizada", perfil.NomeCompleto);
            Assert.Equal("(11) 98765-4321", perfil.Telefone);
            Assert.Equal(
                vinculosIds,
                await dbContext.VinculosAcesso
                    .Where(item => item.UsuarioId == usuarioId)
                    .Select(item => item.Id)
                    .OrderBy(id => id)
                    .ToArrayAsync());
            Assert.Equal(
                franqueadosUsuariosIds,
                await dbContext.FranqueadosUsuarios
                    .Where(item => item.UsuarioId == usuarioId)
                    .Select(item => item.Id)
                    .OrderBy(id => id)
                    .ToArrayAsync());
            Assert.Equal(
                franqueadosUnidadesIds,
                await dbContext.FranqueadosUnidades
                    .Select(item => item.Id)
                    .OrderBy(id => id)
                    .ToArrayAsync());
        }

        using var listagem = await client.GetAsync("/franqueadora/usuarios");
        var htmlListagem = WebUtility.HtmlDecode(
            await listagem.Content.ReadAsStringAsync());
        Assert.Contains("Usuário atualizado com sucesso.", htmlListagem, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Email_duplicado_e_rejeitado_sem_alteracao_parcial()
    {
        using var application = new UsuariosFranqueadoraWebApplicationFactory();
        var organizacaoId = await application.InicializarAdministradorAsync();
        var unidade = await AdicionarUnidadeAsync(application, organizacaoId, "BFA Duplicidade");
        var usuarioId = await AdicionarUsuarioRelacionadoAsync(
            application,
            organizacaoId,
            unidade,
            "Pessoa Original",
            "pessoa-original@bfa.test");
        using var client = CriarCliente(application);
        await LoginAsync(client, application);

        using var response = await EditarUsuarioAsync(
            client,
            usuarioId,
            "Nome não persistido",
            application.AdministradorEmail.ToUpperInvariant(),
            "11911112222");
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Já existe um usuário cadastrado com este email.", html, StringComparison.Ordinal);
        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        var usuario = await dbContext.Users.SingleAsync(item => item.Id == usuarioId);
        var perfil = await dbContext.PerfisUsuario.SingleAsync(item => item.UsuarioId == usuarioId);
        Assert.Equal("pessoa-original@bfa.test", usuario.Email);
        Assert.Equal("pessoa-original@bfa.test", usuario.UserName);
        Assert.Equal("Pessoa Original", perfil.NomeCompleto);
        Assert.Equal("11999999999", perfil.Telefone);
    }

    [Fact]
    public async Task Edicao_administrativa_nao_sobrescreve_nome_usuario_especifico_de_professor()
    {
        using var application = new UsuariosFranqueadoraWebApplicationFactory();
        var organizacaoId = await application.InicializarAdministradorAsync();
        var unidade = await AdicionarUnidadeAsync(application, organizacaoId, "BFA Professor");
        var usuarioId = await AdicionarUsuarioRelacionadoAsync(
            application, organizacaoId, unidade, "Professor", "professor@bfa.test");
        await using (var scope = application.Services.CreateAsyncScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<UsuarioIdentity>>();
            var usuario = await userManager.FindByIdAsync(usuarioId.ToString());
            Assert.NotNull(usuario);
            Assert.True((await userManager.SetUserNameAsync(
                usuario, "professor.cerquilho")).Succeeded);
        }
        using var client = CriarCliente(application);
        await LoginAsync(client, application);

        using var response = await EditarUsuarioAsync(
            client,
            usuarioId,
            "Professor Atualizado",
            "novo-email@bfa.test",
            "11911112222");

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        await using var verificacao = application.Services.CreateAsyncScope();
        var dbContext = verificacao.ServiceProvider.GetRequiredService<BfaDbContext>();
        var atualizado = await dbContext.Users.SingleAsync(item => item.Id == usuarioId);
        Assert.Equal("novo-email@bfa.test", atualizado.Email);
        Assert.Equal("professor.cerquilho", atualizado.UserName);
    }

    [Fact]
    public async Task Usuario_bootstrap_nao_cria_perfil_no_get_e_cria_no_post_valido()
    {
        using var application = new UsuariosFranqueadoraWebApplicationFactory();
        await application.InicializarAdministradorAsync();
        using var client = CriarCliente(application);
        await LoginAsync(client, application);

        using var get = await client.GetAsync(
            $"/franqueadora/usuarios/{application.AdministradorId}/editar");
        var html = WebUtility.HtmlDecode(await get.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        Assert.Contains($"value=\"{application.AdministradorEmail}\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"NomeCompleto\" value=\"\"", html, StringComparison.Ordinal);

        await using (var scope = application.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
            Assert.False(await dbContext.PerfisUsuario.AnyAsync(
                item => item.UsuarioId == application.AdministradorId));
        }

        using var post = await EditarUsuarioAsync(
            client,
            application.AdministradorId,
            "Administrador Bootstrap",
            application.AdministradorEmail,
            string.Empty);
        Assert.Equal(HttpStatusCode.Found, post.StatusCode);

        await using (var scope = application.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
            var perfil = await dbContext.PerfisUsuario.SingleAsync(
                item => item.UsuarioId == application.AdministradorId);
            Assert.Equal("Administrador Bootstrap", perfil.NomeCompleto);
            Assert.Null(perfil.Telefone);
        }
    }

    [Fact]
    public async Task Usuario_externo_e_inexistente_retornam_not_found_sem_expor_dados()
    {
        using var application = new UsuariosFranqueadoraWebApplicationFactory();
        await application.InicializarAdministradorAsync();
        var usuarioExternoId = await AdicionarUsuarioExternoAsync(application);
        using var client = CriarCliente(application);
        await LoginAsync(client, application);

        using var externo = await client.GetAsync(
            $"/franqueadora/usuarios/{usuarioExternoId}/editar");
        using var inexistente = await client.GetAsync(
            $"/franqueadora/usuarios/{Guid.NewGuid()}/editar");
        Assert.Equal(HttpStatusCode.NotFound, externo.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, inexistente.StatusCode);

        using var postExterno = await EditarUsuarioAsync(
            client,
            usuarioExternoId,
            "Tentativa externa",
            "tentativa@bfa.test",
            null,
            tokenUri: "/franqueadora/usuarios");
        Assert.Equal(HttpStatusCode.NotFound, postExterno.StatusCode);

        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        var perfil = await dbContext.PerfisUsuario.SingleAsync(
            item => item.UsuarioId == usuarioExternoId);
        Assert.Equal("Usuário externo", perfil.NomeCompleto);
        var usuario = await dbContext.Users.SingleAsync(item => item.Id == usuarioExternoId);
        Assert.Equal("externo@bfa.test", usuario.Email);
    }

    [Fact]
    public async Task Relacao_comercial_ativa_sem_vinculo_acesso_autoriza_edicao_no_tenant()
    {
        using var application = new UsuariosFranqueadoraWebApplicationFactory();
        var organizacaoId = await application.InicializarAdministradorAsync();
        var unidade = await AdicionarUnidadeAsync(
            application,
            organizacaoId,
            "Unidade apenas comercial");
        var usuarioId = await AdicionarUsuarioComRelacaoComercialAsync(
            application,
            organizacaoId,
            unidade);
        using var client = CriarCliente(application);
        await LoginAsync(client, application);

        using var get = await client.GetAsync(
            $"/franqueadora/usuarios/{usuarioId}/editar");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);

        await using (var consultaScope = application.Services.CreateAsyncScope())
        {
            var consulta = consultaScope.ServiceProvider
                .GetRequiredService<IUsuariosFranqueadoraConsulta>();
            var listagem = await consulta.ListarAsync(
                application.AdministradorId,
                CancellationToken.None);
            var usuarios = Assert.IsAssignableFrom<IReadOnlyList<UsuarioFranqueadoraResumo>>(
                listagem.Valor);
            var comercial = Assert.Single(usuarios, item => item.Id == usuarioId);
            Assert.Empty(comercial.Unidades);
            Assert.False(comercial.AcessoTodaRede);
        }

        var htmlListagem = WebUtility.HtmlDecode(
            await client.GetStringAsync("/franqueadora/usuarios"));
        Assert.Contains("Sem acesso a unidades", htmlListagem, StringComparison.Ordinal);
        Assert.DoesNotContain("Unidade apenas comercial", htmlListagem, StringComparison.Ordinal);

        using var post = await EditarUsuarioAsync(
            client,
            usuarioId,
            "Relação Comercial Atualizada",
            "comercial-atualizado@bfa.test",
            null);
        Assert.Equal(HttpStatusCode.Found, post.StatusCode);

        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        Assert.False(await dbContext.VinculosAcesso.AnyAsync(
            item => item.UsuarioId == usuarioId));
        Assert.Equal(
            "Relação Comercial Atualizada",
            (await dbContext.PerfisUsuario.SingleAsync(
                item => item.UsuarioId == usuarioId)).NomeCompleto);
        Assert.Single(dbContext.FranqueadosUsuarios, item => item.UsuarioId == usuarioId);
    }

    [Fact]
    public async Task Usuario_de_multiplas_organizacoes_recebe_conflito_e_nao_e_alterado()
    {
        using var application = new UsuariosFranqueadoraWebApplicationFactory();
        var organizacaoId = await application.InicializarAdministradorAsync();
        var unidade = await AdicionarUnidadeAsync(application, organizacaoId, "BFA Compartilhada");
        var usuarioId = await AdicionarUsuarioRelacionadoAsync(
            application,
            organizacaoId,
            unidade,
            "Pessoa Compartilhada",
            "compartilhada@bfa.test");
        await AdicionarVinculoOutraOrganizacaoAsync(application, usuarioId);
        using var client = CriarCliente(application);
        await LoginAsync(client, application);

        using var get = await client.GetAsync(
            $"/franqueadora/usuarios/{usuarioId}/editar");
        var html = WebUtility.HtmlDecode(await get.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Conflict, get.StatusCode);
        Assert.Contains("mais de uma Organização", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<form class=\"bfa-admin-form", html, StringComparison.Ordinal);

        using var post = await EditarUsuarioAsync(
            client,
            usuarioId,
            "Nome indevido",
            "indevido@bfa.test",
            "11900001111",
            tokenUri: "/franqueadora/usuarios");
        Assert.Equal(HttpStatusCode.Conflict, post.StatusCode);

        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        var usuario = await dbContext.Users.SingleAsync(item => item.Id == usuarioId);
        var perfil = await dbContext.PerfisUsuario.SingleAsync(item => item.UsuarioId == usuarioId);
        Assert.Equal("compartilhada@bfa.test", usuario.Email);
        Assert.Equal("Pessoa Compartilhada", perfil.NomeCompleto);
    }

    [Fact]
    public async Task Cadastro_administrador_rede_e_atomico_sem_senha_e_sem_franqueado()
    {
        using var application = new UsuariosFranqueadoraWebApplicationFactory();
        var organizacaoId = await application.InicializarAdministradorAsync();
        using var client = CriarCliente(application);
        await LoginAsync(client, application);
        var email = $"novo-admin-{Guid.NewGuid():N}@bfa.test";

        using var response = await CadastrarAdministradorAsync(client, email);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Usuário cadastrado", html, StringComparison.Ordinal);
        var link = ExtrairLinkPrimeiroAcesso(html);
        Assert.Matches(@"^https://localhost/definir-senha\?usuarioId=[0-9a-f-]+&token=[A-Za-z0-9_-]+$", link);
        var tokenPrimeiroAcesso = QueryHelpers.ParseQuery(new Uri(link).Query)["token"]
            .ToString();
        var htmlListagem = await client.GetStringAsync("/franqueadora/usuarios");
        Assert.DoesNotContain(tokenPrimeiroAcesso, htmlListagem, StringComparison.Ordinal);
        Assert.DoesNotContain("/definir-senha", htmlListagem, StringComparison.Ordinal);

        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        var usuario = await dbContext.Users.SingleAsync(item => item.Email == email);
        Assert.Equal(email, usuario.UserName);
        Assert.Null(usuario.PasswordHash);
        Assert.Single(dbContext.PerfisUsuario, perfil => perfil.UsuarioId == usuario.Id);
        var vinculo = Assert.Single(
            dbContext.VinculosAcesso,
            item => item.UsuarioId == usuario.Id);
        Assert.Equal(organizacaoId, vinculo.OrganizacaoId);
        Assert.Equal(PerfilAcesso.AdministradorRede, vinculo.Perfil);
        Assert.Null(vinculo.UnidadeId);
        Assert.Empty(dbContext.Franqueados);
        Assert.Empty(dbContext.FranqueadosUsuarios);
        Assert.Empty(dbContext.FranqueadosUnidades);
        Assert.Empty(dbContext.UserTokens);
    }

    [Fact]
    public async Task Email_existente_retorna_erro_controlado_sem_alteracao_implicita()
    {
        using var application = new UsuariosFranqueadoraWebApplicationFactory();
        await application.InicializarAdministradorAsync();
        using var client = CriarCliente(application);
        await LoginAsync(client, application);

        using var response = await CadastrarAdministradorAsync(
            client,
            application.AdministradorEmail.ToUpperInvariant());
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(
            "Já existe um usuário cadastrado com este email.",
            html,
            StringComparison.Ordinal);
        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        Assert.Equal(1, await dbContext.Users.CountAsync());
        Assert.Empty(dbContext.PerfisUsuario);
    }

    [Fact]
    public async Task Cadastro_franqueado_cria_relacoes_para_todas_as_unidades_selecionadas()
    {
        using var application = new UsuariosFranqueadoraWebApplicationFactory();
        var organizacaoId = await application.InicializarAdministradorAsync();
        var unidade1 = await AdicionarUnidadeAsync(application, organizacaoId, "BFA Tietê");
        var unidade2 = await AdicionarUnidadeAsync(application, organizacaoId, "BFA Sorocaba");
        using var client = CriarCliente(application);
        await LoginAsync(client, application);
        var email = $"franqueado-{Guid.NewGuid():N}@bfa.test";

        using var response = await CadastrarFranqueadoAsync(
            client,
            email,
            [unidade1.Id, unidade2.Id]);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        var usuario = await dbContext.Users.SingleAsync(item => item.Email == email);
        Assert.Null(usuario.PasswordHash);
        var perfil = await dbContext.PerfisUsuario.SingleAsync(item => item.UsuarioId == usuario.Id);
        Assert.Equal("Usuário Franqueado", perfil.NomeCompleto);
        Assert.Equal("(11) 99999-9999", perfil.Telefone);
        var franqueado = await dbContext.Franqueados.SingleAsync();
        Assert.Equal(organizacaoId, franqueado.OrganizacaoId);
        Assert.Equal("12345678901", franqueado.Documento);
        Assert.Equal("Usuário Franqueado", franqueado.NomeRazaoSocial);
        Assert.Equal(email, franqueado.Email);
        Assert.Equal("(11) 99999-9999", franqueado.Telefone);
        Assert.Null(franqueado.NomeFantasia);
        Assert.Null(franqueado.ResponsavelLegal);
        Assert.Equal("18000000", franqueado.Cep);
        Assert.Equal("SP", franqueado.Estado);
        Assert.Equal("Tietê", franqueado.Cidade);
        Assert.Equal(0, application.IbgeClient.Execucoes);
        var franqueadoUsuario = await dbContext.FranqueadosUsuarios.SingleAsync();
        Assert.Equal(usuario.Id, franqueadoUsuario.UsuarioId);
        Assert.True(franqueadoUsuario.Principal);
        Assert.Equal(2, await dbContext.FranqueadosUnidades.CountAsync());
        var acessos = await dbContext.VinculosAcesso
            .Where(item => item.UsuarioId == usuario.Id)
            .ToArrayAsync();
        Assert.Equal(2, acessos.Length);
        Assert.All(acessos, acesso =>
            Assert.Equal(PerfilAcesso.AdministradorUnidade, acesso.Perfil));
        Assert.Equal(
            new[] { unidade1.Id, unidade2.Id }.OrderBy(id => id),
            acessos.Select(item => item.UnidadeId!.Value).OrderBy(id => id));
    }

    [Theory]
    [InlineData(TipoPessoaFranqueado.PessoaFisica, "123.456.789-01", "12345678901")]
    [InlineData(TipoPessoaFranqueado.PessoaFisica, "12345678901", "12345678901")]
    [InlineData(TipoPessoaFranqueado.PessoaJuridica, "12.345.678/0001-99", "12345678000199")]
    [InlineData(TipoPessoaFranqueado.PessoaJuridica, "12345678000199", "12345678000199")]
    [InlineData(TipoPessoaFranqueado.PessoaJuridica, "AB.CDE.F12/3456-78", "ABCDEF12345678")]
    [InlineData(TipoPessoaFranqueado.PessoaJuridica, "abcdef12345678", "ABCDEF12345678")]
    public async Task Post_manual_aceita_documento_com_ou_sem_mascara_e_normaliza_no_servidor(
        TipoPessoaFranqueado tipoPessoa,
        string documento,
        string documentoEsperado)
    {
        using var application = new UsuariosFranqueadoraWebApplicationFactory();
        var organizacaoId = await application.InicializarAdministradorAsync();
        var unidade = await AdicionarUnidadeAsync(application, organizacaoId, "BFA Documento");
        using var client = CriarCliente(application);
        await LoginAsync(client, application);
        var email = $"documento-{Guid.NewGuid():N}@bfa.test";

        using var response = await CadastrarFranqueadoAsync(
            client,
            email,
            [unidade.Id],
            tipoPessoa,
            documento);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        var franqueado = await dbContext.Franqueados.SingleAsync();
        Assert.Equal(documentoEsperado, franqueado.Documento);
    }

    [Fact]
    public async Task Pessoa_fisica_ignora_campos_de_empresa_enviados_manualmente()
    {
        using var application = new UsuariosFranqueadoraWebApplicationFactory();
        var organizacaoId = await application.InicializarAdministradorAsync();
        var unidade = await AdicionarUnidadeAsync(application, organizacaoId, "BFA Pessoa Física");
        using var client = CriarCliente(application);
        await LoginAsync(client, application);
        var email = $"pessoa-fisica-{Guid.NewGuid():N}@bfa.test";

        using var response = await CadastrarFranqueadoAsync(
            client,
            email,
            [unidade.Id],
            camposAdicionais: new Dictionary<string, string>
            {
                ["NomeRazaoSocial"] = "Razão hostil",
                ["NomeFantasia"] = "Fantasia hostil",
                ["ResponsavelLegal"] = "Representante hostil",
                ["TelefoneFranqueado"] = "telefone hostil",
                ["EmailFranqueado"] = "email-invalido-hostil"
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        var franqueado = await dbContext.Franqueados.SingleAsync();
        Assert.Equal("Usuário Franqueado", franqueado.NomeRazaoSocial);
        Assert.Equal(email, franqueado.Email);
        Assert.Equal("(11) 99999-9999", franqueado.Telefone);
        Assert.Null(franqueado.NomeFantasia);
        Assert.Null(franqueado.ResponsavelLegal);
    }

    [Fact]
    public async Task Pessoa_juridica_exige_razao_social_e_email_comercial_no_servidor()
    {
        using var application = new UsuariosFranqueadoraWebApplicationFactory();
        var organizacaoId = await application.InicializarAdministradorAsync();
        var unidade = await AdicionarUnidadeAsync(application, organizacaoId, "BFA Empresa");
        using var client = CriarCliente(application);
        await LoginAsync(client, application);

        using var response = await CadastrarFranqueadoAsync(
            client,
            $"empresa-invalida-{Guid.NewGuid():N}@bfa.test",
            [unidade.Id],
            TipoPessoaFranqueado.PessoaJuridica,
            "AB.CDE.F12/3456-78",
            incluirDadosEmpresa: false);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Informe a razão social.", html, StringComparison.Ordinal);
        Assert.Contains("Informe o email comercial.", html, StringComparison.Ordinal);
        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        Assert.Empty(dbContext.Franqueados);
        Assert.Empty(dbContext.Users.Where(usuario => usuario.Email != application.AdministradorEmail));
    }

    [Fact]
    public async Task Unidade_externa_invalida_todo_cadastro_sem_residuo()
    {
        using var application = new UsuariosFranqueadoraWebApplicationFactory();
        var organizacaoId = await application.InicializarAdministradorAsync();
        var unidadeValida = await AdicionarUnidadeAsync(application, organizacaoId, "BFA Atual");
        var unidadeExterna = await AdicionarUnidadeAsync(application, Guid.NewGuid(), "BFA Externa");
        using var client = CriarCliente(application);
        await LoginAsync(client, application);
        var email = $"invalido-{Guid.NewGuid():N}@bfa.test";

        using var response = await CadastrarFranqueadoAsync(
            client,
            email,
            [unidadeValida.Id, unidadeExterna.Id]);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("unidades selecionadas são inválidas", html, StringComparison.Ordinal);
        await AssertSemCadastroParcialAsync(application, email);
    }

    [Fact]
    public async Task Unidade_com_franqueado_ativo_retorna_conflito_sem_residuo()
    {
        using var application = new UsuariosFranqueadoraWebApplicationFactory();
        var organizacaoId = await application.InicializarAdministradorAsync();
        var unidade = await AdicionarUnidadeAsync(application, organizacaoId, "BFA Ocupada");
        await AdicionarFranqueadoAtivoAsync(application, organizacaoId, unidade.Id);
        using var client = CriarCliente(application);
        await LoginAsync(client, application);
        var email = $"conflito-{Guid.NewGuid():N}@bfa.test";

        using var response = await CadastrarFranqueadoAsync(client, email, [unidade.Id]);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(
            "A unidade BFA Ocupada já possui um franqueado ativo.",
            html,
            StringComparison.Ordinal);
        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        Assert.False(await dbContext.Users.AnyAsync(item => item.Email == email));
        Assert.Empty(dbContext.PerfisUsuario);
        Assert.Equal(1, await dbContext.Franqueados.CountAsync());
        Assert.Empty(dbContext.FranqueadosUsuarios);
    }

    [Fact]
    public async Task Primeiro_acesso_valida_token_senha_confirmacao_e_nao_autentica_automaticamente()
    {
        using var application = new UsuariosFranqueadoraWebApplicationFactory();
        await application.InicializarAdministradorAsync();
        using var client = CriarCliente(application);
        await LoginAsync(client, application);
        using var logout = await PostLogoutAsync(client);
        Assert.Equal(HttpStatusCode.Found, logout.StatusCode);
        var email = $"primeiro-acesso-{Guid.NewGuid():N}@bfa.test";
        await LoginAsync(client, application);
        using var cadastro = await CadastrarAdministradorAsync(client, email);
        var link = ExtrairLinkPrimeiroAcesso(
            WebUtility.HtmlDecode(await cadastro.Content.ReadAsStringAsync()));
        using var logoutNovo = await PostLogoutAsync(client);
        Assert.Equal(HttpStatusCode.Found, logoutNovo.StatusCode);

        var uri = new Uri(link);
        using var get = await client.GetAsync(uri.PathAndQuery);
        var html = await get.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        Assert.Contains("Defina sua senha", WebUtility.HtmlDecode(html), StringComparison.Ordinal);
        var tokenAntiforgery = ExtrairAntiforgery(html);
        var query = QueryHelpers.ParseQuery(uri.Query);

        using var divergente = await client.PostAsync(
            "/definir-senha",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["UsuarioId"] = query["usuarioId"].ToString(),
                ["Token"] = query["token"].ToString(),
                ["NovaSenha"] = "Nova.Senha!123",
                ["ConfirmacaoSenha"] = "Outra.Senha!123",
                ["__RequestVerificationToken"] = tokenAntiforgery
            }));
        var htmlDivergente = WebUtility.HtmlDecode(await divergente.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, divergente.StatusCode);
        Assert.Contains("A confirmação deve ser igual", htmlDivergente, StringComparison.Ordinal);

        tokenAntiforgery = ExtrairAntiforgery(htmlDivergente);
        using var fraca = await client.PostAsync(
            "/definir-senha",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["UsuarioId"] = query["usuarioId"].ToString(),
                ["Token"] = query["token"].ToString(),
                ["NovaSenha"] = "abc",
                ["ConfirmacaoSenha"] = "abc",
                ["__RequestVerificationToken"] = tokenAntiforgery
            }));
        var htmlFraca = WebUtility.HtmlDecode(await fraca.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, fraca.StatusCode);
        Assert.Contains("A senha deve", htmlFraca, StringComparison.Ordinal);

        tokenAntiforgery = ExtrairAntiforgery(htmlFraca);
        using var sucesso = await client.PostAsync(
            "/definir-senha",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["UsuarioId"] = query["usuarioId"].ToString(),
                ["Token"] = query["token"].ToString(),
                ["NovaSenha"] = "Nova.Senha!123",
                ["ConfirmacaoSenha"] = "Nova.Senha!123",
                ["__RequestVerificationToken"] = tokenAntiforgery
            }));
        Assert.Equal(HttpStatusCode.Found, sucesso.StatusCode);
        Assert.Equal("/login", sucesso.Headers.Location?.OriginalString);

        using var protegido = await client.GetAsync("/conta/autenticado");
        Assert.Equal(HttpStatusCode.Found, protegido.StatusCode);
        Assert.StartsWith("/login?", protegido.Headers.Location?.PathAndQuery);

        using var reutilizacao = await client.GetAsync(uri.PathAndQuery);
        Assert.Equal(HttpStatusCode.BadRequest, reutilizacao.StatusCode);

        var loginToken = await ObterAntiforgeryAsync(client, "/login");
        using var login = await client.PostAsync(
            "/login",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Email"] = email,
                ["Senha"] = "Nova.Senha!123",
                ["LembrarMe"] = "false",
                ["ReturnUrl"] = "/conta/autenticado",
                ["__RequestVerificationToken"] = loginToken
            }));
        Assert.Equal(HttpStatusCode.Found, login.StatusCode);
        Assert.Equal("/conta/autenticado", login.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Definir_senha_invalido_e_antiforgery_falham_de_forma_controlada()
    {
        using var application = new UsuariosFranqueadoraWebApplicationFactory();
        using var client = CriarCliente(application);

        using var invalido = await client.GetAsync(
            $"/definir-senha?usuarioId={Guid.NewGuid()}&token=nao-valido");
        var html = WebUtility.HtmlDecode(await invalido.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, invalido.StatusCode);
        Assert.Contains("Link inválido", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Exception", html, StringComparison.OrdinalIgnoreCase);

        using var semToken = await client.PostAsync(
            "/definir-senha",
            new FormUrlEncodedContent([]));
        Assert.Equal(HttpStatusCode.BadRequest, semToken.StatusCode);
    }

    private static HttpClient CriarCliente(UsuariosFranqueadoraWebApplicationFactory application)
    {
        return application.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
    }

    private static async Task LoginAsync(
        HttpClient client,
        UsuariosFranqueadoraWebApplicationFactory application)
    {
        var token = await ObterAntiforgeryAsync(client, "/login");
        using var response = await client.PostAsync(
            "/login",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Email"] = application.AdministradorEmail,
                ["Senha"] = application.AdministradorSenha,
                ["LembrarMe"] = "false",
                ["ReturnUrl"] = string.Empty,
                ["__RequestVerificationToken"] = token
            }));
        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
    }

    private static async Task<HttpResponseMessage> CadastrarAdministradorAsync(
        HttpClient client,
        string email)
    {
        var token = await ObterAntiforgeryAsync(client, "/franqueadora/usuarios/novo");
        return await client.PostAsync(
            "/franqueadora/usuarios/novo",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["TipoCadastro"] = nameof(TipoCadastroUsuario.AdministradorRede),
                ["NomeCompleto"] = "Novo Administrador",
                ["Email"] = email,
                ["Telefone"] = "11999999999",
                ["__RequestVerificationToken"] = token
            }));
    }

    private static async Task<HttpResponseMessage> CadastrarFranqueadoAsync(
        HttpClient client,
        string email,
        IReadOnlyCollection<Guid> unidadesIds,
        TipoPessoaFranqueado tipoPessoa = TipoPessoaFranqueado.PessoaFisica,
        string documento = "123.456.789-01",
        bool incluirDadosEmpresa = true,
        IReadOnlyDictionary<string, string>? camposAdicionais = null,
        int? estadoCodigoIbge =
            UsuariosFranqueadoraWebApplicationFactory.EstadoPadraoCodigoIbge,
        int? municipioCodigoIbge =
            UsuariosFranqueadoraWebApplicationFactory.MunicipioPadraoCodigoIbge)
    {
        var token = await ObterAntiforgeryAsync(client, "/franqueadora/usuarios/novo");
        var campos = new List<KeyValuePair<string, string>>
        {
            new("TipoCadastro", nameof(TipoCadastroUsuario.Franqueado)),
            new("NomeCompleto", "Usuário Franqueado"),
            new("Email", email),
            new("Telefone", "(11) 99999-9999"),
            new("TipoPessoa", tipoPessoa.ToString()),
            new("Documento", documento),
            new("Cep", "18000-000"),
            new("__RequestVerificationToken", token)
        };

        if (estadoCodigoIbge is not null)
        {
            campos.Add(new("EstadoCodigoIbge", estadoCodigoIbge.Value.ToString()));
        }

        if (municipioCodigoIbge is not null)
        {
            campos.Add(new("MunicipioCodigoIbge", municipioCodigoIbge.Value.ToString()));
        }

        if (tipoPessoa == TipoPessoaFranqueado.PessoaJuridica && incluirDadosEmpresa)
        {
            campos.Add(new("NomeRazaoSocial", "Empresa Franqueada Ltda."));
            campos.Add(new("NomeFantasia", "BFA Empresa"));
            campos.Add(new("TelefoneFranqueado", "(11) 98888-8888"));
            campos.Add(new("EmailFranqueado", "comercial@bfa.test"));
            campos.Add(new("ResponsavelLegal", "Representante BFA"));
        }

        if (camposAdicionais is not null)
        {
            campos.AddRange(camposAdicionais);
        }

        campos.AddRange(unidadesIds.Select(unidadeId =>
            new KeyValuePair<string, string>("UnidadesIds", unidadeId.ToString())));

        return await client.PostAsync(
            "/franqueadora/usuarios/novo",
            new FormUrlEncodedContent(campos));
    }

    private static async Task<HttpResponseMessage> EditarUsuarioAsync(
        HttpClient client,
        Guid usuarioId,
        string nomeCompleto,
        string email,
        string? telefone,
        string? tokenUri = null)
    {
        var token = await ObterAntiforgeryAsync(
            client,
            tokenUri ?? $"/franqueadora/usuarios/{usuarioId}/editar");
        return await client.PostAsync(
            $"/franqueadora/usuarios/{usuarioId}/editar",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["NomeCompleto"] = nomeCompleto,
                ["Email"] = email,
                ["Telefone"] = telefone ?? string.Empty,
                ["__RequestVerificationToken"] = token
            }));
    }

    private static async Task<HttpResponseMessage> PostLogoutAsync(HttpClient client)
    {
        var token = await ObterAntiforgeryAsync(client, "/franqueadora/usuarios");
        return await client.PostAsync(
            "/logout",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token
            }));
    }

    private static async Task<string> ObterAntiforgeryAsync(
        HttpClient client,
        string requestUri)
    {
        var html = await client.GetStringAsync(requestUri);
        return ExtrairAntiforgery(html);
    }

    private static string ExtrairAntiforgery(string html)
    {
        var match = AntiforgeryToken().Match(html);
        Assert.True(match.Success, "Token antiforgery não encontrado.");
        return WebUtility.HtmlDecode(match.Groups["token"].Value);
    }

    private static string ExtrairLinkPrimeiroAcesso(string html)
    {
        var match = LinkPrimeiroAcesso().Match(html);
        Assert.True(match.Success, "Link de primeiro acesso não encontrado.");
        return WebUtility.HtmlDecode(match.Groups["link"].Value);
    }

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

    private static async Task<Guid> AdicionarUsuarioRelacionadoAsync(
        UsuariosFranqueadoraWebApplicationFactory application,
        Guid organizacaoId,
        Unidade unidade,
        string nome,
        string email)
    {
        var usuarioId = Guid.NewGuid();
        var franqueadoId = Guid.NewGuid();
        var agoraUtc = DateTime.UtcNow;
        await using var scope = application.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<UsuarioIdentity>>();
        var usuario = new UsuarioIdentity { Id = usuarioId, UserName = email, Email = email };
        Assert.True((await userManager.CreateAsync(usuario)).Succeeded);
        var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        dbContext.PerfisUsuario.Add(new PerfilUsuario(
            Guid.NewGuid(), usuarioId, nome, "11999999999", agoraUtc));
        dbContext.VinculosAcesso.AddRange(
            new VinculoAcesso(
                Guid.NewGuid(), usuarioId, organizacaoId, unidade.Id,
                PerfilAcesso.AdministradorUnidade, agoraUtc),
            new VinculoAcesso(
                Guid.NewGuid(), usuarioId, organizacaoId, unidade.Id,
                PerfilAcesso.Professor, agoraUtc));
        dbContext.Franqueados.Add(new Franqueado(
            franqueadoId,
            organizacaoId,
            TipoPessoaFranqueado.PessoaFisica,
            nome,
            "98765432100",
            email,
            agoraUtc));
        dbContext.FranqueadosUsuarios.Add(new FranqueadoUsuario(
            Guid.NewGuid(), franqueadoId, usuarioId, principal: true, agoraUtc));
        dbContext.FranqueadosUnidades.Add(new FranqueadoUnidade(
            Guid.NewGuid(), franqueadoId, organizacaoId, unidade.Id, agoraUtc));
        await dbContext.SaveChangesAsync();
        return usuarioId;
    }

    private static async Task<Guid> AdicionarUsuarioExternoAsync(
        UsuariosFranqueadoraWebApplicationFactory application)
    {
        var usuarioId = Guid.NewGuid();
        var organizacaoId = Guid.NewGuid();
        var agoraUtc = DateTime.UtcNow;
        await using var scope = application.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<UsuarioIdentity>>();
        var usuario = new UsuarioIdentity
        {
            Id = usuarioId,
            UserName = "externo@bfa.test",
            Email = "externo@bfa.test"
        };
        Assert.True((await userManager.CreateAsync(usuario)).Succeeded);
        var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        dbContext.PerfisUsuario.Add(new PerfilUsuario(
            Guid.NewGuid(), usuarioId, "Usuário externo", null, agoraUtc));
        dbContext.VinculosAcesso.Add(new VinculoAcesso(
            Guid.NewGuid(), usuarioId, organizacaoId, null,
            PerfilAcesso.AdministradorRede, agoraUtc));
        await dbContext.SaveChangesAsync();
        return usuarioId;
    }

    private static async Task AdicionarVinculoOutraOrganizacaoAsync(
        UsuariosFranqueadoraWebApplicationFactory application,
        Guid usuarioId)
    {
        var organizacaoId = Guid.NewGuid();
        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        dbContext.VinculosAcesso.Add(new VinculoAcesso(
            Guid.NewGuid(),
            usuarioId,
            organizacaoId,
            unidadeId: null,
            PerfilAcesso.AdministradorRede,
            DateTime.UtcNow));
        await dbContext.SaveChangesAsync();
    }

    private static async Task<Guid> AdicionarUsuarioComRelacaoComercialAsync(
        UsuariosFranqueadoraWebApplicationFactory application,
        Guid organizacaoId,
        Unidade? unidade = null)
    {
        var agoraUtc = DateTime.UtcNow;
        var usuarioId = Guid.NewGuid();
        var franqueadoId = Guid.NewGuid();
        await using var scope = application.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<UsuarioIdentity>>();
        var usuario = new UsuarioIdentity
        {
            Id = usuarioId,
            UserName = "comercial@bfa.test",
            Email = "comercial@bfa.test"
        };
        Assert.True((await userManager.CreateAsync(usuario)).Succeeded);
        var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        dbContext.PerfisUsuario.Add(new PerfilUsuario(
            Guid.NewGuid(),
            usuarioId,
            "Relação Comercial",
            null,
            agoraUtc));
        dbContext.Franqueados.Add(new Franqueado(
            franqueadoId,
            organizacaoId,
            TipoPessoaFranqueado.PessoaFisica,
            "Relação Comercial",
            "45678912300",
            "comercial@bfa.test",
            agoraUtc));
        dbContext.FranqueadosUsuarios.Add(new FranqueadoUsuario(
            Guid.NewGuid(),
            franqueadoId,
            usuarioId,
            principal: true,
            agoraUtc));
        if (unidade is not null)
        {
            dbContext.FranqueadosUnidades.Add(new FranqueadoUnidade(
                Guid.NewGuid(),
                franqueadoId,
                organizacaoId,
                unidade.Id,
                agoraUtc));
        }
        await dbContext.SaveChangesAsync();
        return usuarioId;
    }

    private static async Task<Guid> AdicionarAdministradorUnidadeAsync(
        UsuariosFranqueadoraWebApplicationFactory application,
        Guid organizacaoId,
        Guid unidadeId)
    {
        var usuarioId = Guid.NewGuid();
        var email = $"operacao-{Guid.NewGuid():N}@bfa.test";
        var agoraUtc = DateTime.UtcNow;
        await using var scope = application.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<UsuarioIdentity>>();
        var usuario = new UsuarioIdentity { Id = usuarioId, UserName = email, Email = email };
        Assert.True((await userManager.CreateAsync(usuario)).Succeeded);
        var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        dbContext.PerfisUsuario.Add(new PerfilUsuario(
            Guid.NewGuid(), usuarioId, "Administrador da Unidade", null, agoraUtc));
        dbContext.VinculosAcesso.Add(new VinculoAcesso(
            Guid.NewGuid(),
            usuarioId,
            organizacaoId,
            unidadeId,
            PerfilAcesso.AdministradorUnidade,
            agoraUtc));
        await dbContext.SaveChangesAsync();
        return usuarioId;
    }

    private static async Task AdicionarAcessoUsuarioAsync(
        UsuariosFranqueadoraWebApplicationFactory application,
        Guid usuarioId,
        Guid organizacaoId,
        Guid unidadeId)
    {
        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        dbContext.VinculosAcesso.Add(new VinculoAcesso(
            Guid.NewGuid(),
            usuarioId,
            organizacaoId,
            unidadeId,
            PerfilAcesso.AdministradorUnidade,
            DateTime.UtcNow));
        await dbContext.SaveChangesAsync();
    }

    private static async Task AdicionarFranqueadoAtivoAsync(
        UsuariosFranqueadoraWebApplicationFactory application,
        Guid organizacaoId,
        Guid unidadeId)
    {
        var agoraUtc = DateTime.UtcNow;
        var franqueadoId = Guid.NewGuid();
        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        dbContext.Franqueados.Add(new Franqueado(
            franqueadoId,
            organizacaoId,
            TipoPessoaFranqueado.PessoaJuridica,
            "Franqueado existente",
            "12345678000199",
            "existente@bfa.test",
            agoraUtc));
        dbContext.FranqueadosUnidades.Add(new FranqueadoUnidade(
            Guid.NewGuid(), franqueadoId, organizacaoId, unidadeId, agoraUtc));
        await dbContext.SaveChangesAsync();
    }

    private static async Task AssertSemCadastroParcialAsync(
        UsuariosFranqueadoraWebApplicationFactory application,
        string email)
    {
        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        Assert.False(await dbContext.Users.AnyAsync(item => item.Email == email));
        Assert.Empty(dbContext.PerfisUsuario);
        Assert.Empty(dbContext.Franqueados);
        Assert.Empty(dbContext.FranqueadosUsuarios);
        Assert.Empty(dbContext.FranqueadosUnidades);
    }

    [GeneratedRegex(
        "name=\"__RequestVerificationToken\"[^>]*value=\"(?<token>[^\"]+)\"",
        RegexOptions.CultureInvariant)]
    private static partial Regex AntiforgeryToken();

    [GeneratedRegex(
        "id=\"link-primeiro-acesso\"[^>]*value=\"(?<link>[^\"]+)\"",
        RegexOptions.CultureInvariant)]
    private static partial Regex LinkPrimeiroAcesso();

    [GeneratedRegex(
        "<a class=\"bfa-admin-nav-link[^\"]*\"\\s+href=\"(?<rota>/franqueadora(?:/usuarios|/unidades)?)\"",
        RegexOptions.CultureInvariant)]
    private static partial Regex LinksMenu();
}
