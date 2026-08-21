using BFA.Application.Acessos;
using BFA.Application.Franqueadora;
using BFA.Application.Franqueadora.AcessosUnidade;
using BFA.Application.Franqueadora.Franqueados;
using BFA.Application.Franqueadora.Unidades;
using BFA.Application.Franqueadora.Usuarios;
using BFA.Application.Identidade;
using BFA.Application.Localidades;
using BFA.Application.Unidades;
using BFA.Application.Usuarios;
using BFA.Infrastructure.Identity;
using BFA.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BFA.IntegrationTests;

public sealed class InfrastructureRegistrationTests : IClassFixture<BfaWebApplicationFactory>
{
    private readonly BfaWebApplicationFactory _application;

    public InfrastructureRegistrationTests(BfaWebApplicationFactory application)
    {
        _application = application;
    }

    [Fact]
    public void BfaDbContext_uses_npgsql_provider()
    {
        using var scope = _application.Services.CreateScope();
        using var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();

        Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", dbContext.Database.ProviderName);
    }

    [Fact]
    public void Database_connection_probe_is_registered()
    {
        using var scope = _application.Services.CreateScope();

        var probe = scope.ServiceProvider.GetService<IDatabaseConnectionProbe>();

        Assert.NotNull(probe);
    }

    [Fact]
    public void Painel_franqueadora_consulta_is_registered()
    {
        using var scope = _application.Services.CreateScope();

        var consulta = scope.ServiceProvider.GetService<IPainelFranqueadoraConsulta>();

        Assert.NotNull(consulta);
    }

    [Fact]
    public void Gerenciamento_unidades_franqueadora_is_registered()
    {
        using var scope = _application.Services.CreateScope();

        var consulta = scope.ServiceProvider.GetService<IUnidadesFranqueadoraConsulta>();
        var servico = scope.ServiceProvider.GetService<IUnidadesFranqueadoraServico>();
        var repositorio = scope.ServiceProvider
            .GetService<IUnidadesFranqueadoraRepositorio>();

        Assert.NotNull(consulta);
        Assert.NotNull(servico);
        Assert.NotNull(repositorio);
        Assert.Same(consulta, servico);
    }

    [Fact]
    public void Gerenciamento_acessos_unidade_is_registered()
    {
        using var scope = _application.Services.CreateScope();

        var consulta = scope.ServiceProvider.GetService<IAcessosUnidadeConsulta>();
        var servico = scope.ServiceProvider.GetService<IAcessosUnidadeServico>();
        var repositorio = scope.ServiceProvider.GetService<IAcessosUnidadeRepositorio>();
        var usuarioPorEmail = scope.ServiceProvider.GetService<IUsuarioPorEmailConsulta>();

        Assert.NotNull(consulta);
        Assert.NotNull(servico);
        Assert.NotNull(repositorio);
        Assert.NotNull(usuarioPorEmail);
        Assert.Same(consulta, servico);
    }

    [Fact]
    public void Gerenciamento_usuarios_e_primeiro_acesso_sao_registrados()
    {
        using var scope = _application.Services.CreateScope();

        var consulta = scope.ServiceProvider.GetService<IUsuariosFranqueadoraConsulta>();
        var servico = scope.ServiceProvider.GetService<IUsuariosFranqueadoraServico>();
        var repositorio = scope.ServiceProvider.GetService<IUsuariosFranqueadoraRepositorio>();
        var primeiroAcesso = scope.ServiceProvider.GetService<IPrimeiroAcessoServico>();

        Assert.NotNull(consulta);
        Assert.NotNull(servico);
        Assert.NotNull(repositorio);
        Assert.NotNull(primeiroAcesso);
        Assert.Same(consulta, servico);
    }

    [Fact]
    public void Gerenciamento_franqueados_e_registrado()
    {
        using var scope = _application.Services.CreateScope();

        var consulta = scope.ServiceProvider.GetService<IFranqueadosConsulta>();
        var servico = scope.ServiceProvider.GetService<IFranqueadosServico>();
        var repositorio = scope.ServiceProvider.GetService<IFranqueadosRepositorio>();

        Assert.NotNull(consulta);
        Assert.NotNull(servico);
        Assert.NotNull(repositorio);
        Assert.Same(consulta, servico);
    }

    [Fact]
    public void Catalogo_localidades_possui_cliente_consulta_repositorio_e_servico()
    {
        using var scope = _application.Services.CreateScope();

        var client = scope.ServiceProvider.GetService<IIbgeLocalidadesClient>();
        var consulta = scope.ServiceProvider.GetService<ILocalidadesConsulta>();
        var repositorio = scope.ServiceProvider
            .GetService<ILocalidadesSincronizacaoRepositorio>();
        var servico = scope.ServiceProvider
            .GetService<ILocalidadesSincronizacaoServico>();

        Assert.NotNull(client);
        Assert.NotNull(consulta);
        Assert.NotNull(repositorio);
        Assert.NotNull(servico);
    }

    [Fact]
    public void Consultas_de_unidade_do_usuario_compartilham_a_mesma_implementacao()
    {
        using var scope = _application.Services.CreateScope();

        var unidadesUsuario = scope.ServiceProvider.GetService<IUnidadesUsuarioConsulta>();
        var contextoUnidade = scope.ServiceProvider.GetService<IUnidadeContextoConsulta>();

        Assert.NotNull(unidadesUsuario);
        Assert.NotNull(contextoUnidade);
        Assert.Same(unidadesUsuario, contextoUnidade);
    }

    [Fact]
    public void Consulta_de_apresentacao_do_usuario_esta_registrada()
    {
        using var scope = _application.Services.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetService<IUsuarioApresentacaoConsulta>());
    }

    [Fact]
    public void Identity_user_store_is_registered_without_role_services()
    {
        using var scope = _application.Services.CreateScope();

        var userStore = scope.ServiceProvider.GetService<IUserStore<UsuarioIdentity>>();
        var userManager = scope.ServiceProvider.GetService<UserManager<UsuarioIdentity>>();
        var signInManager = scope.ServiceProvider.GetService<SignInManager<UsuarioIdentity>>();
        var roleStore = scope.ServiceProvider.GetService<IRoleStore<IdentityRole<Guid>>>();
        var roleManager = scope.ServiceProvider.GetService<RoleManager<IdentityRole<Guid>>>();
        var identityOptions = scope.ServiceProvider.GetRequiredService<IOptions<IdentityOptions>>().Value;

        Assert.NotNull(userStore);
        Assert.NotNull(userManager);
        Assert.NotNull(signInManager);
        Assert.Null(roleStore);
        Assert.Null(roleManager);
        Assert.Equal(128, identityOptions.Stores.MaxLengthForKeys);
        Assert.Equal(IdentitySchemaVersions.Version2, identityOptions.Stores.SchemaVersion);
    }
}
