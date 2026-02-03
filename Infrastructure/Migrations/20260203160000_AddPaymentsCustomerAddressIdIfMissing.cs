using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    // NOTE: Migration class names must be valid C# identifiers (cannot start with numbers).
    public partial class AddPaymentsCustomerAddressIdIfMissing : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Some production DBs may not have Payments.CustomerAddressId yet.
            // Use conditional SQL to avoid failing if it's already present.
            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'Payments'
          AND column_name = 'CustomerAddressId'
    ) THEN
        ALTER TABLE ""Payments"" ADD COLUMN ""CustomerAddressId"" integer NULL;
    END IF;
END$$;
");

            // Create index if missing
            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_class c
        JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE c.relkind = 'i'
          AND c.relname = 'IX_Payments_CustomerAddressId'
          AND n.nspname = 'public'
    ) THEN
        CREATE INDEX ""IX_Payments_CustomerAddressId"" ON ""Payments"" (""CustomerAddressId"");
    END IF;
END$$;
");

            // Add FK constraint if missing
            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'FK_Payments_CustomerAddresses_CustomerAddressId'
    ) THEN
        ALTER TABLE ""Payments""
        ADD CONSTRAINT ""FK_Payments_CustomerAddresses_CustomerAddressId""
        FOREIGN KEY (""CustomerAddressId"") REFERENCES ""CustomerAddresses"" (""Id"")
        ON DELETE SET NULL;
    END IF;
END$$;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Best-effort rollback (safe if objects don't exist)
            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Payments_CustomerAddresses_CustomerAddressId') THEN
        ALTER TABLE ""Payments"" DROP CONSTRAINT ""FK_Payments_CustomerAddresses_CustomerAddressId"";
    END IF;

    IF EXISTS (
        SELECT 1 FROM pg_class c
        JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE c.relkind = 'i'
          AND c.relname = 'IX_Payments_CustomerAddressId'
          AND n.nspname = 'public'
    ) THEN
        DROP INDEX ""IX_Payments_CustomerAddressId"";
    END IF;

    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'Payments'
          AND column_name = 'CustomerAddressId'
    ) THEN
        ALTER TABLE ""Payments"" DROP COLUMN ""CustomerAddressId"";
    END IF;
END$$;
");
        }
    }
}
