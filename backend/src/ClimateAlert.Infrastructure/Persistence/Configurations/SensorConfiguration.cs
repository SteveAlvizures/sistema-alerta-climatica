using ClimateAlert.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClimateAlert.Infrastructure.Persistence.Configurations;

public sealed class SensorConfiguration : IEntityTypeConfiguration<Sensor>
{
    public void Configure(EntityTypeBuilder<Sensor> builder)
    {
        builder.ToTable("Sensors");
        builder.HasKey(sensor => sensor.Id);
        builder.Property(sensor => sensor.Id).ValueGeneratedNever();
        builder.Property(sensor => sensor.Code).HasMaxLength(80).IsRequired();
        builder.Property(sensor => sensor.Name).HasMaxLength(150).IsRequired();
        builder.Property(sensor => sensor.MeasurementType).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(sensor => sensor.Origin).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(sensor => sensor.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(sensor => sensor.Location).HasMaxLength(250).IsRequired();
        builder.Property(sensor => sensor.DeviceCode).HasMaxLength(120);
        builder.Property(sensor => sensor.LastCommunicationAt).HasColumnType("datetimeoffset");
        builder.Property(sensor => sensor.CreatedAt).HasColumnType("datetimeoffset").IsRequired();
        builder.HasIndex(sensor => new { sensor.CommunityId, sensor.Code }).IsUnique();
        builder.HasIndex(sensor => sensor.DeviceCode).IsUnique().HasFilter("[DeviceCode] IS NOT NULL");

        builder.HasMany(sensor => sensor.Readings)
            .WithOne(reading => reading.Sensor)
            .HasForeignKey(reading => reading.SensorId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(sensor => sensor.Readings).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
