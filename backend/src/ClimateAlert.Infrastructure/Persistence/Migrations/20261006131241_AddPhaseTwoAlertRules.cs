using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClimateAlert.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPhaseTwoAlertRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ActivationPointSnapshot",
                table: "Alerts",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Message",
                table: "AlertRules",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "UsesRange",
                table: "AlertRules",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // Preserve the text and threshold previously shown for existing alerts.
            migrationBuilder.Sql("UPDATE [AlertRules] SET [Message] = [Name]");
            migrationBuilder.Sql("UPDATE a SET [ActivationPointSnapshot] = r.[ActivationPoint] FROM [Alerts] a INNER JOIN [AlertRules] r ON a.[RuleId] = r.[Id]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActivationPointSnapshot",
                table: "Alerts");

            migrationBuilder.DropColumn(
                name: "Message",
                table: "AlertRules");

            migrationBuilder.DropColumn(
                name: "UsesRange",
                table: "AlertRules");
        }
    }
}
