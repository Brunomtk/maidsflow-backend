using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    public partial class Reviews : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Converter colunas text -> integer usando USING (Postgres exige)
            migrationBuilder.Sql(@"
                ALTER TABLE ""Reviews""
                ALTER COLUMN ""TeamId"" TYPE integer
                USING NULLIF(trim(""TeamId""), '')::integer;
            ");

            migrationBuilder.Sql(@"
                ALTER TABLE ""Reviews""
                ALTER COLUMN ""ProfessionalId"" TYPE integer
                USING NULLIF(trim(""ProfessionalId""), '')::integer;
            ");

            // Para campos NOT NULL: se estiver vazio, isso vai estourar.
            // (o esperado é já serem números em string)
            migrationBuilder.Sql(@"
                ALTER TABLE ""Reviews""
                ALTER COLUMN ""CustomerId"" TYPE integer
                USING trim(""CustomerId"")::integer;
            ");

            migrationBuilder.Sql(@"
                ALTER TABLE ""Reviews""
                ALTER COLUMN ""CompanyId"" TYPE integer
                USING trim(""CompanyId"")::integer;
            ");

            migrationBuilder.Sql(@"
                ALTER TABLE ""Reviews""
                ALTER COLUMN ""AppointmentId"" TYPE integer
                USING trim(""AppointmentId"")::integer;
            ");

            // 2) Token público e submittedAt
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

            // 3) Índices
            migrationBuilder.CreateIndex(
                name: "IX_Reviews_AppointmentId",
                table: "Reviews",
                column: "AppointmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_PublicToken",
                table: "Reviews",
                column: "PublicToken",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_Reviews_AppointmentId", table: "Reviews");
            migrationBuilder.DropIndex(name: "IX_Reviews_PublicToken", table: "Reviews");

            migrationBuilder.DropColumn(name: "PublicToken", table: "Reviews");
            migrationBuilder.DropColumn(name: "SubmittedAt", table: "Reviews");

            // Voltar integer -> text
            migrationBuilder.Sql(@"
                ALTER TABLE ""Reviews""
                ALTER COLUMN ""TeamId"" TYPE text
                USING ""TeamId""::text;
            ");

            migrationBuilder.Sql(@"
                ALTER TABLE ""Reviews""
                ALTER COLUMN ""ProfessionalId"" TYPE text
                USING ""ProfessionalId""::text;
            ");

            migrationBuilder.Sql(@"
                ALTER TABLE ""Reviews""
                ALTER COLUMN ""CustomerId"" TYPE text
                USING ""CustomerId""::text;
            ");

            migrationBuilder.Sql(@"
                ALTER TABLE ""Reviews""
                ALTER COLUMN ""CompanyId"" TYPE text
                USING ""CompanyId""::text;
            ");

            migrationBuilder.Sql(@"
                ALTER TABLE ""Reviews""
                ALTER COLUMN ""AppointmentId"" TYPE text
                USING ""AppointmentId""::text;
            ");
        }
    }
}
