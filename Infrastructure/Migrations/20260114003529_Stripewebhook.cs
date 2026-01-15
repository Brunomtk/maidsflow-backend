using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Stripewebhook : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // This migration was introduced after an earlier migration that may have already
            // added the same Stripe columns. To keep database updates idempotent (and avoid
            // failing when columns already exist), we use IF NOT EXISTS / IF EXISTS.

            // NOTE: use regular string literals so we can safely escape quotes with \".
            migrationBuilder.Sql("ALTER TABLE \"PlanSubscriptions\" ADD COLUMN IF NOT EXISTS \"StripeSubscriptionId\" character varying(128);");
            migrationBuilder.Sql("ALTER TABLE \"Plans\" ADD COLUMN IF NOT EXISTS \"StripePriceId\" character varying(128);");
            migrationBuilder.Sql("ALTER TABLE \"Plans\" ADD COLUMN IF NOT EXISTS \"StripeProductId\" character varying(128);");
            migrationBuilder.Sql("ALTER TABLE \"Companies\" ADD COLUMN IF NOT EXISTS \"StripeCustomerId\" character varying(128);");

            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_PlanSubscriptions_StripeSubscriptionId\" ON \"PlanSubscriptions\" (\"StripeSubscriptionId\");");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_Plans_StripePriceId\" ON \"Plans\" (\"StripePriceId\");");


        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_PlanSubscriptions_StripeSubscriptionId\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_Plans_StripePriceId\";");

            migrationBuilder.Sql("ALTER TABLE \"PlanSubscriptions\" DROP COLUMN IF EXISTS \"StripeSubscriptionId\";");
            migrationBuilder.Sql("ALTER TABLE \"Plans\" DROP COLUMN IF EXISTS \"StripePriceId\";");
            migrationBuilder.Sql("ALTER TABLE \"Plans\" DROP COLUMN IF EXISTS \"StripeProductId\";");
            migrationBuilder.Sql("ALTER TABLE \"Companies\" DROP COLUMN IF EXISTS \"StripeCustomerId\";");
        }
    }
}
