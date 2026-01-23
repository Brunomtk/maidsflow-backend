using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    public partial class idsduplicados : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Checklists_CustomerAddresses_CustomerAddressId1",
                table: "Checklists");

            migrationBuilder.DropIndex(
                name: "IX_Checklists_CustomerAddressId1",
                table: "Checklists");

            migrationBuilder.DropColumn(
                name: "CustomerAddressId1",
                table: "Checklists");

            migrationBuilder.DropForeignKey(
                name: "FK_CustomerAreas_CustomerAddresses_CustomerAddressId1",
                table: "CustomerAreas");

            migrationBuilder.DropIndex(
                name: "IX_CustomerAreas_CustomerAddressId1",
                table: "CustomerAreas");

            migrationBuilder.DropColumn(
                name: "CustomerAddressId1",
                table: "CustomerAreas");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_CustomerAddresses_CustomerAddressId1",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_CustomerAddressId1",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "CustomerAddressId1",
                table: "Payments");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CustomerAddressId1",
                table: "Checklists",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Checklists_CustomerAddressId1",
                table: "Checklists",
                column: "CustomerAddressId1");

            migrationBuilder.AddForeignKey(
                name: "FK_Checklists_CustomerAddresses_CustomerAddressId1",
                table: "Checklists",
                column: "CustomerAddressId1",
                principalTable: "CustomerAddresses",
                principalColumn: "Id");

            migrationBuilder.AddColumn<int>(
                name: "CustomerAddressId1",
                table: "CustomerAreas",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAreas_CustomerAddressId1",
                table: "CustomerAreas",
                column: "CustomerAddressId1");

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerAreas_CustomerAddresses_CustomerAddressId1",
                table: "CustomerAreas",
                column: "CustomerAddressId1",
                principalTable: "CustomerAddresses",
                principalColumn: "Id");

            migrationBuilder.AddColumn<int>(
                name: "CustomerAddressId1",
                table: "Payments",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_CustomerAddressId1",
                table: "Payments",
                column: "CustomerAddressId1");

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_CustomerAddresses_CustomerAddressId1",
                table: "Payments",
                column: "CustomerAddressId1",
                principalTable: "CustomerAddresses",
                principalColumn: "Id");
        }
    }
}
