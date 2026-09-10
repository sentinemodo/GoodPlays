using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;

namespace GoodPlays.Tests;

public class JsonEnumBindingTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public JsonEnumBindingTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Redis:ConnectionString", "");
            builder.UseSetting("ConnectionStrings:Default",
                "Host=localhost;Port=5432;Database=goodplays_test;Username=goodplays;Password=goodplays");
        }).CreateClient();
    }

    [Fact]
    public async Task CreateImport_AcceptsStringModality()
    {
        using var content = new StringContent(
            """{"modality":"Text","text":"Hades"}""",
            Encoding.UTF8,
            "application/json");

        var response = await _client.PostAsync("/api/v1/imports", content);

        Assert.NotEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateLibraryEntry_AcceptsStringStatus()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/library",
            new { gameId = Guid.Parse("14e11cba-e0f4-4da8-a1e6-f6f701071d3d"), status = "Owned" });

        Assert.NotEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
