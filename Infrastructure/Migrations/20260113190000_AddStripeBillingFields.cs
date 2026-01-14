using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    public partial class AddStripeBillingFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StripeCustomerId",
                table: "Companies",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StripeProductId",
                table: "Plans",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StripePriceId",
                table: "Plans",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StripeSubscriptionId",
                table: "PlanSubscriptions",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Plans_StripePriceId",
                table: "Plans",
                column: "StripePriceId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanSubscriptions_StripeSubscriptionId",
                table: "PlanSubscriptions",
                column: "StripeSubscriptionId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Plans_StripePriceId",
                table: "Plans");

            migrationBuilder.DropIndex(
                name: "IX_PlanSubscriptions_StripeSubscriptionId",
                table: "PlanSubscriptions");

            migrationBuilder.DropColumn(
                name: "StripeCustomerId",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "StripeProductId",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "StripePriceId",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "StripeSubscriptionId",
                table: "PlanSubscriptions");
        }
    }
}
