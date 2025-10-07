using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    public partial class lembredemim : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Se já existir, o Postgres apenas ignora.
            migrationBuilder.Sql(@"ALTER TABLE ""Users"" ADD COLUMN IF NOT EXISTS ""Language"" text NULL;");
            migrationBuilder.Sql(@"ALTER TABLE ""Users"" ADD COLUMN IF NOT EXISTS ""RefreshToken"" text NULL;");
            migrationBuilder.Sql(@"ALTER TABLE ""Users"" ADD COLUMN IF NOT EXISTS ""RefreshTokenExpiresAt"" timestamp with time zone NULL;");
            migrationBuilder.Sql(@"ALTER TABLE ""Users"" ADD COLUMN IF NOT EXISTS ""Theme"" text NULL;");

            // Caso também precise do RememberMe (não está na sua migration atual), descomente:
            // migrationBuilder.Sql(@"ALTER TABLE ""Users"" ADD COLUMN IF NOT EXISTS ""RememberMe"" boolean NULL;");
            // Opcional default:
            // migrationBuilder.Sql(@"ALTER TABLE ""Users"" ALTER COLUMN ""RememberMe"" SET DEFAULT false;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"ALTER TABLE ""Users"" DROP COLUMN IF EXISTS ""Language"";");
            migrationBuilder.Sql(@"ALTER TABLE ""Users"" DROP COLUMN IF EXISTS ""RefreshToken"";");
            migrationBuilder.Sql(@"ALTER TABLE ""Users"" DROP COLUMN IF EXISTS ""RefreshTokenExpiresAt"";");
            migrationBuilder.Sql(@"ALTER TABLE ""Users"" DROP COLUMN IF EXISTS ""Theme"";");

            // Se criou o RememberMe:
            // migrationBuilder.Sql(@"ALTER TABLE ""Users"" DROP COLUMN IF EXISTS ""RememberMe"";");
        }
    }
}
