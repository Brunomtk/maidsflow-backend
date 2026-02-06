using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <summary>
    /// Snapshot sync migration.
    ///
    /// Intentionally does not change the database schema.
    /// It exists to keep EF Core's ModelSnapshot in sync so future migrations
    /// don't re-scaffold previously applied changes.
    /// </summary>
    public partial class SnapshotSync : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // No-op
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op
        }
    }
}
