using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ClimateAlert.Gateway.Tests;

public class OAuthRedirectTests
{
    [Theory]
    [InlineData("/api/auth/oauth/google", "https://accounts.google.com/o/oauth2/v2/auth?state=fixture", true)]
    [InlineData("/api/auth/oauth/github", "https://github.com/login/oauth/authorize?state=fixture", true)]
    [InlineData("/api/auth/oauth/google", "https://accounts.google.com.evil.test/o/oauth2/v2/auth", false)]
    [InlineData("/api/auth/oauth/google", "https://accounts.google.com/other", false)]
    [InlineData("/api/auth/oauth/github", "http://github.com/login/oauth/authorize", false)]
    [InlineData("/api/auth/oauth/github", "https://github.com:444/login/oauth/authorize", false)]
    [InlineData("/api/auth/other", "https://github.com/login/oauth/authorize", false)]
    public async Task OnlyKnownProviderAuthorizationRedirectsLeaveGateway(string path,string location,bool allowed)
    {
        var upstreamBuilder=WebApplication.CreateBuilder();
        upstreamBuilder.Logging.ClearProviders();upstreamBuilder.WebHost.UseUrls("http://127.0.0.1:0");
        await using var upstream=upstreamBuilder.Build();
        upstream.MapGet("/{**rest}",()=>Results.Redirect(location));await upstream.StartAsync();
        await using var gateway=GatewayApplication.Build([],builder=>
        {
            builder.Logging.ClearProviders();builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Configuration.AddInMemoryCollection(new Dictionary<string,string?>
            {
                ["JWT_KEY"]="oauth-redirect-fixture-only-key-32-characters",
                ["ReverseProxy:Clusters:auth:Destinations:auth-service:Address"]=upstream.Urls.Single()+"/"
            });
        });
        await gateway.StartAsync();
        using var client=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false}){BaseAddress=new Uri(gateway.Urls.Single())};
        using var response=await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.Found,response.StatusCode);
        Assert.Equal(allowed?location:new Uri(location).PathAndQuery,response.Headers.Location!.OriginalString);
    }
}
