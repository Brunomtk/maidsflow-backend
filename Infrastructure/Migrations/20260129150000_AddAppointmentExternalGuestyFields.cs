using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <summary>
    /// Adds optional external integration linkage fields to Appointments.
    /// Used for idempotency when creating appointments from Guesty reservations/blocks.
    /// </summary>
    public partial class AddAppointmentExternalGuestyFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "Appointments" ADD COLUMN IF NOT EXISTS "ExternalSource" character varying(40) NULL;
                ALTER TABLE "Appointments" ADD COLUMN IF NOT EXISTS "ExternalReservationId" character varying(120) NULL;
                ALTER TABLE "Appointments" ADD COLUMN IF NOT EXISTS "ExternalListingId" character varying(120) NULL;
                ALTER TABLE "Appointments" ADD COLUMN IF NOT EXISTS "ExternalStatus" character varying(80) NULL;

                CREATE UNIQUE INDEX IF NOT EXISTS "UX_Appointments_Company_ExternalSource_ExternalReservationId"
                ON "Appointments" ("CompanyId", "ExternalSource", "ExternalReservationId")
                WHERE "ExternalReservationId" IS NOT NULL;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM pg_indexes WHERE indexname = 'UX_Appointments_Company_ExternalSource_ExternalReservationId') THEN
                        DROP INDEX "UX_Appointments_Company_ExternalSource_ExternalReservationId";
                    END IF;
                END$$;

                ALTER TABLE "Appointments" DROP COLUMN IF EXISTS "ExternalSource";
                ALTER TABLE "Appointments" DROP COLUMN IF EXISTS "ExternalReservationId";
                ALTER TABLE "Appointments" DROP COLUMN IF EXISTS "ExternalListingId";
                ALTER TABLE "Appointments" DROP COLUMN IF EXISTS "ExternalStatus";
                """);
        }
    }
}
