using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace GoodPlays.Tests;

public class HealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public HealthEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Redis:ConnectionString", "");
            builder.UseSetting("ConnectionStrings:Default", "Host=localhost;Port=5432;Database=goodplays_test;Username=goodplays;Password=goodplays");
        }).CreateClient();
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsOkOrServiceUnavailable()
    {
        var response = await _client.GetAsync("/health");

        Assert.True(
            response.StatusCode is HttpStatusCode.OK or HttpStatusCode.ServiceUnavailable,
            $"Unexpected status: {response.StatusCode}");
    }
}
