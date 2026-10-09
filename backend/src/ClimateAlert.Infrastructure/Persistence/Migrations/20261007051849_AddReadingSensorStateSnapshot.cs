using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClimateAlert.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReadingSensorStateSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SensorStatusAtMeasurement",
                table: "SensorReadings",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SensorStatusAtMeasurement",
                table: "SensorReadings");
        }
    }
}
