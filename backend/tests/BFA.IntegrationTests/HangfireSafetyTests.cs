using Microsoft.Extensions.DependencyInjection;
using Hangfire;

namespace BFA.IntegrationTests;

public sealed class HangfireSafetyTests
{
    [Fact]
    public void Host_de_testes_com_Hangfire_desabilitado_nao_registra_processamento()
    {
        using var application = new BfaWebApplicationFactory();

        Assert.Null(application.Services.GetService<IRecurringJobManager>());
    }
}
