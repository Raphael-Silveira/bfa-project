using BFA.Application.Acessos;
using BFA.Application.Unidades;
using BFA.Infrastructure.Acessos;
using BFA.Infrastructure.Persistence;
using BFA.Infrastructure.Unidades;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BFA.IntegrationTests;

public sealed class AreaUnidadeWebApplicationFactory : LoginWebApplicationFactory
{
    private readonly string _databaseName = $"bfa-area-unidade-{Guid.NewGuid():N}";

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

            services.RemoveAll<IAcessoUsuarioConsulta>();
            services.AddScoped<IAcessoUsuarioConsulta, AcessoUsuarioConsulta>();

            services.RemoveAll<IUnidadesUsuarioConsulta>();
            services.AddScoped<IUnidadesUsuarioConsulta>(serviceProvider =>
                serviceProvider.GetRequiredService<UnidadesUsuarioConsulta>());
        });
    }
}
