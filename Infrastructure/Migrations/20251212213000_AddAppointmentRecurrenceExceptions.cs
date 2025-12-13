using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Infrastructure.Migrations
{
    public partial class AddAppointmentRecurrenceExceptions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Safety: older DBs might not have this column
            migrationBuilder.Sql("ALTER TABLE \"Appointments\" ADD COLUMN IF NOT EXISTS \"ProfessionalIdsData\" text;");

            migrationBuilder.CreateTable(
                name: "AppointmentRecurrenceExceptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),

                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),

                    SeriesId = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurrenceStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OccurrenceEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),

                    IsCancelled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),

                    OverrideTitle = table.Column<string>(type: "text", nullable: true),
                    OverrideAddress = table.Column<string>(type: "text", nullable: true),
                    OverrideNotes = table.Column<string>(type: "text", nullable: true),

                    OverrideStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OverrideEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),

                    OverrideStatus = table.Column<string>(type: "text", nullable: true),
                    OverrideType = table.Column<string>(type: "text", nullable: true),

                    OverrideProfessionalIdsData = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppointmentRecurrenceExceptions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentRecurrenceExceptions_SeriesId",
                table: "AppointmentRecurrenceExceptions",
                column: "SeriesId");

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentRecurrenceExceptions_SeriesId_OccurrenceStart",
                table: "AppointmentRecurrenceExceptions",
                columns: new[] { "SeriesId", "OccurrenceStart" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "AppointmentRecurrenceExceptions");
        }
    }
}
