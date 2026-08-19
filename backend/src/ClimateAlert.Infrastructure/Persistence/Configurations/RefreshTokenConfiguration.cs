using ClimateAlert.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClimateAlert.Infrastructure.Persistence.Configurations;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");
        builder.HasKey(token => token.Id);
        builder.Property(token => token.Id).ValueGeneratedNever();
        builder.Property(token => token.TokenHash).HasMaxLength(500).IsRequired();
        builder.Property(token => token.IssuedAt).HasColumnType("datetimeoffset").IsRequired();
        builder.Property(token => token.ExpiresAt).HasColumnType("datetimeoffset").IsRequired();
        builder.Property(token => token.RevokedAt).HasColumnType("datetimeoffset");
        builder.Property(token => token.RevocationReason).HasMaxLength(500);
        builder.Property(token => token.CreatedAt).HasColumnType("datetimeoffset").IsRequired();
        builder.Ignore(token => token.IsRevoked);
        builder.HasIndex(token => token.TokenHash).IsUnique();
    }
}
