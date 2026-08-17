using System.Net;
using System.Net.Http.Json;
using BFA.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BFA.IntegrationTests;

public sealed class DatabaseHealthEndpointTests
{
    [Fact]
    public async Task Get_returns_healthy_when_database_is_available()
    {
        using var application = CreateApplication(canConnect: true);
        using var client = application.CreateClient();

        using var response = await client.GetAsync("/api/v1/health/database");
        var payload = await response.Content.ReadFromJsonAsync<HealthResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal("healthy", payload.Status);
    }

    [Fact]
    public async Task Get_returns_generic_unhealthy_response_when_database_is_unavailable()
    {
        using var application = CreateApplication(canConnect: false);
        using var client = application.CreateClient();

        using var response = await client.GetAsync("/api/v1/health/database");
        var payload = await response.Content.ReadFromJsonAsync<HealthResponse>();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal("unhealthy", payload.Status);
    }

    [Fact]
    public async Task Existing_health_endpoint_remains_healthy_when_database_is_unavailable()
    {
        using var application = CreateApplication(canConnect: false);
        using var client = application.CreateClient();

        using var response = await client.GetAsync("/api/v1/health");
        var payload = await response.Content.ReadFromJsonAsync<HealthResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal("healthy", payload.Status);
    }

    [Fact]
    public async Task Get_returns_generic_unhealthy_response_when_configuration_is_missing()
    {
        using var application = new BfaWebApplicationFactory().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:BfaDatabase"] = string.Empty
                });
            });
        });
        using var client = application.CreateClient();

        using var response = await client.GetAsync("/api/v1/health/database");
        var payload = await response.Content.ReadFromJsonAsync<HealthResponse>();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal("unhealthy", payload.Status);
    }

    private static WebApplicationFactory<Program> CreateApplication(bool canConnect)
    {
        return new BfaWebApplicationFactory().WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IDatabaseConnectionProbe>();
                services.AddScoped<IDatabaseConnectionProbe>(
                    _ => new StubDatabaseConnectionProbe(canConnect));
            });
        });
    }

    private sealed class StubDatabaseConnectionProbe(bool canConnect)
        : IDatabaseConnectionProbe
    {
        public Task<bool> CanConnectAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(canConnect);
        }
    }

    private sealed record HealthResponse(string Status);
}
