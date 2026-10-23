using System.Text;
using CommunityService.Data;
using CommunityService.Errors;
using CommunityService.Security;
using CommunityService.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder =
    WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddProblemDetails();

builder.Services.AddExceptionHandler<
    GlobalExceptionHandler>();

builder.Services.AddSingleton(
    TimeProvider.System);

string connectionString =
    builder.Configuration
        .GetConnectionString(
            "CommunityDatabase")
    ?? throw new InvalidOperationException(
        "No se configuró CommunityDatabase.");

builder.Services.AddDbContext<
    CommunityDbContext>(
    options =>
        options.UseNpgsql(
            connectionString));

builder.Services.AddScoped<
    CommunityManager>();

string jwtSecret =
    builder.Configuration[
        "Jwt:Secret"]
    ?? throw new InvalidOperationException(
        "No se configuró Jwt:Secret.");

string jwtIssuer =
    builder.Configuration[
        "Jwt:Issuer"]
    ?? throw new InvalidOperationException(
        "No se configuró Jwt:Issuer.");

string jwtAudience =
    builder.Configuration[
        "Jwt:Audience"]
    ?? throw new InvalidOperationException(
        "No se configuró Jwt:Audience.");

builder.Services
    .AddAuthentication(
        JwtBearerDefaults
            .AuthenticationScheme)
    .AddJwtBearer(
        options =>
        {
            options.MapInboundClaims = false;

            options.TokenValidationParameters =
                new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtIssuer,

                    ValidateAudience = true,
                    ValidAudience = jwtAudience,

                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey =
                        new SymmetricSecurityKey(
                            Encoding.UTF8.GetBytes(
                                jwtSecret)),

                    ValidateLifetime = true,

                    ClockSkew =
                        TimeSpan.FromSeconds(
                            30),

                    NameClaimType = "name",
                    RoleClaimType = "role"
                };
        });

builder.Services.AddAuthorization(
    options =>
    {
        options.AddPolicy(
            AuthPolicies.AuthenticatedUser,
            policy =>
                policy.RequireAuthenticatedUser());

        options.AddPolicy(
            AuthPolicies.ReadCommunities,
            policy =>
                policy
                    .RequireAuthenticatedUser()
                    .RequireClaim(
                        "permissions",
                        "communities.read"));

        options.AddPolicy(
            AuthPolicies.WriteCommunities,
            policy =>
                policy
                    .RequireAuthenticatedUser()
                    .RequireClaim(
                        "permissions",
                        "communities.write"));

        options.AddPolicy(
            AuthPolicies.OperateSystem,
            policy =>
                policy
                    .RequireAuthenticatedUser()
                    .RequireAssertion(
                        context =>
                            context.User.IsInRole(
                                "Administrator") ||
                            context.User.IsInRole(
                                "Operator")));
    });

var app =
    builder.Build();

app.UseExceptionHandler();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
