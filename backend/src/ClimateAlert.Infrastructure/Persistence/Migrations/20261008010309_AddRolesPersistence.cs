using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ClimateAlert.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRolesPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<Guid>(
                        type: "uniqueidentifier",
                        nullable: false),

                    Name = table.Column<string>(
                        type: "nvarchar(80)",
                        maxLength: 80,
                        nullable: false),

                    Description = table.Column<string>(
                        type: "nvarchar(250)",
                        maxLength: 250,
                        nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "Id", "Description", "Name" },
                values: new object[,]
                {
                    {
                        new Guid("11111111-1111-1111-1111-111111111111"),
                        "Administración completa del sistema.",
                        "Administrator"
                    },
                    {
                        new Guid("22222222-2222-2222-2222-222222222222"),
                        "Operación y gestión del monitoreo climático.",
                        "Operator"
                    },
                    {
                        new Guid("33333333-3333-3333-3333-333333333333"),
                        "Consulta y visualización de información.",
                        "Query"
                    }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Name",
                table: "Roles",
                column: "Name",
                unique: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RoleId",
                table: "Users",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE [Users]
                SET [RoleId] =
                    CASE
                        WHEN [Role] = 'Administrator'
                            THEN '11111111-1111-1111-1111-111111111111'
                        WHEN [Role] = 'Operator'
                            THEN '22222222-2222-2222-2222-222222222222'
                        WHEN [Role] IN ('Query', 'User')
                            THEN '33333333-3333-3333-3333-333333333333'
                        ELSE NULL
                    END;
                """);

            migrationBuilder.Sql(
                """
                IF EXISTS (
                    SELECT 1
                    FROM [Users]
                    WHERE [RoleId] IS NULL
                )
                BEGIN
                    THROW 50000,
                        'AddRolesPersistence: existen usuarios con un rol no reconocido.',
                        1;
                END;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "RoleId",
                table: "Users",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_RoleId",
                table: "Users",
                column: "RoleId");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Roles_RoleId",
                table: "Users",
                column: "RoleId",
                principalTable: "Roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.DropColumn(
                name: "Role",
                table: "Users");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "Users",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE [Users]
                SET [Role] =
                    CASE
                        WHEN [RoleId] =
                            '11111111-1111-1111-1111-111111111111'
                            THEN 'Administrator'
                        WHEN [RoleId] =
                            '22222222-2222-2222-2222-222222222222'
                            THEN 'Operator'
                        WHEN [RoleId] =
                            '33333333-3333-3333-3333-333333333333'
                            THEN 'Query'
                        ELSE NULL
                    END;
                """);

            migrationBuilder.Sql(
                """
                IF EXISTS (
                    SELECT 1
                    FROM [Users]
                    WHERE [Role] IS NULL
                )
                BEGIN
                    THROW 50001,
                        'AddRolesPersistence rollback: existe un RoleId no reconocido.',
                        1;
                END;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "Role",
                table: "Users",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(80)",
                oldMaxLength: 80,
                oldNullable: true);

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Roles_RoleId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_RoleId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "RoleId",
                table: "Users");

            migrationBuilder.DropTable(
                name: "Roles");
        }
    }
}
