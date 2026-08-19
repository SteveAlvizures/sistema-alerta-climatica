using ClimateAlert.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClimateAlert.Infrastructure.Persistence.Configurations;

public sealed class AlertRuleConfiguration : IEntityTypeConfiguration<AlertRule>
{
    public void Configure(EntityTypeBuilder<AlertRule> builder)
    {
        builder.ToTable("AlertRules");
        builder.HasKey(rule => rule.Id);
        builder.Property(rule => rule.Id).ValueGeneratedNever();
        builder.Property(rule => rule.Code).HasMaxLength(80).IsRequired();
        builder.Property(rule => rule.Name).HasMaxLength(150).IsRequired();
        builder.Property(rule => rule.Phenomenon).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(rule => rule.Variable).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(rule => rule.DangerLevel).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(rule => rule.LowerLimit).HasPrecision(18, 4);
        builder.Property(rule => rule.UpperLimit).HasPrecision(18, 4);
        builder.Property(rule => rule.ValidFrom).HasColumnType("datetimeoffset").IsRequired();
        builder.Property(rule => rule.ValidUntil).HasColumnType("datetimeoffset");
        builder.Property(rule => rule.IsActive).IsRequired();
        builder.Property(rule => rule.CreatedAt).HasColumnType("datetimeoffset").IsRequired();
        builder.HasIndex(rule => new { rule.CommunityId, rule.Code }).IsUnique();

        builder.HasOne(rule => rule.Sensor)
            .WithMany()
            .HasForeignKey(rule => rule.SensorId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(rule => rule.Alerts)
            .WithOne(alert => alert.Rule)
            .HasForeignKey(alert => alert.RuleId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(rule => rule.Alerts).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
