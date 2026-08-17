using System.Net;
using System.Net.Http.Json;

namespace BFA.IntegrationTests;

public sealed class HealthEndpointTests : IClassFixture<BfaWebApplicationFactory>
{
    private readonly HttpClient _client;

    public HealthEndpointTests(BfaWebApplicationFactory application)
    {
        _client = application.CreateClient();
    }

    [Fact]
    public async Task Get_returns_healthy_status()
    {
        using var response = await _client.GetAsync("/api/v1/health");
        var payload = await response.Content.ReadFromJsonAsync<HealthResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal("healthy", payload.Status);
    }

    private sealed record HealthResponse(string Status);
}
