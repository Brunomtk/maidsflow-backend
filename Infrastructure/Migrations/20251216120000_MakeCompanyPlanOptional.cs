using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    public partial class MakeCompanyPlanOptional : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Companies.PlanId deixa de ser obrigatório (nullable) e a FK passa a SET NULL.

            migrationBuilder.DropForeignKey(
                name: "FK_Companies_Plans_PlanId",
                table: "Companies");

            migrationBuilder.AlterColumn<int>(
                name: "PlanId",
                table: "Companies",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddForeignKey(
                name: "FK_Companies_Plans_PlanId",
                table: "Companies",
                column: "PlanId",
                principalTable: "Plans",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Volta para obrigatório.
            // OBS: para garantir que o ALTER COLUMN não falhe, preenchemos NULL com 1.
            // Isso assume que exista um plano Id=1 no banco.

            migrationBuilder.DropForeignKey(
                name: "FK_Companies_Plans_PlanId",
                table: "Companies");

            migrationBuilder.Sql("UPDATE \"Companies\" SET \"PlanId\" = 1 WHERE \"PlanId\" IS NULL;");

            migrationBuilder.AlterColumn<int>(
                name: "PlanId",
                table: "Companies",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Companies_Plans_PlanId",
                table: "Companies",
                column: "PlanId",
                principalTable: "Plans",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
