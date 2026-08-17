using BFA.Infrastructure.Persistence;
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
                throw new InvalidOperationException(
                    $"Connection string '{DatabaseConnectionName}' was not configured.");
            }

            options.UseNpgsql(connectionString);
        });

        return services;
    }
}
