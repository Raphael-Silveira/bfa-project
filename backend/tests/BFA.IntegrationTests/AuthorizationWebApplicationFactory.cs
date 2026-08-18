using BFA.Application.Acessos;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BFA.IntegrationTests;

public sealed class AuthorizationWebApplicationFactory : LoginWebApplicationFactory
{
    public TestAcessoUsuarioConsulta Acessos =>
        Services.GetRequiredService<TestAcessoUsuarioConsulta>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IAcessoUsuarioConsulta>();
            services.AddSingleton<TestAcessoUsuarioConsulta>();
            services.AddSingleton<IAcessoUsuarioConsulta>(serviceProvider =>
                serviceProvider.GetRequiredService<TestAcessoUsuarioConsulta>());
        });
    }
}
