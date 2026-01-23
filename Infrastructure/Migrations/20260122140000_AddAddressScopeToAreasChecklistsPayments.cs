using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    public partial class AddAddressScopeToAreasChecklistsPayments : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CustomerAddressId",
                table: "CustomerAreas",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CustomerAddressId",
                table: "Checklists",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CustomerAddressId",
                table: "Payments",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAreas_CustomerAddressId",
                table: "CustomerAreas",
                column: "CustomerAddressId");

            migrationBuilder.CreateIndex(
                name: "IX_Checklists_CustomerAddressId",
                table: "Checklists",
                column: "CustomerAddressId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_CustomerAddressId",
                table: "Payments",
                column: "CustomerAddressId");

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerAreas_CustomerAddresses_CustomerAddressId",
                table: "CustomerAreas",
                column: "CustomerAddressId",
                principalTable: "CustomerAddresses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Checklists_CustomerAddresses_CustomerAddressId",
                table: "Checklists",
                column: "CustomerAddressId",
                principalTable: "CustomerAddresses",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_CustomerAddresses_CustomerAddressId",
                table: "Payments",
                column: "CustomerAddressId",
                principalTable: "CustomerAddresses",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.Sql(@"
UPDATE ""CustomerAreas"" a
SET ""CustomerAddressId"" = ca.""Id""
FROM ""CustomerAddresses"" ca
WHERE ca.""CustomerId"" = a.""CustomerId""
  AND ca.""IsPrimary"" = TRUE;
");

            migrationBuilder.Sql(@"
UPDATE ""Checklists"" c
SET ""CustomerAddressId"" = ap.""CustomerAddressId""
FROM ""Appointments"" ap
WHERE c.""AppointmentId"" = ap.""Id""
  AND ap.""CustomerAddressId"" IS NOT NULL;
");

            migrationBuilder.Sql(@"
UPDATE ""Checklists"" c
SET ""CustomerAddressId"" = ca.""Id""
FROM ""CustomerAddresses"" ca
WHERE c.""CustomerAddressId"" IS NULL
  AND ca.""CustomerId"" = c.""CustomerId""
  AND ca.""IsPrimary"" = TRUE;
");

            migrationBuilder.Sql(@"
UPDATE ""Payments"" p
SET ""CustomerAddressId"" = ca.""Id""
FROM ""CustomerAddresses"" ca
WHERE p.""CustomerAddressId"" IS NULL
  AND p.""CustomerId"" IS NOT NULL
  AND ca.""CustomerId"" = p.""CustomerId""
  AND ca.""IsPrimary"" = TRUE;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CustomerAreas_CustomerAddresses_CustomerAddressId",
                table: "CustomerAreas");

            migrationBuilder.DropForeignKey(
                name: "FK_Checklists_CustomerAddresses_CustomerAddressId",
                table: "Checklists");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_CustomerAddresses_CustomerAddressId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_CustomerAreas_CustomerAddressId",
                table: "CustomerAreas");

            migrationBuilder.DropIndex(
                name: "IX_Checklists_CustomerAddressId",
                table: "Checklists");

            migrationBuilder.DropIndex(
                name: "IX_Payments_CustomerAddressId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "CustomerAddressId",
                table: "CustomerAreas");

            migrationBuilder.DropColumn(
                name: "CustomerAddressId",
                table: "Checklists");

            migrationBuilder.DropColumn(
                name: "CustomerAddressId",
                table: "Payments");
        }
    }
}
