using System.Net;
using BFA.Application.Bootstrap;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BFA.IntegrationTests;

public sealed class StartupNormalSemBootstrapTests
{
    [Fact]
    public async Task Startup_normal_nao_executa_bootstrap()
    {
        using var application = new StartupNormalWebApplicationFactory();
        using var client = application.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

        using var response = await client.GetAsync("/api/v1/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, application.BootstrapInicial.Execucoes);
    }

    private sealed class StartupNormalWebApplicationFactory : BfaWebApplicationFactory
    {
        public TestBootstrapInicial BootstrapInicial =>
            Services.GetRequiredService<TestBootstrapInicial>();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IBootstrapInicial>();
                services.AddSingleton<TestBootstrapInicial>();
                services.AddSingleton<IBootstrapInicial>(serviceProvider =>
                    serviceProvider.GetRequiredService<TestBootstrapInicial>());
            });
        }
    }
}
