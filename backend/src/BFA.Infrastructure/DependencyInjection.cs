using BFA.Infrastructure.Identity;
using BFA.Infrastructure.Persistence;
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
            .AddEntityFrameworkStores<BfaDbContext>();

        services.AddScoped<IDatabaseConnectionProbe, DatabaseConnectionProbe>();

        return services;
    }
}
