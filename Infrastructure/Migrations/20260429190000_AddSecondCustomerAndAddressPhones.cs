using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    public partial class AddSecondCustomerAndAddressPhones : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'Customers' AND column_name = 'Phone2'
    ) THEN
        ALTER TABLE ""Customers"" ADD COLUMN ""Phone2"" character varying(32) NULL;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'CustomerAddresses' AND column_name = 'Phone'
    ) THEN
        ALTER TABLE ""CustomerAddresses"" ADD COLUMN ""Phone"" character varying(32) NULL;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'CustomerAddresses' AND column_name = 'Phone2'
    ) THEN
        ALTER TABLE ""CustomerAddresses"" ADD COLUMN ""Phone2"" character varying(32) NULL;
    END IF;
END $$;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'CustomerAddresses' AND column_name = 'Phone2'
    ) THEN
        ALTER TABLE ""CustomerAddresses"" DROP COLUMN ""Phone2"";
    END IF;

    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'CustomerAddresses' AND column_name = 'Phone'
    ) THEN
        ALTER TABLE ""CustomerAddresses"" DROP COLUMN ""Phone"";
    END IF;

    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'Customers' AND column_name = 'Phone2'
    ) THEN
        ALTER TABLE ""Customers"" DROP COLUMN ""Phone2"";
    END IF;
END $$;
");
        }
    }
}
