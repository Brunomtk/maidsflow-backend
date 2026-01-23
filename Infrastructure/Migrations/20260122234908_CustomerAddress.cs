using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CustomerAddress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CustomerAreas_CustomerId_Name_Active",
                table: "CustomerAreas");

            migrationBuilder.AddColumn<int>(
                name: "CustomerAddressId",
                table: "Payments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CustomerAddressId1",
                table: "Payments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CustomerAddressId",
                table: "CustomerAreas",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CustomerAddressId1",
                table: "CustomerAreas",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CustomerAddressId",
                table: "Checklists",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CustomerAddressId1",
                table: "Checklists",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CustomerAddressId",
                table: "Appointments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OverrideCustomerAddressId",
                table: "AppointmentRecurrenceExceptions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CustomerAddressIdSnapshot",
                table: "AppointmentCompletions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerAddressSnapshot",
                table: "AppointmentCompletions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FrequencySnapshot",
                table: "AppointmentCompletions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentMethodSnapshot",
                table: "AppointmentCompletions",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CustomerAddresses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CustomerId = table.Column<int>(type: "integer", nullable: false),
                    Label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AddressLine1 = table.Column<string>(type: "text", nullable: false),
                    AddressLine2 = table.Column<string>(type: "text", nullable: true),
                    City = table.Column<string>(type: "text", nullable: false),
                    State = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    ZipCode = table.Column<string>(type: "text", nullable: true),
                    Observations = table.Column<string>(type: "text", nullable: true),
                    Ticket = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Frequency = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PaymentMethod = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerAddresses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerAddresses_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_CustomerAddressId",
                table: "Payments",
                column: "CustomerAddressId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_CustomerAddressId1",
                table: "Payments",
                column: "CustomerAddressId1");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAreas_CustomerAddressId",
                table: "CustomerAreas",
                column: "CustomerAddressId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAreas_CustomerAddressId1",
                table: "CustomerAreas",
                column: "CustomerAddressId1");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAreas_CustomerId_CustomerAddressId_Name_Active",
                table: "CustomerAreas",
                columns: new[] { "CustomerId", "CustomerAddressId", "Name", "Active" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Checklists_CustomerAddressId",
                table: "Checklists",
                column: "CustomerAddressId");

            migrationBuilder.CreateIndex(
                name: "IX_Checklists_CustomerAddressId1",
                table: "Checklists",
                column: "CustomerAddressId1");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_CustomerAddressId",
                table: "Appointments",
                column: "CustomerAddressId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAddresses_CustomerId",
                table: "CustomerAddresses",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAddresses_CustomerId_IsPrimary",
                table: "CustomerAddresses",
                columns: new[] { "CustomerId", "IsPrimary" });

            migrationBuilder.AddForeignKey(
                name: "FK_Appointments_CustomerAddresses_CustomerAddressId",
                table: "Appointments",
                column: "CustomerAddressId",
                principalTable: "CustomerAddresses",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Checklists_CustomerAddresses_CustomerAddressId",
                table: "Checklists",
                column: "CustomerAddressId",
                principalTable: "CustomerAddresses",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Checklists_CustomerAddresses_CustomerAddressId1",
                table: "Checklists",
                column: "CustomerAddressId1",
                principalTable: "CustomerAddresses",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerAreas_CustomerAddresses_CustomerAddressId",
                table: "CustomerAreas",
                column: "CustomerAddressId",
                principalTable: "CustomerAddresses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerAreas_CustomerAddresses_CustomerAddressId1",
                table: "CustomerAreas",
                column: "CustomerAddressId1",
                principalTable: "CustomerAddresses",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_CustomerAddresses_CustomerAddressId",
                table: "Payments",
                column: "CustomerAddressId",
                principalTable: "CustomerAddresses",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_CustomerAddresses_CustomerAddressId1",
                table: "Payments",
                column: "CustomerAddressId1",
                principalTable: "CustomerAddresses",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Appointments_CustomerAddresses_CustomerAddressId",
                table: "Appointments");

            migrationBuilder.DropForeignKey(
                name: "FK_Checklists_CustomerAddresses_CustomerAddressId",
                table: "Checklists");

            migrationBuilder.DropForeignKey(
                name: "FK_Checklists_CustomerAddresses_CustomerAddressId1",
                table: "Checklists");

            migrationBuilder.DropForeignKey(
                name: "FK_CustomerAreas_CustomerAddresses_CustomerAddressId",
                table: "CustomerAreas");

            migrationBuilder.DropForeignKey(
                name: "FK_CustomerAreas_CustomerAddresses_CustomerAddressId1",
                table: "CustomerAreas");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_CustomerAddresses_CustomerAddressId",
                table: "Payments");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_CustomerAddresses_CustomerAddressId1",
                table: "Payments");

            migrationBuilder.DropTable(
                name: "CustomerAddresses");

            migrationBuilder.DropIndex(
                name: "IX_Payments_CustomerAddressId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_CustomerAddressId1",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_CustomerAreas_CustomerAddressId",
                table: "CustomerAreas");

            migrationBuilder.DropIndex(
                name: "IX_CustomerAreas_CustomerAddressId1",
                table: "CustomerAreas");

            migrationBuilder.DropIndex(
                name: "IX_CustomerAreas_CustomerId_CustomerAddressId_Name_Active",
                table: "CustomerAreas");

            migrationBuilder.DropIndex(
                name: "IX_Checklists_CustomerAddressId",
                table: "Checklists");

            migrationBuilder.DropIndex(
                name: "IX_Checklists_CustomerAddressId1",
                table: "Checklists");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_CustomerAddressId",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "CustomerAddressId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "CustomerAddressId1",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "CustomerAddressId",
                table: "CustomerAreas");

            migrationBuilder.DropColumn(
                name: "CustomerAddressId1",
                table: "CustomerAreas");

            migrationBuilder.DropColumn(
                name: "CustomerAddressId",
                table: "Checklists");

            migrationBuilder.DropColumn(
                name: "CustomerAddressId1",
                table: "Checklists");

            migrationBuilder.DropColumn(
                name: "CustomerAddressId",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "OverrideCustomerAddressId",
                table: "AppointmentRecurrenceExceptions");

            migrationBuilder.DropColumn(
                name: "CustomerAddressIdSnapshot",
                table: "AppointmentCompletions");

            migrationBuilder.DropColumn(
                name: "CustomerAddressSnapshot",
                table: "AppointmentCompletions");

            migrationBuilder.DropColumn(
                name: "FrequencySnapshot",
                table: "AppointmentCompletions");

            migrationBuilder.DropColumn(
                name: "PaymentMethodSnapshot",
                table: "AppointmentCompletions");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAreas_CustomerId_Name_Active",
                table: "CustomerAreas",
                columns: new[] { "CustomerId", "Name", "Active" },
                unique: true);
        }
    }
}
