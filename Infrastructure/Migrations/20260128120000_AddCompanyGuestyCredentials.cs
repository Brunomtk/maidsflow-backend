using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    public partial class AddCompanyGuestyCredentials : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GuestyTokenType",
                table: "Companies",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuestyClientId",
                table: "Companies",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuestyClientSecret",
                table: "Companies",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuestyApiType",
                table: "Companies",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuestyAuthBaseUrl",
                table: "Companies",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuestyAuthScope",
                table: "Companies",
                type: "text",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "GuestyTokenType", table: "Companies");
            migrationBuilder.DropColumn(name: "GuestyClientId", table: "Companies");
            migrationBuilder.DropColumn(name: "GuestyClientSecret", table: "Companies");
            migrationBuilder.DropColumn(name: "GuestyApiType", table: "Companies");
            migrationBuilder.DropColumn(name: "GuestyAuthBaseUrl", table: "Companies");
            migrationBuilder.DropColumn(name: "GuestyAuthScope", table: "Companies");
        }
    }
}
