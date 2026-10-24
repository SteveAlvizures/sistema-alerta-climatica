using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;

namespace ClimateAlert.Gateway.Tests;

public sealed class GatewayIntegrationTests(
    GatewayFixture fixture)
    : IClassFixture<GatewayFixture>
{
    [Theory]
    [InlineData("/health")]
    [InlineData("/health/live")]
    public async Task
        HealthReturnsSafeServiceStatus(
            string path)
    {
        using var response =
            await fixture.Client.GetAsync(path);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        var body =
            await response.Content
                .ReadFromJsonAsync<JsonElement>();

        Assert.Equal(
            "Healthy",
            body.GetProperty("status")
                .GetString());

        Assert.Equal(
            "ClimateAlert.Gateway",
            body.GetProperty("service")
                .GetString());
    }

    [Fact]
    public async Task
        PublicRoutePreservesJsonAndQuery()
    {
        string route =
            "/api/communities?page=1&pageSize=10&search=Gateway";

        Assert.Equal(
            await fixture.Direct
                .GetStringAsync(route),
            await fixture.Client
                .GetStringAsync(route));
    }

    [Fact]
    public async Task
        LoginThroughGatewayUsesExistingContract()
    {
        string token =
            await fixture.LoginAsync("admin");

        Assert.True(
            new JwtSecurityTokenHandler()
                .CanReadToken(token));
    }

    [Theory]
    [InlineData(null, 401)]
    [InlineData("invalid", 401)]
    [InlineData("reader", 403)]
    [InlineData("admin", 200)]
    public async Task
        BackendDecidesProtectedRouteAuthorization(
            string? identity,
            int expected)
    {
        using var request =
            new HttpRequestMessage(
                HttpMethod.Get,
                "/api/users");

        if (identity is not null)
        {
            request.Headers.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    identity == "admin"
                        ? fixture.AdminToken
                        : identity == "reader"
                            ? fixture.UserToken
                            : "invalid.jwt");
        }

        using var response =
            await fixture.Client
                .SendAsync(request);

        Assert.Equal(
            expected,
            (int)response.StatusCode);
    }

    [Fact]
    public async Task
        InvalidJwtDoesNotTurnPublicRoutesIntoProtectedRoutes()
    {
        using var request =
            new HttpRequestMessage(
                HttpMethod.Get,
                "/api/communities");

        request.Headers.Authorization =
            new(
                "Bearer",
                "invalid.jwt");

        using var response =
            await fixture.Client
                .SendAsync(request);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task
        GatewayPerformsPreliminaryValidationWithoutBlockingPublicRequests(
            bool valid)
    {
        string id =
            Guid.NewGuid()
                .ToString();

        using var request =
            new HttpRequestMessage(
                HttpMethod.Get,
                "/api/communities");

        request.Headers.Add(
            "X-Correlation-ID",
            id);

        request.Headers.Authorization =
            new(
                "Bearer",
                valid
                    ? fixture.CreateGatewayToken()
                    : "invalid.jwt");

        using var response =
            await fixture.Client
                .SendAsync(request);

        await response.Content
            .ReadAsByteArrayAsync();

        for (
            int attempt = 0;
            attempt < 100 &&
            !GatewayFixture
                .AuthenticatedRequests
                .ContainsKey(id);
            attempt++)
        {
            await Task.Delay(5);
        }

        Assert.Equal(
            valid,
            GatewayFixture
                .AuthenticatedRequests[id]);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);
    }

    [Fact]
    public async Task
        Upstream503BusinessBodyIsPreserved()
    {
        using var response =
            await fixture.Client
                .GetAsync(
                    "/api/probe/unavailable");

        Assert.Equal(
            HttpStatusCode.ServiceUnavailable,
            response.StatusCode);

        Assert.Equal(
            "Business unavailable",
            (
                await response.Content
                    .ReadFromJsonAsync<JsonElement>()
            )
            .GetProperty("message")
            .GetString());
    }

    [Fact]
    public async Task
        AllowedCorsResponseExposesCorrelationId()
    {
        using var request =
            new HttpRequestMessage(
                HttpMethod.Get,
                "/api/communities");

        request.Headers.Add(
            "Origin",
            "http://localhost:4200");

        using var response =
            await fixture.Client
                .SendAsync(request);

        Assert.Equal(
            "http://localhost:4200",
            response.Headers
                .GetValues(
                    "Access-Control-Allow-Origin")
                .Single());

        Assert.Contains(
            "X-Correlation-ID",
            response.Headers
                .GetValues(
                    "Access-Control-Expose-Headers")
                .Single());
    }

    [Fact]
    public async Task
        MultipleCorrelationIdsAreReplaced()
    {
        using var request =
            new HttpRequestMessage(
                HttpMethod.Get,
                "/health");

        request.Headers.Add(
            "X-Correlation-ID",
            new[]
            {
                Guid.NewGuid().ToString(),
                Guid.NewGuid().ToString()
            });

        using var response =
            await fixture.Client
                .SendAsync(request);

        Assert.True(
            Guid.TryParse(
                response.Headers
                    .GetValues(
                        "X-Correlation-ID")
                    .Single(),
                out _));
    }

    [Fact]
    public async Task
        UpstreamCannotOverrideGatewayCorsPolicy()
    {
        using var request =
            new HttpRequestMessage(
                HttpMethod.Get,
                "/api/probe/headers");

        request.Headers.Add(
            "Origin",
            "https://untrusted.example");

        using var response =
            await fixture.Client
                .SendAsync(request);

        Assert.False(
            response.Headers.Contains(
                "Access-Control-Allow-Origin"));
    }

    [Theory]
    [InlineData("issuer")]
    [InlineData("audience")]
    [InlineData("signature")]
    [InlineData("expiration")]
    public async Task
        BadTokensStillReceiveBackend401(
            string defect)
    {
        var credentials =
            new SigningCredentials(
                new SymmetricSecurityKey(
                    SHA256.HashData(
                        Encoding.UTF8.GetBytes(
                            defect == "signature"
                                ? "wrong-signature-key"
                                : GatewayFixture.Key))),
                SecurityAlgorithms.HmacSha256);

        var token =
            new JwtSecurityToken(
                defect == "issuer"
                    ? "wrong"
                    : "test",

                defect == "audience"
                    ? "wrong"
                    : "test",

                [
                    new(
                        "sub",
                        Guid.NewGuid()
                            .ToString())
                ],

                DateTime.UtcNow
                    .AddHours(-2),

                defect == "expiration"
                    ? DateTime.UtcNow
                        .AddHours(-1)
                    : DateTime.UtcNow
                        .AddMinutes(10),

                credentials);

        using var request =
            new HttpRequestMessage(
                HttpMethod.Get,
                "/api/users");

        request.Headers.Authorization =
            new(
                "Bearer",
                new JwtSecurityTokenHandler()
                    .WriteToken(token));

        using var response =
            await fixture.Client
                .SendAsync(request);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Theory]
    [InlineData("/api/not-an-existing-route")]
    [InlineData("/not-api")]
    public async Task
        MissingRoutesRemain404(
            string path)
    {
        using var response =
            await fixture.Client
                .GetAsync(path);

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }

    [Fact]
    public async Task
        ReportsRouteIsAvailable()
    {
        using var response =
            await fixture.Client
                .GetAsync("/api/reports");

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);
    }

    [Fact]
    public async Task
        BusinessValidationAndConflictSurviveProxy()
    {
        using var invalid =
            new HttpRequestMessage(
                HttpMethod.Post,
                "/api/communities")
            {
                Content =
                    JsonContent.Create(
                        new
                        {
                            name = "",
                            location = ""
                        })
            };

        invalid.Headers.Authorization =
            new(
                "Bearer",
                fixture.AdminToken);

        using var bad =
            await fixture.Client
                .SendAsync(invalid);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            bad.StatusCode);

        Assert.Equal(
            400,
            (
                await bad.Content
                    .ReadFromJsonAsync<JsonElement>()
            )
            .GetProperty("status")
            .GetInt32());

        using var duplicate =
            new HttpRequestMessage(
                HttpMethod.Post,
                "/api/communities")
            {
                Content =
                    JsonContent.Create(
                        new
                        {
                            name = "Gateway test",
                            location = "Test location"
                        })
            };

        duplicate.Headers.Authorization =
            new(
                "Bearer",
                fixture.AdminToken);

        using var conflict =
            await fixture.Client
                .SendAsync(duplicate);

        Assert.Equal(
            HttpStatusCode.Conflict,
            conflict.StatusCode);

        Assert.Equal(
            409,
            (
                await conflict.Content
                    .ReadFromJsonAsync<JsonElement>()
            )
            .GetProperty("status")
            .GetInt32());
    }

    [Theory]
    [InlineData(
        "http://localhost:4200",
        true)]
    [InlineData(
        "https://untrusted.example",
        false)]
    public async Task
        CorsPreflightOnlyAllowsConfiguredOrigin(
            string origin,
            bool allowed)
    {
        using var request =
            new HttpRequestMessage(
                HttpMethod.Options,
                "/api/users");

        request.Headers.Add(
            "Origin",
            origin);

        request.Headers.Add(
            "Access-Control-Request-Method",
            "GET");

        request.Headers.Add(
            "Access-Control-Request-Headers",
            "authorization,x-correlation-id");

        using var response =
            await fixture.Client
                .SendAsync(request);

        Assert.Equal(
            HttpStatusCode.NoContent,
            response.StatusCode);

        Assert.Equal(
            allowed,
            response.Headers.Contains(
                "Access-Control-Allow-Origin"));

        if (allowed)
        {
            Assert.Equal(
                origin,
                response.Headers
                    .GetValues(
                        "Access-Control-Allow-Origin")
                    .Single());
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-valid-id")]
    [InlineData(
        "a6a58c00-cb8a-4987-918b-badfb6f1895d")]
    public async Task
        CorrelationIdIsSafeAndPropagated(
            string? supplied)
    {
        using var request =
            new HttpRequestMessage(
                HttpMethod.Get,
                "/api/probe/headers");

        if (supplied is not null)
        {
            request.Headers.Add(
                "X-Correlation-ID",
                supplied);
        }

        using var response =
            await fixture.Client
                .SendAsync(request);

        string id =
            response.Headers
                .GetValues(
                    "X-Correlation-ID")
                .Single();

        Assert.True(
            Guid.TryParse(
                id,
                out _));

        if (
            Guid.TryParse(
                supplied,
                out var expected))
        {
            Assert.Equal(
                expected.ToString(),
                id);
        }

        var received =
            await response.Content
                .ReadFromJsonAsync<
                    Dictionary<string, string>>();

        Assert.Equal(
            id,
            received!
                .Single(
                    item =>
                        item.Key.Equals(
                            "X-Correlation-ID",
                            StringComparison
                                .OrdinalIgnoreCase))
                .Value);
    }

    [Fact]
    public async Task
        SpoofedIdentityAndInternalResponseHeadersAreRemoved()
    {
        using var request =
            new HttpRequestMessage(
                HttpMethod.Get,
                "/api/probe/headers");

        string[] spoofed =
        [
            "X-User-Id",
            "X-Roles",
            "X-Auth-Request-User",
            "X-Identity",
            "X-Internal-Host",
            "Remote-User",
            "Forwarded",
            "X-Forwarded-For",
            "X-Real-IP",
            "X-Client-Cert",
            "X-MS-CLIENT-PRINCIPAL",
            "X-Original-URL"
        ];

        foreach (
            string header
            in spoofed)
        {
            request.Headers.Add(
                header,
                "untrusted");
        }

        using var response =
            await fixture.Client
                .SendAsync(request);

        var received =
            await response.Content
                .ReadFromJsonAsync<
                    Dictionary<string, string>>();

        Assert.All(
            spoofed,
            header =>
                Assert.DoesNotContain(
                    received!.Keys,
                    key =>
                        key.Equals(
                            header,
                            StringComparison
                                .OrdinalIgnoreCase)));

        Assert.False(
            response.Headers.Contains(
                "Server"));

        Assert.False(
            response.Headers.Contains(
                "X-Powered-By"));

        Assert.False(
            response.Headers.Contains(
                "X-Internal-Host"));

        Assert.Equal(
            "/api/communities/123",
            response.Headers
                .Location!
                .OriginalString);
    }

    [Theory]
    [InlineData(
        "/api/auth/login",
        "auth")]
    [InlineData(
        "/api/users",
        "users")]
    [InlineData(
        "/api/communities",
        "communities")]
    [InlineData(
        "/api/communities/{id}/sensors",
        "community-sensors")]
    [InlineData(
        "/api/communities/{id}/alerts",
        "community-alerts")]
    [InlineData(
        "/api/sensors",
        "sensors")]
    [InlineData(
        "/api/sensors/{id}/readings/latest",
        "sensors")]
    [InlineData(
        "/api/sensor-readings",
        "sensor-readings")]
    [InlineData(
        "/api/alerts",
        "alerts")]
    [InlineData(
        "/api/alert-rules",
        "alert-rules")]
    [InlineData(
        "/api/events",
        "events")]
    [InlineData(
        "/api/audit-actions",
        "audit-actions")]
    [InlineData(
        "/api/dashboard",
        "dashboard")]
    [InlineData(
        "/api/reports",
        "reports")]
    public async Task
        ExpectedYarpRouteWins(
            string path,
            string expected)
    {
        string id =
            Guid.NewGuid()
                .ToString();

        using var request =
            new HttpRequestMessage(
                HttpMethod.Get,
                path.Replace(
                    "{id}",
                    fixture.CommunityId
                        .ToString()));

        request.Headers.Add(
            "X-Correlation-ID",
            id);

        using var response =
            await fixture.Client
                .SendAsync(request);

        await response.Content
            .ReadAsByteArrayAsync();

        for (
            int attempt = 0;
            attempt < 100 &&
            !GatewayFixture
                .SelectedRoutes
                .ContainsKey(id);
            attempt++)
        {
            await Task.Delay(5);
        }

        Assert.Equal(
            expected,
            GatewayFixture
                .SelectedRoutes[id]);
    }

    [Fact]
    public async Task
        BackendUnavailableHasSafe502AndReadiness503WhileLivenessStays200()
    {
        var listener =
            new TcpListener(
                IPAddress.Loopback,
                0);

        listener.Start();

        int port =
            (
                (IPEndPoint)
                    listener.LocalEndpoint
            ).Port;

        listener.Stop();

        using var client =
            await fixture
                .AlternativeGatewayAsync(
                    $"http://127.0.0.1:{port}/");

        using var response =
            await client.GetAsync(
                "/api/communities");

        Assert.Equal(
            HttpStatusCode.BadGateway,
            response.StatusCode);

        string text =
            await response.Content
                .ReadAsStringAsync();

        Assert.Contains(
            "correlationId",
            text);

        Assert.DoesNotContain(
            "127.0.0.1",
            text);

        Assert.DoesNotContain(
            "Exception",
            text);

        Assert.DoesNotContain(
            "backend:",
            text);

        Assert.Equal(
            "application/problem+json",
            response.Content
                .Headers
                .ContentType!
                .MediaType);

        Assert.Equal(
            HttpStatusCode.ServiceUnavailable,
            (
                await client.GetAsync(
                    "/health/ready")
            ).StatusCode);

        Assert.Equal(
            HttpStatusCode.OK,
            (
                await client.GetAsync(
                    "/health/live")
            ).StatusCode);
    }

    [Fact]
    public async Task
        SlowUpstreamHasControlled504()
    {
        using var client =
            await fixture
                .AlternativeGatewayAsync(
                    fixture.Upstream,
                    "00:00:01");

        using var response =
            await client.GetAsync(
                "/api/probe/slow");

        Assert.Equal(
            HttpStatusCode.GatewayTimeout,
            response.StatusCode);

        var body =
            await response.Content
                .ReadFromJsonAsync<JsonElement>();

        Assert.Equal(
            504,
            body.GetProperty("status")
                .GetInt32());
    }
}
