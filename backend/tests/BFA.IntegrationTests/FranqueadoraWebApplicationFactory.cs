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

    public TestFranqueadoraDashboardConsulta Dashboard =>
        Services.GetRequiredService<TestFranqueadoraDashboardConsulta>();

    public TestFranqueadoraAlunosConsulta Alunos =>
        Services.GetRequiredService<TestFranqueadoraAlunosConsulta>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IAcessoUsuarioConsulta>();
            services.RemoveAll<IPainelFranqueadoraConsulta>();
            services.RemoveAll<IFranqueadoraDashboardConsulta>();
            services.RemoveAll<IFranqueadoraAlunosConsulta>();
            services.AddSingleton<TestAcessoUsuarioConsulta>();
            services.AddSingleton<IAcessoUsuarioConsulta>(serviceProvider =>
                serviceProvider.GetRequiredService<TestAcessoUsuarioConsulta>());
            services.AddSingleton<TestPainelFranqueadoraConsulta>();
            services.AddSingleton<IPainelFranqueadoraConsulta>(serviceProvider =>
                serviceProvider.GetRequiredService<TestPainelFranqueadoraConsulta>());
            services.AddSingleton<TestFranqueadoraDashboardConsulta>();
            services.AddSingleton<IFranqueadoraDashboardConsulta>(serviceProvider =>
                serviceProvider.GetRequiredService<TestFranqueadoraDashboardConsulta>());
            services.AddSingleton<TestFranqueadoraAlunosConsulta>();
            services.AddSingleton<IFranqueadoraAlunosConsulta>(serviceProvider =>
                serviceProvider.GetRequiredService<TestFranqueadoraAlunosConsulta>());
        });
    }
}

public sealed class TestFranqueadoraAlunosConsulta : IFranqueadoraAlunosConsulta
{
    public Task<FranqueadoraAlunosResultado> ListarAsync(
        Guid usuarioId,
        Guid? unidadeId,
        string? busca,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(FranqueadoraAlunosResultado.Sucesso(
            new FranqueadoraAlunosResumo(
                Guid.NewGuid(),
                "Brazilian Footvolley Academy",
                0,
                [],
                [])));
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

public sealed class TestFranqueadoraDashboardConsulta : IFranqueadoraDashboardConsulta
{
    public FranqueadoraDashboardResultado Resultado { get; set; } =
        FranqueadoraDashboardResultado.SemAcesso();

    public Task<FranqueadoraDashboardResultado> ObterAsync(
        Guid usuarioId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Resultado);
    }
}
