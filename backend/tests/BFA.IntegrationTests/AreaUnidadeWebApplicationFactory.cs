using BFA.Application.Acessos;
using BFA.Application.Unidades;
using BFA.Infrastructure.Acessos;
using BFA.Infrastructure.Persistence;
using BFA.Infrastructure.Unidades;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BFA.IntegrationTests;

public sealed class AreaUnidadeWebApplicationFactory : LoginWebApplicationFactory
{
    private readonly string _databaseName = $"bfa-area-unidade-{Guid.NewGuid():N}";

    public string DiretorioArmazenamento { get; } = Path.Combine(
        Path.GetTempPath(),
        "bfa-area-unidade-documentos",
        Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting(
            "Armazenamento:Documentos:DiretorioBase",
            DiretorioArmazenamento);
        builder.UseSetting(
            "Armazenamento:Documentos:TamanhoMaximoBytes",
            (20 * 1024 * 1024).ToString());
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IDbContextOptionsConfiguration<BfaDbContext>>();
            services.RemoveAll<DbContextOptions<BfaDbContext>>();
            services.RemoveAll<BfaDbContext>();
            services.AddDbContext<BfaDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName)
                    .ConfigureWarnings(warnings => warnings.Ignore(
                        InMemoryEventId.TransactionIgnoredWarning)));

            services.RemoveAll<IAcessoUsuarioConsulta>();
            services.AddScoped<IAcessoUsuarioConsulta, AcessoUsuarioConsulta>();

            services.RemoveAll<IUnidadesUsuarioConsulta>();
            services.AddScoped<IUnidadesUsuarioConsulta>(serviceProvider =>
                serviceProvider.GetRequiredService<UnidadesUsuarioConsulta>());
        });
    }
}
