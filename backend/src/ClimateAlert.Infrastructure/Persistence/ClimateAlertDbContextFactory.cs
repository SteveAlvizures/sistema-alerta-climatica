using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ClimateAlert.Infrastructure.Persistence;

public sealed class ClimateAlertDbContextFactory : IDesignTimeDbContextFactory<ClimateAlertDbContext>
{
    public ClimateAlertDbContext CreateDbContext(string[] args)
    {
        // Scaffolding migrations does not connect to the database or start API seeders.
        string connection = Environment.GetEnvironmentVariable("SQLSERVER_CONNECTION_STRING")
            ?? "Server=localhost;Database=ClimateAlertDesignTime;Trusted_Connection=True;TrustServerCertificate=True";
        return new ClimateAlertDbContext(new DbContextOptionsBuilder<ClimateAlertDbContext>().UseSqlServer(connection).Options);
    }
}
