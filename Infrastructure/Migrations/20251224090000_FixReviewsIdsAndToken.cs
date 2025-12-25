using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    public partial class FixReviewsIdsAndToken : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // New public token used by the review form link
            migrationBuilder.AddColumn<Guid>(
                name: "PublicToken",
                table: "Reviews",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SubmittedAt",
                table: "Reviews",
                type: "timestamp with time zone",
                nullable: true);

            // Move ID columns from text -> integer (safe migration path)
            migrationBuilder.AddColumn<int>(name: "CustomerId_new", table: "Reviews", type: "integer", nullable: true);
            migrationBuilder.AddColumn<int>(name: "ProfessionalId_new", table: "Reviews", type: "integer", nullable: true);
            migrationBuilder.AddColumn<int>(name: "TeamId_new", table: "Reviews", type: "integer", nullable: true);
            migrationBuilder.AddColumn<int>(name: "CompanyId_new", table: "Reviews", type: "integer", nullable: true);
            migrationBuilder.AddColumn<int>(name: "AppointmentId_new", table: "Reviews", type: "integer", nullable: true);

            // Convert existing numeric strings (if any)
            // NOTE: in verbatim strings (@"..."), double-quotes must be escaped by doubling them ("").
            migrationBuilder.Sql(@"UPDATE ""Reviews"" SET ""CustomerId_new"" = NULLIF(""CustomerId"", '')::integer;");
            migrationBuilder.Sql(@"UPDATE ""Reviews"" SET ""ProfessionalId_new"" = NULLIF(""ProfessionalId"", '')::integer;");
            migrationBuilder.Sql(@"UPDATE ""Reviews"" SET ""TeamId_new"" = NULLIF(""TeamId"", '')::integer;");
            migrationBuilder.Sql(@"UPDATE ""Reviews"" SET ""CompanyId_new"" = NULLIF(""CompanyId"", '')::integer;");
            migrationBuilder.Sql(@"UPDATE ""Reviews"" SET ""AppointmentId_new"" = NULLIF(""AppointmentId"", '')::integer;");

            // For required IDs, keep DB consistent (if any old rows were empty)
            migrationBuilder.Sql(@"UPDATE ""Reviews"" SET ""CustomerId_new"" = 0 WHERE ""CustomerId_new"" IS NULL;");
            migrationBuilder.Sql(@"UPDATE ""Reviews"" SET ""CompanyId_new"" = 0 WHERE ""CompanyId_new"" IS NULL;");
            migrationBuilder.Sql(@"UPDATE ""Reviews"" SET ""AppointmentId_new"" = 0 WHERE ""AppointmentId_new"" IS NULL;");

            migrationBuilder.DropColumn(name: "CustomerId", table: "Reviews");
            migrationBuilder.DropColumn(name: "ProfessionalId", table: "Reviews");
            migrationBuilder.DropColumn(name: "TeamId", table: "Reviews");
            migrationBuilder.DropColumn(name: "CompanyId", table: "Reviews");
            migrationBuilder.DropColumn(name: "AppointmentId", table: "Reviews");

            migrationBuilder.RenameColumn(name: "CustomerId_new", table: "Reviews", newName: "CustomerId");
            migrationBuilder.RenameColumn(name: "ProfessionalId_new", table: "Reviews", newName: "ProfessionalId");
            migrationBuilder.RenameColumn(name: "TeamId_new", table: "Reviews", newName: "TeamId");
            migrationBuilder.RenameColumn(name: "CompanyId_new", table: "Reviews", newName: "CompanyId");
            migrationBuilder.RenameColumn(name: "AppointmentId_new", table: "Reviews", newName: "AppointmentId");

            migrationBuilder.AlterColumn<int>(
                name: "CustomerId",
                table: "Reviews",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "CompanyId",
                table: "Reviews",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "AppointmentId",
                table: "Reviews",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_PublicToken",
                table: "Reviews",
                column: "PublicToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_AppointmentId",
                table: "Reviews",
                column: "AppointmentId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_Reviews_PublicToken", table: "Reviews");
            migrationBuilder.DropIndex(name: "IX_Reviews_AppointmentId", table: "Reviews");

            // Add back text columns
            migrationBuilder.AddColumn<string>(name: "CustomerId_old", table: "Reviews", type: "text", nullable: true);
            migrationBuilder.AddColumn<string>(name: "ProfessionalId_old", table: "Reviews", type: "text", nullable: true);
            migrationBuilder.AddColumn<string>(name: "TeamId_old", table: "Reviews", type: "text", nullable: true);
            migrationBuilder.AddColumn<string>(name: "CompanyId_old", table: "Reviews", type: "text", nullable: true);
            migrationBuilder.AddColumn<string>(name: "AppointmentId_old", table: "Reviews", type: "text", nullable: true);

            migrationBuilder.Sql(@"UPDATE ""Reviews"" SET ""CustomerId_old"" = ""CustomerId""::text;");
            migrationBuilder.Sql(@"UPDATE ""Reviews"" SET ""ProfessionalId_old"" = CASE WHEN ""ProfessionalId"" IS NULL THEN NULL ELSE ""ProfessionalId""::text END;");
            migrationBuilder.Sql(@"UPDATE ""Reviews"" SET ""TeamId_old"" = CASE WHEN ""TeamId"" IS NULL THEN NULL ELSE ""TeamId""::text END;");
            migrationBuilder.Sql(@"UPDATE ""Reviews"" SET ""CompanyId_old"" = ""CompanyId""::text;");
            migrationBuilder.Sql(@"UPDATE ""Reviews"" SET ""AppointmentId_old"" = ""AppointmentId""::text;");

            migrationBuilder.DropColumn(name: "CustomerId", table: "Reviews");
            migrationBuilder.DropColumn(name: "ProfessionalId", table: "Reviews");
            migrationBuilder.DropColumn(name: "TeamId", table: "Reviews");
            migrationBuilder.DropColumn(name: "CompanyId", table: "Reviews");
            migrationBuilder.DropColumn(name: "AppointmentId", table: "Reviews");

            migrationBuilder.RenameColumn(name: "CustomerId_old", table: "Reviews", newName: "CustomerId");
            migrationBuilder.RenameColumn(name: "ProfessionalId_old", table: "Reviews", newName: "ProfessionalId");
            migrationBuilder.RenameColumn(name: "TeamId_old", table: "Reviews", newName: "TeamId");
            migrationBuilder.RenameColumn(name: "CompanyId_old", table: "Reviews", newName: "CompanyId");
            migrationBuilder.RenameColumn(name: "AppointmentId_old", table: "Reviews", newName: "AppointmentId");

            migrationBuilder.DropColumn(name: "PublicToken", table: "Reviews");
            migrationBuilder.DropColumn(name: "SubmittedAt", table: "Reviews");
        }
    }
}
