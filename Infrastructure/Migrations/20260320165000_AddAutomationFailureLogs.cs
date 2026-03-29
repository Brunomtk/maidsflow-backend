using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    public partial class AddAutomationFailureLogs : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AutomationFailureLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: true),
                    Source = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    WorkflowKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    WorkflowName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NodeName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ErrorDetails = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    ExecutionId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    AppointmentId = table.Column<int>(type: "integer", nullable: true),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: true),
                    AlertEmailTo = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    AlertEmailSent = table.Column<bool>(type: "boolean", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AlertEmailSentAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutomationFailureLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AutomationFailureLogs_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AutomationFailureLogs_CompanyId",
                table: "AutomationFailureLogs",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_AutomationFailureLogs_ExecutionId",
                table: "AutomationFailureLogs",
                column: "ExecutionId");

            migrationBuilder.CreateIndex(
                name: "IX_AutomationFailureLogs_Source_OccurredAtUtc",
                table: "AutomationFailureLogs",
                columns: new[] { "Source", "OccurredAtUtc" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AutomationFailureLogs");
        }
    }
}
