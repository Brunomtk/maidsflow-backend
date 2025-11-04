using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    public partial class AddCompanyIdToChecklists : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CompanyId",
                table: "Checklists",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE ""Checklists"" ck
                SET ""CompanyId"" = cust.""CompanyId""
                FROM ""Customers"" cust
                WHERE cust.""Id"" = ck.""CustomerId"";
            ");

            migrationBuilder.AlterColumn<int>(
                name: "CompanyId",
                table: "Checklists",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Checklists_CompanyId",
                table: "Checklists",
                column: "CompanyId");

            migrationBuilder.AddForeignKey(
                name: "FK_Checklists_Companies_CompanyId",
                table: "Checklists",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Checklists_Companies_CompanyId",
                table: "Checklists");

            migrationBuilder.DropIndex(
                name: "IX_Checklists_CompanyId",
                table: "Checklists");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "Checklists");
        }
    }
}
