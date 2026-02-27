using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    public partial class AddGpsTrackingSourceAndRefs : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Source",
                table: "GpsTrackings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "AppointmentId",
                table: "GpsTrackings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CustomerId",
                table: "GpsTrackings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CheckRecordId",
                table: "GpsTrackings",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_GpsTrackings_ProfessionalId_Timestamp",
                table: "GpsTrackings",
                columns: new[] { "ProfessionalId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_GpsTrackings_CheckRecordId",
                table: "GpsTrackings",
                column: "CheckRecordId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_GpsTrackings_ProfessionalId_Timestamp",
                table: "GpsTrackings");

            migrationBuilder.DropIndex(
                name: "IX_GpsTrackings_CheckRecordId",
                table: "GpsTrackings");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "GpsTrackings");

            migrationBuilder.DropColumn(
                name: "AppointmentId",
                table: "GpsTrackings");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                table: "GpsTrackings");

            migrationBuilder.DropColumn(
                name: "CheckRecordId",
                table: "GpsTrackings");
        }
    }
}
