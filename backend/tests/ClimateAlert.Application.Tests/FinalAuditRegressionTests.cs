using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using ClimateAlert.Api.Authentication;
using ClimateAlert.Application.Features.SensorReadings;
using ClimateAlert.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace ClimateAlert.Application.Tests;

public sealed class FinalAuditRegressionTests
{
    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("invalid")]
    public void InvalidJwtExpirationFailsFast(string minutes)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["JWT_KEY"] = "academic-test-signing-key-more-than-32-characters", ["JWT_EXPIRATION_MINUTES"] = minutes }).Build();
        Assert.Throws<InvalidOperationException>(() => JwtOptions.FromConfiguration(configuration));
    }

    [Theory]
    [InlineData(null, 60)]
    [InlineData("15", 15)]
    public void ValidJwtExpirationRemainsConfigurable(string? minutes, int expected)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["JWT_KEY"] = "academic-test-signing-key-more-than-32-characters", ["JWT_EXPIRATION_MINUTES"] = minutes }).Build();
        Assert.Equal(expected, JwtOptions.FromConfiguration(configuration).ExpirationMinutes);
    }

    [Theory]
    [InlineData(-10, 401)]
    [InlineData(300, 200)]
    public async Task ActualJwtMiddlewareRejectsRecentlyExpiredToken(int seconds, int expected)
    {
        await using var host = await AlertLifecycleHttpTests.TestHost.StartAsync();
        Guid actor = await host.LoginAsAsync("Operator");
        var options = new JwtOptions("academic-test-signing-key-more-than-32-characters", "test", "test", 60);
        var token = new JwtSecurityToken("test", "test", [new Claim(JwtRegisteredClaimNames.Sub, actor.ToString()),
            new Claim(ClaimTypes.Role, "Operator")], DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddSeconds(seconds),
            new SigningCredentials(new SymmetricSecurityKey(options.SigningKey), SecurityAlgorithms.HmacSha256));
        host.Client.DefaultRequestHeaders.Authorization = new("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        Assert.Equal(expected, (int)(await host.Client.GetAsync("/api/events")).StatusCode);
    }

    [Theory]
    [InlineData("172.30.42.10", "https")]
    [InlineData("198.51.100.10", "http")]
    public async Task ForwardedProtoIsAcceptedOnlyFromTrustedProxy(string remote, string expected)
    {
        var configuration = new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedProto, ForwardLimit = 1 };
        configuration.KnownIPNetworks.Add(System.Net.IPNetwork.Parse("172.30.42.0/24"));
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse(remote);
        context.Request.Scheme = "http";
        context.Request.Headers["X-Forwarded-Proto"] = "https";
        var middleware = new ForwardedHeadersMiddleware(_ => Task.CompletedTask, NullLoggerFactory.Instance, Options.Create(configuration));
        await middleware.Invoke(context);
        Assert.Equal(expected, context.Request.Scheme);
    }

    [Fact]
    public async Task ReadingSnapshotSurvivesDeactivationAndLegacyStateIsNotInvented()
    {
        await using var host = await AlertLifecycleHttpTests.TestHost.StartAsync();
        await host.LoginAsAsync("Operator");
        var response = await host.Client.PostAsJsonAsync("/api/sensor-readings", new { sensorId = host.SensorId, value = 15 });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await using (var db = host.Database())
        {
            var legacy = (await db.SensorReadings.FindAsync(host.ReadingId))!;
            db.Entry(legacy).Property(r => r.SensorStatusAtMeasurement).CurrentValue = null;
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.OK, (await host.Client.PatchAsJsonAsync($"/api/sensors/{host.SensorId}/status", new { isActive = false })).StatusCode);
        var history = (await host.GetAsync<PagedResponse<SensorReadingResponse>>($"/api/sensors/{host.SensorId}/readings"))!;
        Assert.Equal(SensorStatus.Active, history.Data.Single(r => r.Id != host.ReadingId).SensorStatusAtMeasurement);
        Assert.Null(history.Data.Single(r => r.Id == host.ReadingId).SensorStatusAtMeasurement);
        var global = (await host.GetAsync<PagedResponse<HistoryReadingResponse>>($"/api/sensor-readings?sensorId={host.SensorId}"))!;
        Assert.Equal(SensorStatus.Active, global.Data.Single(r => r.Id != host.ReadingId).SensorStatusAtMeasurement);
        Assert.Null(global.Data.Single(r => r.Id == host.ReadingId).SensorStatusAtMeasurement);
    }
}
