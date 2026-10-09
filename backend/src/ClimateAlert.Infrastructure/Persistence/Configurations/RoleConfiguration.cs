using ClimateAlert.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClimateAlert.Infrastructure.Persistence.Configurations;

public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Roles");

        builder.HasKey(role => role.Id);

        builder.Property(role => role.Id)
            .ValueGeneratedNever();

        builder.Property(role => role.Name)
            .HasMaxLength(80)
            .IsRequired();

        builder.Property(role => role.Description)
            .HasMaxLength(250)
            .IsRequired();

        builder.HasIndex(role => role.Name)
            .IsUnique();

        builder.HasData(
            new
            {
                Id = UserRoles.AdministratorId,
                Name = UserRoles.Administrator,
                Description = "Administración completa del sistema."
            },
            new
            {
                Id = UserRoles.OperatorId,
                Name = UserRoles.Operator,
                Description = "Operación y gestión del monitoreo climático."
            },
            new
            {
                Id = UserRoles.QueryId,
                Name = UserRoles.Query,
                Description = "Consulta y visualización de información."
            }
        );
    }
}
