using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Infrastructure.Migrations
{
    public partial class AddPayrollRunsAndItems : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PayrollRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ClosedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PaidDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollRuns_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PayrollItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PayrollRunId = table.Column<int>(type: "integer", nullable: false),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    ProfessionalId = table.Column<int>(type: "integer", nullable: false),
                    AppointmentId = table.Column<int>(type: "integer", nullable: false),
                    ServiceTypeId = table.Column<int>(type: "integer", nullable: true),
                    Category = table.Column<string>(type: "text", nullable: true),
                    TeamRole = table.Column<int>(type: "integer", nullable: false),
                    PayrollRuleId = table.Column<int>(type: "integer", nullable: true),
                    PayrollRulePriority = table.Column<int>(type: "integer", nullable: true),
                    RateType = table.Column<int>(type: "integer", nullable: true),
                    RateValue = table.Column<decimal>(type: "numeric", nullable: true),
                    SourceAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CalculatedAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    MissingRule = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollItems_PayrollRuns_PayrollRunId",
                        column: x => x.PayrollRunId,
                        principalTable: "PayrollRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PayrollItems_Professionals_ProfessionalId",
                        column: x => x.ProfessionalId,
                        principalTable: "Professionals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollItems_Appointments_AppointmentId",
                        column: x => x.AppointmentId,
                        principalTable: "Appointments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollItems_ServiceTypes_ServiceTypeId",
                        column: x => x.ServiceTypeId,
                        principalTable: "ServiceTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PayrollItems_PayrollRules_PayrollRuleId",
                        column: x => x.PayrollRuleId,
                        principalTable: "PayrollRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollRuns_CompanyId",
                table: "PayrollRuns",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollItems_PayrollRunId",
                table: "PayrollItems",
                column: "PayrollRunId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollItems_ProfessionalId",
                table: "PayrollItems",
                column: "ProfessionalId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollItems_AppointmentId",
                table: "PayrollItems",
                column: "AppointmentId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollItems_ServiceTypeId",
                table: "PayrollItems",
                column: "ServiceTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollItems_PayrollRuleId",
                table: "PayrollItems",
                column: "PayrollRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollItems_Run_Professional_Appointment",
                table: "PayrollItems",
                columns: new[] { "PayrollRunId", "ProfessionalId", "AppointmentId" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "PayrollItems");
            migrationBuilder.DropTable(name: "PayrollRuns");
        }
    }
}
