using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    public partial class AddAppointmentRecurrenceExceptions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Safety: older DBs might not have this column
            migrationBuilder.Sql("ALTER TABLE \"Appointments\" ADD COLUMN IF NOT EXISTS \"ProfessionalIdsData\" text;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Keep backward-compatible rollback
            migrationBuilder.Sql("ALTER TABLE \"Appointments\" DROP COLUMN IF EXISTS \"ProfessionalIdsData\";");
        }
    }
}
