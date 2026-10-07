using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClimateAlert.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAlertLifecycleTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AcknowledgedAt",
                table: "Alerts",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AcknowledgedById",
                table: "Alerts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ClosedById",
                table: "Alerts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ComparisonOperatorSnapshot",
                table: "Alerts",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LowerLimitSnapshot",
                table: "Alerts",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "UpperLimitSnapshot",
                table: "Alerts",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "UsesRangeSnapshot",
                table: "Alerts",
                type: "bit",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_AcknowledgedById",
                table: "Alerts",
                column: "AcknowledgedById");

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_ClosedById",
                table: "Alerts",
                column: "ClosedById");

            migrationBuilder.AddForeignKey(
                name: "FK_Alerts_Users_AcknowledgedById",
                table: "Alerts",
                column: "AcknowledgedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Alerts_Users_ClosedById",
                table: "Alerts",
                column: "ClosedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Alerts_Users_AcknowledgedById",
                table: "Alerts");

            migrationBuilder.DropForeignKey(
                name: "FK_Alerts_Users_ClosedById",
                table: "Alerts");

            migrationBuilder.DropIndex(
                name: "IX_Alerts_AcknowledgedById",
                table: "Alerts");

            migrationBuilder.DropIndex(
                name: "IX_Alerts_ClosedById",
                table: "Alerts");

            migrationBuilder.DropColumn(
                name: "AcknowledgedAt",
                table: "Alerts");

            migrationBuilder.DropColumn(
                name: "AcknowledgedById",
                table: "Alerts");

            migrationBuilder.DropColumn(
                name: "ClosedById",
                table: "Alerts");

            migrationBuilder.DropColumn(
                name: "ComparisonOperatorSnapshot",
                table: "Alerts");

            migrationBuilder.DropColumn(
                name: "LowerLimitSnapshot",
                table: "Alerts");

            migrationBuilder.DropColumn(
                name: "UpperLimitSnapshot",
                table: "Alerts");

            migrationBuilder.DropColumn(
                name: "UsesRangeSnapshot",
                table: "Alerts");
        }
    }
}
