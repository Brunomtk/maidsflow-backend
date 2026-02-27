using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    public partial class AddUniqueUsersEmailIndex : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Enforce case-insensitive uniqueness for email.
            // Note: this will fail if duplicates already exist; clean them before applying.
            migrationBuilder.Sql(@"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Users_Email_Lower_Unique"" ON ""Users"" (lower(""Email""));");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_Users_Email_Lower_Unique"";");
        }
    }
}
