using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    public partial class RemoveUnusedGpsTrackingFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Vehicle",
                table: "GpsTrackings");

            migrationBuilder.DropColumn(
                name: "Speed",
                table: "GpsTrackings");

            migrationBuilder.DropColumn(
                name: "Battery",
                table: "GpsTrackings");

            migrationBuilder.DropColumn(
                name: "Accuracy",
                table: "GpsTrackings");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Vehicle",
                table: "GpsTrackings",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<double>(
                name: "Speed",
                table: "GpsTrackings",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "Battery",
                table: "GpsTrackings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "Accuracy",
                table: "GpsTrackings",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);
        }
    }
}
