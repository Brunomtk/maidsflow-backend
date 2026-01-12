using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Infrastructure.Migrations
{
    public partial class AddAppointmentCompletionsAndPayrollItemOccurrences : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppointmentCompletions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    AppointmentId = table.Column<int>(type: "integer", nullable: false),
                    SeriesId = table.Column<Guid>(type: "uuid", nullable: true),
                    OccurrenceStart = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    OccurrenceEnd = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CustomerIdSnapshot = table.Column<int>(type: "integer", nullable: true),
                    TeamIdSnapshot = table.Column<int>(type: "integer", nullable: true),
                    CategorySnapshot = table.Column<string>(type: "text", nullable: true),
                    ServiceTypeIdSnapshot = table.Column<int>(type: "integer", nullable: true),
                    SourceAmountSnapshot = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ProfessionalIdsDataSnapshot = table.Column<string>(type: "text", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppointmentCompletions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppointmentCompletions_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AppointmentCompletions_Appointments_AppointmentId",
                        column: x => x.AppointmentId,
                        principalTable: "Appointments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentCompletions_Company_Appointment_OccurrenceStart",
                table: "AppointmentCompletions",
                columns: new[] { "CompanyId", "AppointmentId", "OccurrenceStart" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentCompletions_Company_Series_OccurrenceStart",
                table: "AppointmentCompletions",
                columns: new[] { "CompanyId", "SeriesId", "OccurrenceStart" });

            // PayrollItems: store occurrence window to support recurring appointments
            migrationBuilder.AddColumn<DateTime>(
                name: "OccurrenceStart",
                table: "PayrollItems",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OccurrenceEnd",
                table: "PayrollItems",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AppointmentCompletionId",
                table: "PayrollItems",
                type: "integer",
                nullable: true);

            // Backfill existing rows from Appointments
            migrationBuilder.Sql(@"
UPDATE ""PayrollItems"" pi
SET ""OccurrenceStart"" = a.""Start"",
    ""OccurrenceEnd"" = a.""End""
FROM ""Appointments"" a
WHERE pi.""AppointmentId"" = a.""Id"" AND pi.""OccurrenceStart"" IS NULL;
");

            migrationBuilder.AlterColumn<DateTime>(
                name: "OccurrenceStart",
                table: "PayrollItems",
                type: "timestamp without time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "OccurrenceEnd",
                table: "PayrollItems",
                type: "timestamp without time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollItems_AppointmentCompletionId",
                table: "PayrollItems",
                column: "AppointmentCompletionId");

            migrationBuilder.AddForeignKey(
                name: "FK_PayrollItems_AppointmentCompletions_AppointmentCompletionId",
                table: "PayrollItems",
                column: "AppointmentCompletionId",
                principalTable: "AppointmentCompletions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // Replace unique index to include OccurrenceStart
            migrationBuilder.DropIndex(
                name: "IX_PayrollItems_Run_Professional_Appointment",
                table: "PayrollItems");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollItems_Run_Professional_Appointment_Occurrence",
                table: "PayrollItems",
                columns: new[] { "PayrollRunId", "ProfessionalId", "AppointmentId", "OccurrenceStart" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PayrollItems_Run_Professional_Appointment_Occurrence",
                table: "PayrollItems");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollItems_Run_Professional_Appointment",
                table: "PayrollItems",
                columns: new[] { "PayrollRunId", "ProfessionalId", "AppointmentId" },
                unique: true);

            migrationBuilder.DropForeignKey(
                name: "FK_PayrollItems_AppointmentCompletions_AppointmentCompletionId",
                table: "PayrollItems");

            migrationBuilder.DropIndex(
                name: "IX_PayrollItems_AppointmentCompletionId",
                table: "PayrollItems");

            migrationBuilder.DropColumn(name: "OccurrenceStart", table: "PayrollItems");
            migrationBuilder.DropColumn(name: "OccurrenceEnd", table: "PayrollItems");
            migrationBuilder.DropColumn(name: "AppointmentCompletionId", table: "PayrollItems");

            migrationBuilder.DropTable(name: "AppointmentCompletions");
        }
    }
}
