using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    public partial class AddCustomerAddressGuestyFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "CustomerAddresses" ADD COLUMN IF NOT EXISTS "GuestyListingId" character varying(80) NULL;
                ALTER TABLE "CustomerAddresses" ADD COLUMN IF NOT EXISTS "GuestyListingTitle" character varying(200) NULL;
                ALTER TABLE "CustomerAddresses" ADD COLUMN IF NOT EXISTS "GuestySyncedAtUtc" timestamp without time zone NULL;

                CREATE INDEX IF NOT EXISTS "IX_CustomerAddresses_CustomerId_GuestyListingId" ON "CustomerAddresses" ("CustomerId", "GuestyListingId");
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Best-effort rollback (idempotent)
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM pg_indexes WHERE indexname = 'IX_CustomerAddresses_CustomerId_GuestyListingId') THEN
                        DROP INDEX "IX_CustomerAddresses_CustomerId_GuestyListingId";
                    END IF;
                END$$;

                ALTER TABLE "CustomerAddresses" DROP COLUMN IF EXISTS "GuestyListingId";
                ALTER TABLE "CustomerAddresses" DROP COLUMN IF EXISTS "GuestyListingTitle";
                ALTER TABLE "CustomerAddresses" DROP COLUMN IF EXISTS "GuestySyncedAtUtc";
                """);
        }
    }
}
