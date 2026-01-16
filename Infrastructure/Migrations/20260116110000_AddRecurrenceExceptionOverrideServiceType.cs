using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    public partial class AddRecurrenceExceptionOverrideServiceType : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Allow per-occurrence overrides of ServiceTypeId for recurring series.
            migrationBuilder.Sql("ALTER TABLE \"AppointmentRecurrenceExceptions\" ADD COLUMN IF NOT EXISTS \"OverrideServiceTypeId\" integer;");

            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_AppointmentRecurrenceExceptions_OverrideServiceTypeId\" ON \"AppointmentRecurrenceExceptions\" (\"OverrideServiceTypeId\");");

            // NOTE: This is a verbatim string literal (prefixed with @), so double-quotes must be escaped by doubling them (""), not with backslashes.
            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'FK_AppointmentRecurrenceExceptions_ServiceTypes_OverrideServiceTypeId'
    ) THEN
        ALTER TABLE ""AppointmentRecurrenceExceptions""
        ADD CONSTRAINT ""FK_AppointmentRecurrenceExceptions_ServiceTypes_OverrideServiceTypeId""
        FOREIGN KEY (""OverrideServiceTypeId"")
        REFERENCES ""ServiceTypes"" (""Id"")
        ON DELETE SET NULL;
    END IF;
END $$;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE \"AppointmentRecurrenceExceptions\" DROP CONSTRAINT IF EXISTS \"FK_AppointmentRecurrenceExceptions_ServiceTypes_OverrideServiceTypeId\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_AppointmentRecurrenceExceptions_OverrideServiceTypeId\";");
            migrationBuilder.Sql("ALTER TABLE \"AppointmentRecurrenceExceptions\" DROP COLUMN IF EXISTS \"OverrideServiceTypeId\";");
        }
    }
}
