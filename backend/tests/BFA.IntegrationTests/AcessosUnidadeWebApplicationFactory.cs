using BFA.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BFA.IntegrationTests;

public sealed class AcessosUnidadeWebApplicationFactory
    : FranqueadoraWebApplicationFactory
{
    private readonly string _databaseName = $"bfa-acessos-unidade-web-{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IDbContextOptionsConfiguration<BfaDbContext>>();
            services.RemoveAll<DbContextOptions<BfaDbContext>>();
            services.RemoveAll<BfaDbContext>();
            services.AddDbContext<BfaDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));
        });
    }
}
