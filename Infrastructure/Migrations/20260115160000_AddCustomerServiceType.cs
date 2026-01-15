using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    public partial class AddCustomerServiceType : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add optional ServiceTypeId to Customers.
            // NOTE: use regular string literals for single-line SQL (safe " escaping).
            migrationBuilder.Sql("ALTER TABLE \"Customers\" ADD COLUMN IF NOT EXISTS \"ServiceTypeId\" integer;");

            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_Customers_ServiceTypeId\" ON \"Customers\" (\"ServiceTypeId\");");

            // Add FK only if it doesn't exist (PostgreSQL)
            // NOTE: verbatim string is used for readability; quotes must be doubled inside it.
            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'FK_Customers_ServiceTypes_ServiceTypeId'
    ) THEN
        ALTER TABLE ""Customers""
        ADD CONSTRAINT ""FK_Customers_ServiceTypes_ServiceTypeId""
        FOREIGN KEY (""ServiceTypeId"")
        REFERENCES ""ServiceTypes"" (""Id"")
        ON DELETE SET NULL;
    END IF;
END $$;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE \"Customers\" DROP CONSTRAINT IF EXISTS \"FK_Customers_ServiceTypes_ServiceTypeId\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_Customers_ServiceTypeId\";");
            migrationBuilder.Sql("ALTER TABLE \"Customers\" DROP COLUMN IF EXISTS \"ServiceTypeId\";");
        }
    }
}
