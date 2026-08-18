using BFA.Application.Franqueadora;
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
