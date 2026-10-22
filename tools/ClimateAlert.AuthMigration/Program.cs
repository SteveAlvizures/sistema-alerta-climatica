using System.Text.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using ClimateAlert.Domain.Entities;
using ClimateAlert.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

try { await ExecuteAsync(args); }
catch
{
    Console.Error.WriteLine("Migration utility failed. Review configuration/source privately; no credentials were printed.");
    Environment.ExitCode = 1;
}

static async Task ExecuteAsync(string[] args)
{
if (args.Length != 2 || args[0] is not ("export" or "fixtures"))
    throw new ArgumentException("Usage: export|fixtures <private output path>");
object output;
if (args[0] == "export")
{
    var connection = Environment.GetEnvironmentVariable("SQLSERVER_CONNECTION_STRING")
        ?? throw new InvalidOperationException("SQLSERVER_CONNECTION_STRING is required.");
    await using var db = new ClimateAlertDbContext(new DbContextOptionsBuilder<ClimateAlertDbContext>().UseSqlServer(connection).Options);
    // Read-only export. Never migrates, seeds, updates or deletes source records.
    output = await db.Users.AsNoTracking().OrderBy(user => user.Id).Select(user => new
    { user.Id, user.Name, user.Email, user.PasswordHash, user.Role, user.IsActive, user.CreatedAt, user.LastAccessAt }).ToArrayAsync();
}
else
{
    const string password = "Fixture-only-Password-123!";
    var user = new User("Fixture admin", "fixture-admin", "pending", "Administrator", DateTimeOffset.UtcNow);
    var hashes = new[]
    {
        new { format = "IdentityV3", hash = new PasswordHasher<User>().HashPassword(user,password) },
        new { format = "IdentityV2", hash = new PasswordHasher<User>(Options.Create(new PasswordHasherOptions
            { CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV2 })).HashPassword(user,password) }
    };
    string key = Environment.GetEnvironmentVariable("JWT_KEY") ?? "fixture-only-signing-key-at-least-32-characters";
    var credentials = new SigningCredentials(new SymmetricSecurityKey(SHA256.HashData(Encoding.UTF8.GetBytes(key))),SecurityAlgorithms.HmacSha256);
    var token = new JwtSecurityToken("test","test",[new("sub", user.Id.ToString()),new(ClaimTypes.Name,user.Name),new(ClaimTypes.Role,user.Role),new("email",user.Email),new("jti",Guid.NewGuid().ToString())],
        DateTime.UtcNow,DateTime.UtcNow.AddHours(1),credentials);
    output = new { password, hashes, userId=user.Id, token = new JwtSecurityTokenHandler().WriteToken(token) };
}
string path = Path.GetFullPath(args[1]);
Directory.CreateDirectory(Path.GetDirectoryName(path)!);
await File.WriteAllTextAsync(path, JsonSerializer.Serialize(output,new JsonSerializerOptions(JsonSerializerDefaults.Web)));
if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path,UnixFileMode.UserRead|UnixFileMode.UserWrite);
Console.WriteLine("Private output written; no credentials or hashes printed.");
}
