using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    public partial class AddUniqueIndex_PlanSubscriptions_StripeSubscriptionId : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_indexes
        WHERE schemaname = 'public'
          AND indexname = 'ux_plansubscriptions_stripesubscriptionid'
    ) THEN
        CREATE UNIQUE INDEX ux_plansubscriptions_stripesubscriptionid
            ON ""PlanSubscriptions"" (""StripeSubscriptionId"")
            WHERE ""StripeSubscriptionId"" IS NOT NULL;
    END IF;
END $$;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ux_plansubscriptions_stripesubscriptionid;");
        }
    }
}
