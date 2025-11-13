using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserAndProfessionalToNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ProfessionalId",
                table: "Notifications",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "Notifications",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CompanyId",
                table: "Checklists",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Checklists_CompanyId",
                table: "Checklists",
                column: "CompanyId");

            migrationBuilder.AddForeignKey(
                name: "FK_Checklists_Companies_CompanyId",
                table: "Checklists",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Checklists_Companies_CompanyId",
                table: "Checklists");

            migrationBuilder.DropIndex(
                name: "IX_Checklists_CompanyId",
                table: "Checklists");

            migrationBuilder.DropColumn(
                name: "ProfessionalId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "Checklists");
        }
    }
}
