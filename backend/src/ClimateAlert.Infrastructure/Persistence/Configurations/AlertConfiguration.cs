using ClimateAlert.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClimateAlert.Infrastructure.Persistence.Configurations;

public sealed class AlertConfiguration : IEntityTypeConfiguration<Alert>
{
    public void Configure(EntityTypeBuilder<Alert> builder)
    {
        builder.ToTable("Alerts");
        builder.HasKey(alert => alert.Id);
        builder.Property(alert => alert.Id).ValueGeneratedNever();
        builder.Property(alert => alert.Level).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(alert => alert.Phenomenon).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(alert => alert.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(alert => alert.Message).HasMaxLength(1000).IsRequired();
        builder.Property(alert => alert.DetectedAt).HasColumnType("datetimeoffset").IsRequired();
        builder.Property(alert => alert.UpdatedAt).HasColumnType("datetimeoffset").IsRequired();
        builder.Property(alert => alert.ClosedAt).HasColumnType("datetimeoffset");
        builder.Property(alert => alert.CreatedAt).HasColumnType("datetimeoffset").IsRequired();
        builder.HasIndex(alert => new { alert.CommunityId, alert.Status, alert.DetectedAt });

        builder.HasOne(alert => alert.SupportingReading)
            .WithMany()
            .HasForeignKey(alert => alert.SupportingReadingId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(alert => alert.Event)
            .WithMany(climateEvent => climateEvent.Alerts)
            .HasForeignKey(alert => alert.EventId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
