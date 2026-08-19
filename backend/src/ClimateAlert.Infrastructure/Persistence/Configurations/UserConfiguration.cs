using ClimateAlert.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClimateAlert.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(user => user.Id);
        builder.Property(user => user.Id).ValueGeneratedNever();
        builder.Property(user => user.Name).HasMaxLength(150).IsRequired();
        builder.Property(user => user.Email).HasMaxLength(254).IsRequired();
        builder.Property(user => user.PasswordHash).HasMaxLength(500).IsRequired();
        builder.Property(user => user.Role).HasMaxLength(80).IsRequired();
        builder.Property(user => user.IsActive).IsRequired();
        builder.Property(user => user.CreatedAt).HasColumnType("datetimeoffset").IsRequired();
        builder.Property(user => user.LastAccessAt).HasColumnType("datetimeoffset");
        builder.HasIndex(user => user.Email).IsUnique();

        builder.HasMany(user => user.RefreshTokens)
            .WithOne(token => token.User)
            .HasForeignKey(token => token.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(user => user.RefreshTokens).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(user => user.AuditActions)
            .WithOne(action => action.User)
            .HasForeignKey(action => action.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(user => user.AuditActions).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
