using ClimateAlert.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClimateAlert.Infrastructure.Persistence.Configurations;

public sealed class SensorReadingConfiguration : IEntityTypeConfiguration<SensorReading>
{
    public void Configure(EntityTypeBuilder<SensorReading> builder)
    {
        builder.ToTable("SensorReadings");
        builder.HasKey(reading => reading.Id);
        builder.Property(reading => reading.Id).ValueGeneratedNever();
        builder.Property(reading => reading.Variable).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(reading => reading.Value).HasPrecision(18, 4).IsRequired();
        builder.Property(reading => reading.Unit).HasMaxLength(30).IsRequired();
        builder.Property(reading => reading.MeasuredAt).HasColumnType("datetimeoffset").IsRequired();
        builder.Property(reading => reading.ReceivedAt).HasColumnType("datetimeoffset").IsRequired();
        builder.Property(reading => reading.Origin).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(reading => reading.CreatedAt).HasColumnType("datetimeoffset").IsRequired();
        builder.HasIndex(reading => new { reading.SensorId, reading.MeasuredAt });
    }
}
