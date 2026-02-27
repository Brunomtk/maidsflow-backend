using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    public partial class emailiguais : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Índice único case-insensitive para impedir emails duplicados
            migrationBuilder.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Users_Email_Lower_Unique""
                ON ""Users"" (lower(""Email""));
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP INDEX IF EXISTS ""IX_Users_Email_Lower_Unique"";
            ");
        }
    }
}