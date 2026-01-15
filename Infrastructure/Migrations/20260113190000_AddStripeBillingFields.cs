using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    public partial class AddStripeBillingFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Keep this migration idempotent and aligned with the current model
            // (columns limited to 128 chars in DbContext mapping).

            // NOTE: using regular string literals here (not verbatim @"...") so we can safely use \" escaping.
            migrationBuilder.Sql("ALTER TABLE \"Companies\" ADD COLUMN IF NOT EXISTS \"StripeCustomerId\" character varying(128);");
            migrationBuilder.Sql("ALTER TABLE \"Plans\" ADD COLUMN IF NOT EXISTS \"StripeProductId\" character varying(128);");
            migrationBuilder.Sql("ALTER TABLE \"Plans\" ADD COLUMN IF NOT EXISTS \"StripePriceId\" character varying(128);");
            migrationBuilder.Sql("ALTER TABLE \"PlanSubscriptions\" ADD COLUMN IF NOT EXISTS \"StripeSubscriptionId\" character varying(128);");

            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_Plans_StripePriceId\" ON \"Plans\" (\"StripePriceId\");");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_PlanSubscriptions_StripeSubscriptionId\" ON \"PlanSubscriptions\" (\"StripeSubscriptionId\");");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_Plans_StripePriceId\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_PlanSubscriptions_StripeSubscriptionId\";");

            migrationBuilder.Sql("ALTER TABLE \"Companies\" DROP COLUMN IF EXISTS \"StripeCustomerId\";");
            migrationBuilder.Sql("ALTER TABLE \"Plans\" DROP COLUMN IF EXISTS \"StripeProductId\";");
            migrationBuilder.Sql("ALTER TABLE \"Plans\" DROP COLUMN IF EXISTS \"StripePriceId\";");
            migrationBuilder.Sql("ALTER TABLE \"PlanSubscriptions\" DROP COLUMN IF EXISTS \"StripeSubscriptionId\";");
        }
    }
}
