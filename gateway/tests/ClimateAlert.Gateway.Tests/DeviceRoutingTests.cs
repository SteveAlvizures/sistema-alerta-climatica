using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ClimateAlert.Gateway.Tests;

public sealed class DeviceRoutingTests
{
    [Theory]
    [InlineData("Development", null, null, 201)]
    [InlineData("Production", "127.0.0.1", "https", 201)]
    [InlineData("Production", "192.0.2.1", "https", 400)]
    [InlineData("Production", "127.0.0.1", "http", 400)]
    [InlineData("Production", "127.0.0.1", "https, http", 400)]
    public async Task DeviceRouteUsesSensorClusterAndStripsForgedInternalIdentity(string environment, string? trustedProxy,
        string? forwardedProto, int expected)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        await using var sensor = builder.Build();
        const string payload = "{\"sensorId\":\"11111111-1111-1111-1111-111111111111\",\"value\":24.5,\"messageId\":\"22222222-2222-2222-2222-222222222222\"}";
        sensor.MapPost("/api/device-readings", async Task<IResult> (HttpContext context) => Results.Json(new
        {
            body = await new StreamReader(context.Request.Body).ReadToEndAsync(),
            correlation = context.Request.Headers["X-Correlation-ID"].ToString(),
            keyPresent = context.Request.Headers["X-Device-Key"] == "isolated-routing-test-key",
            forged = context.Request.Headers.Keys.Any(k => k.StartsWith("X-Internal", StringComparison.OrdinalIgnoreCase)
                || k.StartsWith("X-User", StringComparison.OrdinalIgnoreCase) || k.StartsWith("X-Role", StringComparison.OrdinalIgnoreCase)),
        }, statusCode: 201));
        await sensor.StartAsync();
        await using var gateway = GatewayApplication.Build(["--environment=" + environment], b =>
        {
            b.Logging.ClearProviders();
            b.WebHost.UseUrls("http://127.0.0.1:0");
            b.Configuration.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "GatewaySettings.json"));
            b.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JWT_KEY"] = GatewayFixture.Key,
                ["ReverseProxy:Clusters:sensor:Destinations:primary:Address"] = sensor.Urls.Single() + "/"
            });
            if (trustedProxy is not null)
                b.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Gateway:TrustedProxyAddresses:0"] = trustedProxy });
        });
        await gateway.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(gateway.Urls.Single()) };
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/device-readings")
            { Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json") };
        var correlation = Guid.NewGuid().ToString();
        request.Headers.Add("X-Correlation-ID", correlation);
        if (forwardedProto is not null) request.Headers.Add("X-Forwarded-Proto", forwardedProto);
        request.Headers.Add("X-Device-Key", "isolated-routing-test-key");
        request.Headers.Add("X-Internal-Service-Key", "forged");
        request.Headers.Add("X-User-Id", "forged");
        request.Headers.Add("X-Role", "Administrator");
        var response = await client.SendAsync(request);
        Assert.Equal(expected, (int)response.StatusCode);
        if (expected != 201) return;
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.True(body.GetProperty("keyPresent").GetBoolean());
        Assert.False(body.GetProperty("forged").GetBoolean());
        Assert.Equal(payload, body.GetProperty("body").GetString());
        Assert.Equal(correlation, body.GetProperty("correlation").GetString());
        Assert.Equal(correlation, response.Headers.GetValues("X-Correlation-ID").Single());
    }

    [Theory]
    [InlineData("/api/device-readings")]
    [InlineData("/api/device-credentials")]
    public async Task ProductionRejectsPlaintextEvenWithForgedForwardedProto(string path)
    {
        await using var gateway = GatewayApplication.Build(["--environment=Production"], b =>
        {
            b.Logging.ClearProviders();
            b.WebHost.UseUrls("http://127.0.0.1:0");
            b.Configuration.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "GatewaySettings.json"));
            b.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["JWT_KEY"] = GatewayFixture.Key });
        });
        await gateway.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(gateway.Urls.Single()) };
        client.DefaultRequestHeaders.Add("X-Forwarded-Proto", "https");
        var response = await client.PostAsync(path, null);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("HTTPS", await response.Content.ReadAsStringAsync());
    }
}
