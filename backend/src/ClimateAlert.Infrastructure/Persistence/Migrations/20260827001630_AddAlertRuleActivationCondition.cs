using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClimateAlert.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAlertRuleActivationCondition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ActivationPoint",
                table: "AlertRules",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ComparisonOperator",
                table: "AlertRules",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("""
                UPDATE [AlertRules]
                SET [ComparisonOperator] = CASE WHEN [LowerLimit] IS NOT NULL THEN '>=' ELSE '<=' END,
                    [ActivationPoint] = COALESCE([LowerLimit], [UpperLimit], 0)
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActivationPoint",
                table: "AlertRules");

            migrationBuilder.DropColumn(
                name: "ComparisonOperator",
                table: "AlertRules");
        }
    }
}
