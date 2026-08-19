using ClimateAlert.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClimateAlert.Infrastructure.Persistence.Configurations;

public sealed class CommunityConfiguration : IEntityTypeConfiguration<Community>
{
    public void Configure(EntityTypeBuilder<Community> builder)
    {
        builder.ToTable("Communities");
        builder.HasKey(community => community.Id);
        builder.Property(community => community.Id).ValueGeneratedNever();
        builder.Property(community => community.Name).HasMaxLength(150).IsRequired();
        builder.Property(community => community.Location).HasMaxLength(250).IsRequired();
        builder.Property(community => community.Description).HasMaxLength(1000);
        builder.Property(community => community.IsActive).IsRequired();
        builder.Property(community => community.CreatedAt).HasColumnType("datetimeoffset").IsRequired();
        builder.HasIndex(community => new { community.Name, community.Location }).IsUnique();

        builder.HasMany(community => community.Sensors)
            .WithOne(sensor => sensor.Community)
            .HasForeignKey(sensor => sensor.CommunityId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(community => community.Sensors).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(community => community.AlertRules)
            .WithOne(rule => rule.Community)
            .HasForeignKey(rule => rule.CommunityId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(community => community.AlertRules).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(community => community.Alerts)
            .WithOne(alert => alert.Community)
            .HasForeignKey(alert => alert.CommunityId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(community => community.Alerts).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(community => community.Events)
            .WithOne(climateEvent => climateEvent.Community)
            .HasForeignKey(climateEvent => climateEvent.CommunityId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(community => community.Events).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
