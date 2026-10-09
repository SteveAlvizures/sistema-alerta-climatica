using ClimateAlert.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ClimateEvent = ClimateAlert.Domain.Entities.Event;

namespace ClimateAlert.Infrastructure.Persistence.Configurations;

public sealed class EventConfiguration : IEntityTypeConfiguration<ClimateEvent>
{
    public void Configure(EntityTypeBuilder<ClimateEvent> builder)
    {
        builder.ToTable("Events");
        builder.HasKey(climateEvent => climateEvent.Id);
        builder.Property(climateEvent => climateEvent.Id).ValueGeneratedNever();
        builder.Property(climateEvent => climateEvent.Phenomenon).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(climateEvent => climateEvent.Description).HasMaxLength(1000).IsRequired();
        builder.Property(climateEvent => climateEvent.HighestLevel).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(climateEvent => climateEvent.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(climateEvent => climateEvent.StartedAt).HasColumnType("datetimeoffset").IsRequired();
        builder.Property(climateEvent => climateEvent.UpdatedAt).HasColumnType("datetimeoffset").IsRequired();
        builder.Property(climateEvent => climateEvent.EndedAt).HasColumnType("datetimeoffset");
        builder.Property(climateEvent => climateEvent.CreatedAt).HasColumnType("datetimeoffset").IsRequired();
        builder.HasIndex(climateEvent => new { climateEvent.CommunityId, climateEvent.Status, climateEvent.StartedAt });
        builder.Navigation(climateEvent => climateEvent.Alerts).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
