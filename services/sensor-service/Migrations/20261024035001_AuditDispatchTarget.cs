using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SensorService.Migrations
{
    /// <inheritdoc />
    public partial class AuditDispatchTarget : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Target",
                table: "EvaluationDispatch",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Target",
                table: "EvaluationDispatch");
        }
    }
}
