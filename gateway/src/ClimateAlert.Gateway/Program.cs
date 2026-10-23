using ClimateAlert.Gateway.Authentication;
using ClimateAlert.Gateway.Errors;
using ClimateAlert.Gateway.Health;
using ClimateAlert.Gateway.Observability;
using ClimateAlert.Gateway.Proxy;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var app = GatewayApplication.Build(args);
app.Run();

public static class GatewayApplication
{
    public static WebApplication Build(string[] args, Action<WebApplicationBuilder>? configure = null)
    {
        var builder = WebApplication.CreateBuilder(args);
        configure?.Invoke(builder);
        builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);
        builder.Services.AddProblemDetails();
        builder.Services.AddExceptionHandler<GatewayExceptionHandler>();
        builder.Services.AddInitialJwtValidation(builder.Configuration);
        builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
        {
            var origins = builder.Configuration.GetSection("Gateway:AllowedOrigins").Get<string[]>() ?? [];
            if (origins.Length > 0) policy.WithOrigins(origins).AllowAnyMethod().AllowAnyHeader()
                .WithExposedHeaders(CorrelationMiddleware.HeaderName, "Content-Disposition");
        }));
        builder.Services.AddReverseProxy()
            .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
            .AddTransforms(ProxyTransforms.Configure);
        builder.Services.AddHttpClient("readiness", client => client.Timeout = TimeSpan.FromSeconds(3))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        builder.Services.AddHealthChecks().AddCheck<UpstreamHealthCheck>("backend", tags: ["ready"])
            .AddCheck<AuthUpstreamHealthCheck>("auth", tags: ["ready"])
            .AddCheck<ReportUpstreamHealthCheck>("reports", tags: ["ready"]);

        var app = builder.Build();
        app.UseMiddleware<CorrelationMiddleware>();
        app.UseExceptionHandler();
        app.UseRouting();
        app.UseCors();
        // Preliminary cryptographic validation only. The monolith still checks the current
        // account and authorizes every protected operation. No global authorization policy.
        app.UseAuthentication();
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            Predicate = _ => false,
            ResponseWriter = GatewayHealthResponse.WriteAsync
        });
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false,
            ResponseWriter = GatewayHealthResponse.WriteAsync
        });
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
            ResultStatusCodes = { [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable },
            ResponseWriter = GatewayHealthResponse.WriteAsync
        });
        app.MapReverseProxy(proxy =>
        {
            proxy.UseMiddleware<ProxyErrorMiddleware>();
            proxy.UseLoadBalancing();
        });
        return app;
    }
}
