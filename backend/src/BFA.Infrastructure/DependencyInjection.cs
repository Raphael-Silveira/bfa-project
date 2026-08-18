using BFA.Application.Acessos;
using BFA.Application.Bootstrap;
using BFA.Application.Franqueadora;
using BFA.Application.Franqueadora.Unidades;
using BFA.Infrastructure.Acessos;
using BFA.Infrastructure.Bootstrap;
using BFA.Infrastructure.Franqueadora;
using BFA.Infrastructure.Identity;
using BFA.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BFA.Infrastructure;

public static class DependencyInjection
{
    private const string DatabaseConnectionName = "BfaDatabase";

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddDbContext<BfaDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString(DatabaseConnectionName);

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                options.UseNpgsql();
                return;
            }

            options.UseNpgsql(connectionString);
        });

        services.AddIdentityCore<UsuarioIdentity>(options =>
        {
            options.Stores.MaxLengthForKeys = 128;
            options.Stores.SchemaVersion = IdentitySchemaVersions.Version2;
        })
            .AddEntityFrameworkStores<BfaDbContext>()
            .AddSignInManager();

        services.AddAuthentication(IdentityConstants.ApplicationScheme)
            .AddIdentityCookies();

        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = "BFA.Auth";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.LoginPath = "/login";
            options.AccessDeniedPath = "/acesso-negado";
            options.SlidingExpiration = true;
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
        });

        services.AddScoped<IDatabaseConnectionProbe, DatabaseConnectionProbe>();
        services.AddScoped<IAcessoUsuarioConsulta, AcessoUsuarioConsulta>();
        services.AddScoped<IBootstrapInicial, BootstrapInicial>();
        services.AddScoped<IPainelFranqueadoraConsulta, PainelFranqueadoraConsulta>();
        services.AddScoped<IUnidadesFranqueadoraRepositorio, UnidadesFranqueadoraRepositorio>();
        services.AddScoped<UnidadesFranqueadoraServico>();
        services.AddScoped<IUnidadesFranqueadoraConsulta>(serviceProvider =>
            serviceProvider.GetRequiredService<UnidadesFranqueadoraServico>());
        services.AddScoped<IUnidadesFranqueadoraServico>(serviceProvider =>
            serviceProvider.GetRequiredService<UnidadesFranqueadoraServico>());
        services.AddSingleton(TimeProvider.System);

        return services;
    }
}
