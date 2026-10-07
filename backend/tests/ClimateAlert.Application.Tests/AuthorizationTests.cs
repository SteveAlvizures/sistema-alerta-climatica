using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using ClimateAlert.Api.Authentication;
using ClimateAlert.Api.Controllers;
using ClimateAlert.Domain.Entities;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace ClimateAlert.Application.Tests;

public sealed class AuthorizationTests
{
    [Theory]
    [InlineData("Administrator", "Administrator")]
    [InlineData("Operator", "Operator")]
    [InlineData("ConsultationUser", "ConsultationUser")]
    [InlineData("User", "ConsultationUser")]
    public void DomainAcceptsOfficialRolesAndLegacyAlias(string input, string expected)
    {
        User user = new("name", "email", "hash", input, DateTimeOffset.UtcNow);
        Assert.Equal(expected, user.Role);
        Assert.Throws<ArgumentException>(() => user.UpdateIdentity("name", "email", "hash", "SuperAdmin"));
    }

    [Fact]
    public async Task JwtMiddlewareEnforcesEveryControllerPermissionAndPublicRoute()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        var key = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes("authorization-tests-signing-key-at-least-32-bytes"));
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true, ValidIssuer = "tests",
                ValidateAudience = true, ValidAudience = "tests",
                ValidateIssuerSigningKey = true, IssuerSigningKey = key,
                ValidateLifetime = true, ClockSkew = TimeSpan.Zero
            });
        builder.Services.AddClimateAuthorization();
        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        // Use the actual authorization metadata from every controller action.
        // Probe handlers avoid database side effects while exercising HTTP middleware.
        var endpoints = new List<(string Path, string? Policy)>();
        foreach (var controller in typeof(AuthController).Assembly.GetTypes()
            .Where(type => typeof(ControllerBase).IsAssignableFrom(type) && !type.IsAbstract))
        foreach (var method in controller.GetMethods().Where(method => method.IsDefined(typeof(HttpMethodAttribute), true)))
        {
            var attributes = controller.GetCustomAttributes(true).Concat(method.GetCustomAttributes(true)).ToArray();
            var permission = attributes.OfType<AuthorizeAttribute>().LastOrDefault();
            bool anonymous = attributes.OfType<AllowAnonymousAttribute>().Any();
            var http = method.GetCustomAttributes(true).OfType<HttpMethodAttribute>().Single();
            if (!http.HttpMethods.Contains("GET") && !anonymous)
                Assert.Equal(controller == typeof(UsersController) ? AuthorizationPolicies.AdministratorOnly
                    : AuthorizationPolicies.OperateSystem, permission?.Policy);
            string path = $"/probe/{controller.Name}/{method.Name}";
            var endpoint = app.MapGet(path, () => Microsoft.AspNetCore.Http.Results.NoContent());
            if (permission is not null) endpoint.RequireAuthorization(permission);
            if (anonymous) endpoint.AllowAnonymous();
            endpoints.Add((path, anonymous ? null : permission?.Policy));
        }
        await app.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        foreach (string? role in new string?[] { null, "Administrator", "Operator", "ConsultationUser", "User", "Unknown" })
        {
            client.DefaultRequestHeaders.Authorization = role is null ? null : new("Bearer", Token(role));
            foreach (var endpoint in endpoints)
            {
                var expected = endpoint.Policy is null ? HttpStatusCode.NoContent
                    : role is null ? HttpStatusCode.Unauthorized
                    : role == "Administrator" || (role == "Operator" && endpoint.Policy == AuthorizationPolicies.OperateSystem)
                        ? HttpStatusCode.NoContent : HttpStatusCode.Forbidden;
                Assert.Equal(expected, (await client.GetAsync(endpoint.Path)).StatusCode);
            }
        }
        client.DefaultRequestHeaders.Authorization = new("Bearer", Token("Administrator", expired: true));
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.GetAsync(endpoints.First(endpoint => endpoint.Policy is not null).Path)).StatusCode);
        await app.StopAsync();

        string Token(string role, bool expired = false) => new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            "tests", "tests", [new Claim(ClaimTypes.Role, role)],
            DateTime.UtcNow.AddHours(-2), expired ? DateTime.UtcNow.AddHours(-1) : DateTime.UtcNow.AddMinutes(5),
            new SigningCredentials(key, SecurityAlgorithms.HmacSha256)));
    }
}
