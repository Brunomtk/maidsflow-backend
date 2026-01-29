using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    public partial class AddCompanyGuestyToken : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GuestyAccessToken",
                table: "Companies",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "GuestyTokenExpiresAtUtc",
                table: "Companies",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "GuestyTokenUpdatedAtUtc",
                table: "Companies",
                type: "timestamp without time zone",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GuestyAccessToken",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "GuestyTokenExpiresAtUtc",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "GuestyTokenUpdatedAtUtc",
                table: "Companies");
        }
    }
}
