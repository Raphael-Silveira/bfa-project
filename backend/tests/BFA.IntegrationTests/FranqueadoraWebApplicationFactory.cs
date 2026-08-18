using BFA.Application.Acessos;
using BFA.Application.Franqueadora;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BFA.IntegrationTests;

public class FranqueadoraWebApplicationFactory : LoginWebApplicationFactory
{
    public TestAcessoUsuarioConsulta Acessos =>
        Services.GetRequiredService<TestAcessoUsuarioConsulta>();

    public TestPainelFranqueadoraConsulta Painel =>
        Services.GetRequiredService<TestPainelFranqueadoraConsulta>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IAcessoUsuarioConsulta>();
            services.RemoveAll<IPainelFranqueadoraConsulta>();
            services.AddSingleton<TestAcessoUsuarioConsulta>();
            services.AddSingleton<IAcessoUsuarioConsulta>(serviceProvider =>
                serviceProvider.GetRequiredService<TestAcessoUsuarioConsulta>());
            services.AddSingleton<TestPainelFranqueadoraConsulta>();
            services.AddSingleton<IPainelFranqueadoraConsulta>(serviceProvider =>
                serviceProvider.GetRequiredService<TestPainelFranqueadoraConsulta>());
        });
    }
}

public sealed class TestPainelFranqueadoraConsulta : IPainelFranqueadoraConsulta
{
    public PainelFranqueadoraResultado Resultado { get; set; } =
        PainelFranqueadoraResultado.SemAcesso();

    public Guid? UltimoUsuarioId { get; private set; }

    public Task<PainelFranqueadoraResultado> ObterAsync(
        Guid usuarioId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        UltimoUsuarioId = usuarioId;
        return Task.FromResult(Resultado);
    }
}
