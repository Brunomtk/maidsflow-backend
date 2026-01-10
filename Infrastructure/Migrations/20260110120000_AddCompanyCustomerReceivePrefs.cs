using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    public partial class AddCompanyCustomerReceivePrefs : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ReceiveSms",
                table: "Companies",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "ReceiveEmail",
                table: "Companies",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "ReceiveSms",
                table: "Customers",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "ReceiveEmail",
                table: "Customers",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReceiveSms",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "ReceiveEmail",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "ReceiveSms",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "ReceiveEmail",
                table: "Customers");
        }
    }
}
