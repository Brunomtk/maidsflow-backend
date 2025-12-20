using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    public partial class MakePaymentsPlanOptional_AddCustomerId : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // PlanId: required -> optional
            migrationBuilder.AlterColumn<int>(
                name: "PlanId",
                table: "Payments",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

                        // CustomerId: new nullable FK to Customers (SAFE / idempotente)
            // - não quebra se a coluna já existir
            // - não quebra se o índice/FK já existirem
            migrationBuilder.Sql(@"ALTER TABLE ""Payments"" ADD COLUMN IF NOT EXISTS ""CustomerId"" integer;");

            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_Payments_CustomerId""
ON ""Payments"" (""CustomerId"");");

            migrationBuilder.Sql(@"
DO $$
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM pg_constraint WHERE conname = 'FK_Payments_Customers_CustomerId'
  ) THEN
    ALTER TABLE ""Payments""
    ADD CONSTRAINT ""FK_Payments_Customers_CustomerId""
    FOREIGN KEY (""CustomerId"") REFERENCES ""Customers"" (""Id"")
    ON DELETE SET NULL;
  END IF;
END $$;
");}

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Customers_CustomerId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_CustomerId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                table: "Payments");

            // Rollback PlanId to required (best-effort: set default for nulls)
            migrationBuilder.Sql("UPDATE \"Payments\" SET \"PlanId\" = 1 WHERE \"PlanId\" IS NULL;");

            migrationBuilder.AlterColumn<int>(
                name: "PlanId",
                table: "Payments",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
