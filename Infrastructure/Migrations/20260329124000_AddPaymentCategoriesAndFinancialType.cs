using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Infrastructure.Migrations
{
    public partial class AddPaymentCategoriesAndFinancialType : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PaymentCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedDate = table.Column<System.DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedDate = table.Column<System.DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentCategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentCategories_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddColumn<string>(
                name: "FinancialType",
                table: "Payments",
                type: "text",
                nullable: false,
                defaultValue: "Income");

            migrationBuilder.AddColumn<int>(
                name: "PaymentCategoryId",
                table: "Payments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentCategoryName",
                table: "Payments",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentCategories_CompanyId_Name",
                table: "PaymentCategories",
                columns: new[] { "CompanyId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_CompanyId_FinancialType_DueDate",
                table: "Payments",
                columns: new[] { "CompanyId", "FinancialType", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_PaymentCategoryId",
                table: "Payments",
                column: "PaymentCategoryId");

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_PaymentCategories_PaymentCategoryId",
                table: "Payments",
                column: "PaymentCategoryId",
                principalTable: "PaymentCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.Sql(@"
INSERT INTO ""PaymentCategories"" (""CompanyId"", ""Name"", ""IsSystem"", ""Active"", ""CreatedDate"", ""UpdatedDate"")
SELECT DISTINCT p.""CompanyId"", 'Appointments', TRUE, TRUE, NOW(), NOW()
FROM ""Payments"" p
WHERE NOT EXISTS (
    SELECT 1
    FROM ""PaymentCategories"" pc
    WHERE pc.""CompanyId"" = p.""CompanyId"" AND pc.""Name"" = 'Appointments'
);

UPDATE ""Payments"" p
SET ""PaymentCategoryId"" = pc.""Id"",
    ""PaymentCategoryName"" = pc.""Name""
FROM ""PaymentCategories"" pc
WHERE pc.""CompanyId"" = p.""CompanyId""
  AND pc.""Name"" = 'Appointments'
  AND p.""PaymentCategoryId"" IS NULL;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Payments_PaymentCategories_PaymentCategoryId",
                table: "Payments");

            migrationBuilder.DropTable(
                name: "PaymentCategories");

            migrationBuilder.DropIndex(
                name: "IX_Payments_CompanyId_FinancialType_DueDate",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_PaymentCategoryId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "FinancialType",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "PaymentCategoryId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "PaymentCategoryName",
                table: "Payments");
        }
    }
}
