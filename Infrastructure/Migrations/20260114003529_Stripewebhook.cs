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



            migrationBuilder.AddColumn<string>(
                name: "StripeSubscriptionId",
                table: "PlanSubscriptions",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StripePriceId",
                table: "Plans",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StripeProductId",
                table: "Plans",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StripeCustomerId",
                table: "Companies",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);



            migrationBuilder.CreateIndex(
                name: "IX_PlanSubscriptions_StripeSubscriptionId",
                table: "PlanSubscriptions",
                column: "StripeSubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_Plans_StripePriceId",
                table: "Plans",
                column: "StripePriceId");


        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            

            migrationBuilder.DropIndex(
                name: "IX_PlanSubscriptions_StripeSubscriptionId",
                table: "PlanSubscriptions");

            migrationBuilder.DropIndex(
                name: "IX_Plans_StripePriceId",
                table: "Plans");

           
            migrationBuilder.DropColumn(
                name: "StripeSubscriptionId",
                table: "PlanSubscriptions");

            migrationBuilder.DropColumn(
                name: "StripePriceId",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "StripeProductId",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "StripeCustomerId",
                table: "Companies");
        }
    }
}
