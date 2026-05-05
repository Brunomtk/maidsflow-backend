using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <summary>
    /// Adds optional <c>Language</c> columns to <c>Customers</c> and <c>Companies</c>.
    /// Used to drive localized outbound communication (SMS / email / PDF / push) per recipient.
    ///
    /// Format: BCP-47-ish strings — "en", "pt-BR", "es", "fr". Null = fallback to default.
    ///
    /// Idempotent: safe to run multiple times. Production may already have these columns
    /// applied manually; the IF NOT EXISTS guard keeps it harmless.
    /// </summary>
    public partial class AddCustomerCompanyLanguage : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'Customers' AND column_name = 'Language'
    ) THEN
        ALTER TABLE ""Customers"" ADD COLUMN ""Language"" character varying(10) NULL;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'Companies' AND column_name = 'Language'
    ) THEN
        ALTER TABLE ""Companies"" ADD COLUMN ""Language"" character varying(10) NULL;
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
        WHERE table_schema = 'public' AND table_name = 'Companies' AND column_name = 'Language'
    ) THEN
        ALTER TABLE ""Companies"" DROP COLUMN ""Language"";
    END IF;

    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'Customers' AND column_name = 'Language'
    ) THEN
        ALTER TABLE ""Customers"" DROP COLUMN ""Language"";
    END IF;
END $$;
");
        }
    }
}
