using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SensorService.Migrations
{
    /// <inheritdoc />
    public partial class DeviceIngestion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DeviceCredential",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    KeyHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUsedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SensorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceCredential", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeviceCredential_Sensors_SensorId",
                        column: x => x.SensorId,
                        principalTable: "Sensors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DeviceReadingReceipt",
                columns: table => new
                {
                    DeviceCredentialId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReadingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceReadingReceipt", x => new { x.DeviceCredentialId, x.MessageId });
                    table.ForeignKey(
                        name: "FK_DeviceReadingReceipt_DeviceCredential_DeviceCredentialId",
                        column: x => x.DeviceCredentialId,
                        principalTable: "DeviceCredential",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DeviceReadingReceipt_Readings_ReadingId",
                        column: x => x.ReadingId,
                        principalTable: "Readings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceCredential_KeyHash",
                table: "DeviceCredential",
                column: "KeyHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeviceCredential_SensorId",
                table: "DeviceCredential",
                column: "SensorId");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceReadingReceipt_ReadingId",
                table: "DeviceReadingReceipt",
                column: "ReadingId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeviceReadingReceipt");

            migrationBuilder.DropTable(
                name: "DeviceCredential");
        }
    }
}
