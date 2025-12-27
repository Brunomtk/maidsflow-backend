using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    public partial class avatarcompany : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'Companies'
          AND column_name = 'AvatarKey'
    ) THEN
        ALTER TABLE ""Companies"" ADD COLUMN ""AvatarKey"" text NULL;
    END IF;
END $$;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Down opcional: remover só se existir
            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'Companies'
          AND column_name = 'AvatarKey'
    ) THEN
        ALTER TABLE ""Companies"" DROP COLUMN ""AvatarKey"";
    END IF;
END $$;
");
        }
    }
}
