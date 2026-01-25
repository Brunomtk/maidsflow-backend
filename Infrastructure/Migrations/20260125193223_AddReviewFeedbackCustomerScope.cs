using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    public partial class AddReviewFeedbackCustomerScope : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Reviews: CustomerAddressId
            migrationBuilder.AddColumn<int>(
                name: "CustomerAddressId",
                table: "Reviews",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_CustomerAddressId",
                table: "Reviews",
                column: "CustomerAddressId");

            migrationBuilder.AddForeignKey(
                name: "FK_Reviews_CustomerAddresses_CustomerAddressId",
                table: "Reviews",
                column: "CustomerAddressId",
                principalTable: "CustomerAddresses",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // InternalFeedbacks: AppointmentId / CustomerId / CustomerAddressId
            migrationBuilder.AddColumn<int>(
                name: "AppointmentId",
                table: "InternalFeedbacks",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CustomerId",
                table: "InternalFeedbacks",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CustomerAddressId",
                table: "InternalFeedbacks",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_InternalFeedbacks_AppointmentId",
                table: "InternalFeedbacks",
                column: "AppointmentId");

            migrationBuilder.CreateIndex(
                name: "IX_InternalFeedbacks_CustomerId",
                table: "InternalFeedbacks",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_InternalFeedbacks_CustomerAddressId",
                table: "InternalFeedbacks",
                column: "CustomerAddressId");

            migrationBuilder.AddForeignKey(
                name: "FK_InternalFeedbacks_Appointments_AppointmentId",
                table: "InternalFeedbacks",
                column: "AppointmentId",
                principalTable: "Appointments",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_InternalFeedbacks_Customers_CustomerId",
                table: "InternalFeedbacks",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_InternalFeedbacks_CustomerAddresses_CustomerAddressId",
                table: "InternalFeedbacks",
                column: "CustomerAddressId",
                principalTable: "CustomerAddresses",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // InternalFeedbacks
            migrationBuilder.DropForeignKey(
                name: "FK_InternalFeedbacks_Appointments_AppointmentId",
                table: "InternalFeedbacks");

            migrationBuilder.DropForeignKey(
                name: "FK_InternalFeedbacks_Customers_CustomerId",
                table: "InternalFeedbacks");

            migrationBuilder.DropForeignKey(
                name: "FK_InternalFeedbacks_CustomerAddresses_CustomerAddressId",
                table: "InternalFeedbacks");

            migrationBuilder.DropIndex(
                name: "IX_InternalFeedbacks_AppointmentId",
                table: "InternalFeedbacks");

            migrationBuilder.DropIndex(
                name: "IX_InternalFeedbacks_CustomerId",
                table: "InternalFeedbacks");

            migrationBuilder.DropIndex(
                name: "IX_InternalFeedbacks_CustomerAddressId",
                table: "InternalFeedbacks");

            migrationBuilder.DropColumn(
                name: "AppointmentId",
                table: "InternalFeedbacks");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                table: "InternalFeedbacks");

            migrationBuilder.DropColumn(
                name: "CustomerAddressId",
                table: "InternalFeedbacks");

            // Reviews
            migrationBuilder.DropForeignKey(
                name: "FK_Reviews_CustomerAddresses_CustomerAddressId",
                table: "Reviews");

            migrationBuilder.DropIndex(
                name: "IX_Reviews_CustomerAddressId",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "CustomerAddressId",
                table: "Reviews");
        }
    }
}
