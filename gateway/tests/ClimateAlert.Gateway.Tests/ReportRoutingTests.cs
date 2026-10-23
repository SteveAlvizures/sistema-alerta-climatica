using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ClimateAlert.Gateway.Tests;

public sealed class ReportRoutingTests(GatewayFixture fixture) : IClassFixture<GatewayFixture>
{
    [Theory]
    [InlineData("/api/sensor-readings/latest?page=1&pageSize=50", 200)]
    [InlineData("/api/sensor-readings/latest?page=0&pageSize=50", 400)]
    public async Task AdditiveLatestReadingBatchContractIsAccessible(string path, int expected)
    {
        using var response = await fixture.Client.GetAsync(path);
        Assert.Equal(expected, (int)response.StatusCode);
    }

    [Theory]
    [InlineData(null, 401)]
    [InlineData("invalid", 401)]
    [InlineData("reader", 403)]
    [InlineData("admin", 200)]
    public async Task ReportRoutePreservesUpstreamAuthorization(string? identity, int expected)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/reports/transport/200");
        if (identity is not null) request.Headers.Authorization = new("Bearer",
            identity == "admin" ? fixture.AdminToken : identity == "reader" ? fixture.UserToken : "invalid");
        using var response = await fixture.Client.SendAsync(request);
        Assert.Equal(expected, (int)response.StatusCode);
    }

    [Theory]
    [InlineData(404)]
    [InlineData(503)]
    public async Task ReportRoutePreservesControlledErrorsAndCorrelation(int status)
    {
        string id = Guid.NewGuid().ToString();
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/reports/transport/{status}");
        request.Headers.Authorization = new("Bearer", fixture.AdminToken);
        request.Headers.Add("X-Correlation-ID", id);
        request.Headers.Add("X-User-Id", "spoofed");
        using var response = await fixture.Client.SendAsync(request);
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal(id, response.Headers.GetValues("X-Correlation-ID").Single());
        Assert.False(response.Headers.Contains("X-Internal-Host"));
        Assert.False(response.Headers.Contains("X-Powered-By"));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(id, body.GetProperty("correlationId").GetString());
        Assert.False(body.GetProperty("spoofed").GetBoolean());
    }

    [Fact]
    public async Task UnavailableReportDestinationReturnsSafe502()
    {
        using var client = await fixture.AlternativeGatewayAsync("http://127.0.0.1:1/");
        using var response = await client.GetAsync("/api/reports/types");
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("127.0.0.1", body);
        Assert.Contains("correlationId", body);
    }
}
