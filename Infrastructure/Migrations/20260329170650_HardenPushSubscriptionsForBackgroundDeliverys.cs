using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    public partial class HardenPushSubscriptionsForBackgroundDeliverys : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BrowserName",
                table: "PushSubscriptions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CompanyId",
                table: "PushSubscriptions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeviceId",
                table: "PushSubscriptions",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeviceName",
                table: "PushSubscriptions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FailureCount",
                table: "PushSubscriptions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "PushSubscriptions",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPwaInstalled",
                table: "PushSubscriptions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastPushAttemptAtUtc",
                table: "PushSubscriptions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastPushOpenedAtUtc",
                table: "PushSubscriptions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSeenAtUtc",
                table: "PushSubscriptions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSuccessfulPushAtUtc",
                table: "PushSubscriptions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastError",
                table: "PushSubscriptions",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PermissionState",
                table: "PushSubscriptions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Platform",
                table: "PushSubscriptions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserRole",
                table: "PushSubscriptions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.Sql(@"
UPDATE ""PushSubscriptions"" ps
SET ""CompanyId"" = u.""CompanyId"",
    ""UserRole"" = u.""Role"",
    ""IsActive"" = TRUE,
    ""LastSeenAtUtc"" = COALESCE(ps.""LastSeenAtUtc"", ps.""UpdatedDate""),
    ""LastSuccessfulPushAtUtc"" = ps.""LastSuccessfulPushAtUtc""
FROM ""Users"" u
WHERE u.""Id"" = ps.""UserId"";");

            migrationBuilder.CreateIndex(
                name: "IX_PushSubscriptions_UserId_DeviceId",
                table: "PushSubscriptions",
                columns: new[] { "UserId", "DeviceId" });

            migrationBuilder.CreateIndex(
                name: "IX_PushSubscriptions_UserId_IsActive",
                table: "PushSubscriptions",
                columns: new[] { "UserId", "IsActive" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PushSubscriptions_UserId_DeviceId",
                table: "PushSubscriptions");

            migrationBuilder.DropIndex(
                name: "IX_PushSubscriptions_UserId_IsActive",
                table: "PushSubscriptions");

            migrationBuilder.DropColumn(name: "BrowserName", table: "PushSubscriptions");
            migrationBuilder.DropColumn(name: "CompanyId", table: "PushSubscriptions");
            migrationBuilder.DropColumn(name: "DeviceId", table: "PushSubscriptions");
            migrationBuilder.DropColumn(name: "DeviceName", table: "PushSubscriptions");
            migrationBuilder.DropColumn(name: "FailureCount", table: "PushSubscriptions");
            migrationBuilder.DropColumn(name: "IsActive", table: "PushSubscriptions");
            migrationBuilder.DropColumn(name: "IsPwaInstalled", table: "PushSubscriptions");
            migrationBuilder.DropColumn(name: "LastPushAttemptAtUtc", table: "PushSubscriptions");
            migrationBuilder.DropColumn(name: "LastPushOpenedAtUtc", table: "PushSubscriptions");
            migrationBuilder.DropColumn(name: "LastSeenAtUtc", table: "PushSubscriptions");
            migrationBuilder.DropColumn(name: "LastSuccessfulPushAtUtc", table: "PushSubscriptions");
            migrationBuilder.DropColumn(name: "LastError", table: "PushSubscriptions");
            migrationBuilder.DropColumn(name: "PermissionState", table: "PushSubscriptions");
            migrationBuilder.DropColumn(name: "Platform", table: "PushSubscriptions");
            migrationBuilder.DropColumn(name: "UserRole", table: "PushSubscriptions");
        }
    }
}
