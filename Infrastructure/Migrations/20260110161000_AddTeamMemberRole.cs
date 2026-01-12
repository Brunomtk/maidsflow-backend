using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamMemberRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Role",
                table: "TeamMembers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Backfill: keep legacy IsLeader semantics.
            migrationBuilder.Sql("UPDATE \"TeamMembers\" SET \"Role\" = 1 WHERE \"IsLeader\" = TRUE;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Role",
                table: "TeamMembers");
        }
    }
}
