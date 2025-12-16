using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    public partial class MakePaymentsPlanOptional_AddCustomerId : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // PlanId: required -> optional
            migrationBuilder.AlterColumn<int>(
                name: "PlanId",
                table: "Payments",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            // CustomerId: new nullable FK to Customers
            migrationBuilder.AddColumn<int>(
                name: "CustomerId",
                table: "Payments",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_CustomerId",
                table: "Payments",
                column: "CustomerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Customers_CustomerId",
                table: "Payments",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Customers_CustomerId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_CustomerId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                table: "Payments");

            // Rollback PlanId to required (best-effort: set default for nulls)
            migrationBuilder.Sql("UPDATE \"Payments\" SET \"PlanId\" = 1 WHERE \"PlanId\" IS NULL;");

            migrationBuilder.AlterColumn<int>(
                name: "PlanId",
                table: "Payments",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
