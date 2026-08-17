using BFA.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace BFA.IntegrationTests;

public sealed class InfrastructureRegistrationTests : IClassFixture<BfaWebApplicationFactory>
{
    private readonly BfaWebApplicationFactory _application;

    public InfrastructureRegistrationTests(BfaWebApplicationFactory application)
    {
        _application = application;
    }

    [Fact]
    public void BfaDbContext_uses_npgsql_provider()
    {
        using var scope = _application.Services.CreateScope();
        using var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();

        Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", dbContext.Database.ProviderName);
    }

    [Fact]
    public void Database_connection_probe_is_registered()
    {
        using var scope = _application.Services.CreateScope();

        var probe = scope.ServiceProvider.GetService<IDatabaseConnectionProbe>();

        Assert.NotNull(probe);
    }
}
