using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClimateAlert.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CompleteCommunityAndSensorManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Sensors",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "InstallationDate",
                table: "Sensors",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "Sensors",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Unit",
                table: "Sensors",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Country",
                table: "Communities",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Department",
                table: "Communities",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Latitude",
                table: "Communities",
                type: "decimal(10,7)",
                precision: 10,
                scale: 7,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Longitude",
                table: "Communities",
                type: "decimal(10,7)",
                precision: 10,
                scale: 7,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Municipality",
                table: "Communities",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Description",
                table: "Sensors");

            migrationBuilder.DropColumn(
                name: "InstallationDate",
                table: "Sensors");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Sensors");

            migrationBuilder.DropColumn(
                name: "Unit",
                table: "Sensors");

            migrationBuilder.DropColumn(
                name: "Country",
                table: "Communities");

            migrationBuilder.DropColumn(
                name: "Department",
                table: "Communities");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "Communities");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "Communities");

            migrationBuilder.DropColumn(
                name: "Municipality",
                table: "Communities");
        }
    }
}
