using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using ClimateAlert.Api.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace ClimateAlert.Gateway.Tests;

public sealed class AuthInteropFactAttribute : FactAttribute
{
    public AuthInteropFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AUTH_INTEROP_URL")))
            Skip = "Start the dedicated NestJS/PostgreSQL integration service and set AUTH_INTEROP_URL.";
    }
}

public sealed class AuthServiceInteropTests
{
    private const string Key = "fixture-only-signing-key-at-least-32-characters";
    private static string Url => Environment.GetEnvironmentVariable("AUTH_INTEROP_URL")!;
    private static JsonElement Fixture => JsonDocument.Parse(File.ReadAllText(
        Environment.GetEnvironmentVariable("AUTH_INTEROP_FIXTURE")!)).RootElement;

    [AuthInteropFact]
    public async Task RealNestJwtIsAuthenticatedByDotNetGatewayAndAuthorizesUsers()
    {
        var observation = new AuthenticationObservation();
        await using var gateway = GatewayApplication.Build([], builder =>
        {
            builder.Configuration.AddJsonFile(Path.Combine(AppContext.BaseDirectory,"GatewaySettings.json"));
            builder.Configuration.AddInMemoryCollection(new Dictionary<string,string?>
            {
                ["JWT_KEY"] = Key, ["JWT_ISSUER"] = "test", ["JWT_AUDIENCE"] = "test",
                ["ReverseProxy:Clusters:auth:Destinations:auth-service:Address"] = Url
            });
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Logging.ClearProviders();
            builder.Services.AddSingleton<IStartupFilter>(observation);
        });
        await gateway.StartAsync();
        using var client = new HttpClient {BaseAddress=new Uri(gateway.Urls.Single())};
        using var login = await client.PostAsJsonAsync("/api/auth/login",new {username="fixture-admin",password=Fixture.GetProperty("password").GetString()});
        Assert.Equal(HttpStatusCode.OK,login.StatusCode);
        var response=await login.Content.ReadFromJsonAsync<LoginResponse>();
        var token=new JwtSecurityTokenHandler().ReadJwtToken(response!.AccessToken);
        Assert.Equal("test",token.Issuer);Assert.Contains("test",token.Audiences);
        Assert.Contains(token.Claims,claim=>claim.Type==ClaimTypes.Role&&claim.Value=="Administrator");
        client.DefaultRequestHeaders.Authorization=new("Bearer",response.AccessToken);
        Assert.Equal(HttpStatusCode.OK,(await client.GetAsync("/api/users")).StatusCode);
        Assert.True(await observation.Authenticated.Task.WaitAsync(TimeSpan.FromSeconds(3)));
        client.DefaultRequestHeaders.Authorization=new("Bearer",response.AccessToken[..^10]+"AAAAAAAAAA");
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("/api/users")).StatusCode);
        await gateway.StopAsync();
    }

    [AuthInteropFact]
    public async Task DotNetJwtIsAcceptedByNestAndCurrentIdentityIsReturned()
    {
        var options=new JwtOptions(Key,"test","test",60);
        var token=new JwtSecurityToken("test","test",[
            new("sub",Fixture.GetProperty("userId").GetString()!),new(ClaimTypes.Role,"Administrator")],
            DateTime.UtcNow,DateTime.UtcNow.AddMinutes(5),
            new SigningCredentials(new SymmetricSecurityKey(options.SigningKey),SecurityAlgorithms.HmacSha256));
        using var client=new HttpClient {BaseAddress=new Uri(Url)};
        client.DefaultRequestHeaders.Authorization=new("Bearer",new JwtSecurityTokenHandler().WriteToken(token));
        client.DefaultRequestHeaders.Add("X-Internal-Service-Key","integration-only-internal-key-32-characters");
        using var response=await client.GetAsync("/internal/identity/current");
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        var identity=await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(Fixture.GetProperty("userId").GetString(),identity.GetProperty("id").GetString());
        Assert.Equal("Administrator",identity.GetProperty("role").GetString());
    }

    private sealed class AuthenticationObservation : IStartupFilter
    {
        public TaskCompletionSource<bool> Authenticated {get;} = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)=>app=>
        {
            app.Use(async (context, remaining)=>
            {
                await remaining();
                if(context.Request.Path=="/api/users")Authenticated.TrySetResult(context.User.Identity?.IsAuthenticated==true);
            });
            next(app);
        };
    }
}
