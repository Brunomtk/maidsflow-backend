using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class _1AddCustomerServiceType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ServiceTypeId",
                table: "Customers",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Customers_ServiceTypeId",
                table: "Customers",
                column: "ServiceTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Customers_ServiceTypes_ServiceTypeId",
                table: "Customers",
                column: "ServiceTypeId",
                principalTable: "ServiceTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Customers_ServiceTypes_ServiceTypeId",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Customers_ServiceTypeId",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "ServiceTypeId",
                table: "Customers");
        }
    }
}
