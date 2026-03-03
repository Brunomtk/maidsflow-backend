using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    public partial class AddAppointmentMessageLogOccurrenceFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SeriesId",
                table: "AppointmentMessageLogs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OccurrenceStartUtc",
                table: "AppointmentMessageLogs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OccurrenceEndUtc",
                table: "AppointmentMessageLogs",
                type: "timestamp with time zone",
                nullable: true);

            // Index to speed up per-occurrence queries (recurring appointments)
            migrationBuilder.CreateIndex(
                name: "IX_AppointmentMessageLogs_AppointmentId_OccurrenceStartUtc_Kind_Channel_CreatedDate",
                table: "AppointmentMessageLogs",
                columns: new[] { "AppointmentId", "OccurrenceStartUtc", "Kind", "Channel", "CreatedDate" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppointmentMessageLogs_AppointmentId_OccurrenceStartUtc_Kind_Channel_CreatedDate",
                table: "AppointmentMessageLogs");

            migrationBuilder.DropColumn(
                name: "SeriesId",
                table: "AppointmentMessageLogs");

            migrationBuilder.DropColumn(
                name: "OccurrenceStartUtc",
                table: "AppointmentMessageLogs");

            migrationBuilder.DropColumn(
                name: "OccurrenceEndUtc",
                table: "AppointmentMessageLogs");
        }
    }
}
