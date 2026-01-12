using System;
using Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Infrastructure.Migrations
{
    [DbContext(typeof(DbContextClass))]
    [Migration("20260110162000_AddPayrollRules")]
    public partial class AddPayrollRules : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PayrollRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    ServiceTypeId = table.Column<int>(type: "integer", nullable: true),
                    TeamRole = table.Column<int>(type: "integer", nullable: false),
                    RateType = table.Column<int>(type: "integer", nullable: false),
                    RateValue = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollRules_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PayrollRules_ServiceTypes_ServiceTypeId",
                        column: x => x.ServiceTypeId,
                        principalTable: "ServiceTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollRules_CompanyId",
                table: "PayrollRules",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollRules_ServiceTypeId",
                table: "PayrollRules",
                column: "ServiceTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollRules_CompanyId_ServiceTypeId_TeamRole_Priority",
                table: "PayrollRules",
                columns: new[] { "CompanyId", "ServiceTypeId", "TeamRole", "Priority" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PayrollRules");
        }
    }
}
