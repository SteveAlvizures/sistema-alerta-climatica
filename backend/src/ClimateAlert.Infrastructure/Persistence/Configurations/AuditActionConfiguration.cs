using ClimateAlert.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClimateAlert.Infrastructure.Persistence.Configurations;

public sealed class AuditActionConfiguration : IEntityTypeConfiguration<AuditAction>
{
    public void Configure(EntityTypeBuilder<AuditAction> builder)
    {
        builder.ToTable("AuditActions");
        builder.HasKey(action => action.Id);
        builder.Property(action => action.Id).ValueGeneratedNever();
        builder.Property(action => action.Action).HasMaxLength(120).IsRequired();
        builder.Property(action => action.Description).HasMaxLength(1000).IsRequired();
        builder.Property(action => action.AffectedEntity).HasMaxLength(120).IsRequired();
        builder.Property(action => action.OccurredAt).HasColumnType("datetimeoffset").IsRequired();
        builder.Property(action => action.CreatedAt).HasColumnType("datetimeoffset").IsRequired();
        builder.HasIndex(action => new { action.UserId, action.OccurredAt });
    }
}
